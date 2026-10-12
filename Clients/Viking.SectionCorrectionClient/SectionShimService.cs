using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// Keeps a view-only translation ("shim") current for the section on screen. The correction service publishes one
    /// lattice per section and changes it only on a rebuild, so the service downloads whole lattices (the section on
    /// screen first, then its neighbours in the direction of travel), samples them locally for the view's centre and
    /// four corners, fades the offset in by downsample using <see cref="Settings"/>, and hands the result to the apply
    /// callback in volume units. Camera moves and section steps that hit a cached lattice need no network at all.
    /// </summary>
    /// <remarks>
    /// Cached lattices are valid while the service's version token (see <see cref="ISectionShimSource.GetVersionAsync"/>)
    /// is unchanged; it is read before the first download for a StosGroup and again every
    /// <see cref="DefaultVersionCheckInterval"/>, and a change drops that StosGroup's lattices.
    /// Threading: <see cref="SceneChanged"/> and <see cref="Configure"/> may be called from the UI thread and do no
    /// I/O. The apply callback runs on the caller's thread for cached lattices and on a thread-pool thread when a
    /// download completes, so a UI host must marshal inside the callback. Nothing here touches the camera.
    /// Disposing cancels all downloads in flight.
    /// </remarks>
    public sealed class SectionShimService : IDisposable
    {
        /// <summary>How often the service's version token is re-read while the viewer is in use.</summary>
        public static readonly TimeSpan DefaultVersionCheckInterval = TimeSpan.FromMinutes(10);

        const int MaxCachedSections = 48;
        const int MaxConcurrentDownloads = 3;
        static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(30);

        readonly ISectionShimSource _source;
        readonly Action<double, double> _apply;
        readonly Action<string> _log;
        readonly TimeSpan _versionCheckInterval;
        readonly object _lock = new object();
        readonly Dictionary<(string Stos, long Z), LatticeEntry> _lattices = new Dictionary<(string, long), LatticeEntry>();
        readonly HashSet<(string Stos, long Z)> _downloading = new HashSet<(string, long)>();
        readonly Dictionary<string, string> _versions = new Dictionary<string, string>();
        readonly Dictionary<string, DateTime> _nextVersionCheckUtc = new Dictionary<string, DateTime>();
        readonly HashSet<string> _checkingVersion = new HashSet<string>();
        List<(string Stos, long Z)> _wanted = new List<(string, long)>();

        Uri _serviceUrl;
        string _database;
        double _nmPerPixel = 1;
        View _view;
        CancellationTokenSource _cancellation = new CancellationTokenSource();
        int _generation;
        long _tick;
        DateTime _backoffUntilUtc = DateTime.MinValue;
        double _appliedX = double.NaN;
        double _appliedY = double.NaN;
        bool _disposed;

        /// <param name="source">Where lattices come from.</param>
        /// <param name="apply">Receives the faded shim in volume units (the camera's units). See the threading remarks.</param>
        /// <param name="settings">Fade preferences; may be changed at any time.</param>
        /// <param name="log">Optional diagnostics sink.</param>
        /// <param name="versionCheckInterval">How often to re-read the service's version token; defaults to <see cref="DefaultVersionCheckInterval"/>.</param>
        public SectionShimService(
            ISectionShimSource source,
            Action<double, double> apply,
            SectionShimSettings settings = null,
            Action<string> log = null,
            TimeSpan? versionCheckInterval = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            Settings = settings ?? new SectionShimSettings();
            _log = log;
            _versionCheckInterval = versionCheckInterval ?? DefaultVersionCheckInterval;
        }

        /// <summary>Fade preferences. Takes effect on the next <see cref="SceneChanged"/>.</summary>
        public SectionShimSettings Settings { get; }

        /// <summary>True when a correction service is configured for the open annotation context, whether or not the shim is switched on.</summary>
        public bool IsAvailable
        {
            get
            {
                lock (_lock)
                    return _serviceUrl != null;
            }
        }

        /// <summary>
        /// Sets the service for the open annotation context. A null or invalid URL disables the shim and clears it.
        /// </summary>
        /// <param name="correctionServiceUrl">URL Identity sent for the context, or null when it has none.</param>
        /// <param name="annotationDatabase">Annotation database name sent by Identity (the correction set key).</param>
        /// <param name="nmPerPixel">Volume units to nanometres (the volume's XY scale).</param>
        public void Configure(string correctionServiceUrl, string annotationDatabase, double nmPerPixel)
        {
            bool enabled = Uri.TryCreate(correctionServiceUrl, UriKind.Absolute, out Uri url)
                && !string.IsNullOrWhiteSpace(annotationDatabase)
                && nmPerPixel > 0;
            lock (_lock)
            {
                DropEverythingLocked();
                _backoffUntilUtc = DateTime.MinValue;
                _serviceUrl = enabled ? url : null;
                _database = enabled ? annotationDatabase : null;
                _nmPerPixel = enabled ? nmPerPixel : 1;
            }

            if (!enabled)
                SetApplied(0, 0);
        }

        /// <summary>
        /// Reports the current scene. Applies the shim sampled from the cached lattice for this section at once and
        /// queues whatever lattices are still missing around it.
        /// </summary>
        /// <param name="stosGroup">Active transform (StosGroup) name; null or empty means no correction set.</param>
        /// <param name="z">Section number on screen.</param>
        /// <param name="centerX">View centre X in volume units, before the shim (the camera's LookAt).</param>
        /// <param name="centerY">View centre Y in volume units, before the shim.</param>
        /// <param name="halfWidth">Half the visible width in volume units.</param>
        /// <param name="halfHeight">Half the visible height in volume units.</param>
        /// <param name="downsample">Camera downsample.</param>
        /// <param name="neighborZs">
        /// Sections the user is likely to step to next, nearest first (for example z-1, z+1, z-2, z+2). Their lattices
        /// are downloaded after the section on screen, those in the direction of travel first.
        /// </param>
        /// <remarks>
        /// While the section's lattice is still downloading the apply callback receives zero; it receives the sampled
        /// shim as soon as the download completes.
        /// </remarks>
        public void SceneChanged(string stosGroup, long z, double centerX, double centerY, double halfWidth, double halfHeight, double downsample,
            IReadOnlyList<long> neighborZs = null)
        {
            // A switched-off shim behaves as a zero weight: nothing is downloaded and the view is not shifted.
            double weight = Settings.Enabled ? Settings.Weight(downsample) : 0;
            SectionLatticeField field = null;
            bool sectionChanged;
            string sectionLog = null;
            double nmPerPixel;
            View view;
            List<Download> jobs;
            ProbeJob probe;
            lock (_lock)
            {
                if (_serviceUrl == null || _disposed)
                    return;

                View previous = _view;
                view = new View(stosGroup ?? string.Empty, z, centerX, centerY, halfWidth, halfHeight, downsample, neighborZs, previous);
                _view = view;
                nmPerPixel = _nmPerPixel;
                sectionChanged = previous == null || previous.Z != z || previous.Stos != view.Stos;
                if (view.Stos.Length > 0)
                {
                    field = TouchLocked((view.Stos, z))?.Field;
                    PlanLocked(view, weight);
                    probe = TakeProbeLocked(view.Stos, weight);
                    jobs = TakeDownloadsLocked();
                    if (sectionChanged && _log != null)
                    {
                        sectionLog = $"Section z={z}: lattice {(_lattices.ContainsKey((view.Stos, z)) ? (field != null ? $"cached ({field.NodeCount} nodes)" : "cached as none") : "not cached")}, "
                            + $"queued z=[{string.Join(",", _wanted.Select(w => w.Z))}], weight={weight:0.00}";
                    }
                }
                else
                {
                    probe = null;
                    jobs = new List<Download>();
                }
            }

            if (sectionLog != null)
                _log(sectionLog);
            ApplyFaded(field == null ? (ShimSample?)null : Reduce(field, view, nmPerPixel), weight, nmPerPixel);
            Start(probe, jobs);
        }

        /// <summary>
        /// Rebuilds the ordered download list for the view: the section on screen, then its neighbours with those in
        /// the direction of travel first, skipping lattices already held or in flight. Caller holds <see cref="_lock"/>.
        /// </summary>
        void PlanLocked(View view, double weight)
        {
            var wanted = new List<(string, long)>();
            if (weight > 0 && DateTime.UtcNow >= _backoffUntilUtc)
            {
                foreach (long z in new[] { view.Z }.Concat(view.NeighborZs))
                {
                    var key = (view.Stos, z);
                    if (TouchLocked(key) == null && !_downloading.Contains(key))
                        wanted.Add(key);
                }
            }

            _wanted = wanted;
        }

        /// <summary>Marks a held lattice as recently used and returns it, or null when it is not held.</summary>
        LatticeEntry TouchLocked((string Stos, long Z) key)
        {
            if (!_lattices.TryGetValue(key, out LatticeEntry entry))
                return null;
            entry.Used = ++_tick;
            return entry;
        }

        /// <summary>
        /// A version read is due when the StosGroup has none yet or the last one is old. Downloads wait for the first
        /// one so every lattice is stored against a known version. Caller holds <see cref="_lock"/>.
        /// </summary>
        ProbeJob TakeProbeLocked(string stos, double weight)
        {
            if (weight <= 0 || DateTime.UtcNow < _backoffUntilUtc || _checkingVersion.Contains(stos))
                return null;
            bool due = !_versions.ContainsKey(stos)
                || (_nextVersionCheckUtc.TryGetValue(stos, out DateTime next) && DateTime.UtcNow >= next);
            if (!due)
                return null;

            _checkingVersion.Add(stos);
            return new ProbeJob(stos, _serviceUrl, _database, _generation, _cancellation.Token);
        }

        /// <summary>
        /// Takes the next downloads off the wanted list, up to the concurrency limit, for StosGroups whose version is
        /// known. Caller holds <see cref="_lock"/> and starts the returned jobs after releasing it.
        /// </summary>
        List<Download> TakeDownloadsLocked()
        {
            var jobs = new List<Download>();
            while (_downloading.Count < MaxConcurrentDownloads && _wanted.Count > 0)
            {
                (string stos, long z) = _wanted[0];
                _wanted.RemoveAt(0);
                if (!_versions.ContainsKey(stos) || _lattices.ContainsKey((stos, z)) || !_downloading.Add((stos, z)))
                    continue;
                jobs.Add(new Download(stos, z, _serviceUrl, _database, _generation, _cancellation.Token));
            }

            return jobs;
        }

        void Start(ProbeJob probe, List<Download> jobs)
        {
            if (probe != null)
                _ = CheckVersionAsync(probe);
            foreach (Download job in jobs)
                _ = DownloadAsync(job);
        }

        async Task CheckVersionAsync(ProbeJob job)
        {
            try
            {
                string version = await _source.GetVersionAsync(job.Url, job.Database, job.Stos, job.Token).ConfigureAwait(false);
                if (job.Token.IsCancellationRequested)
                    return;

                List<Download> jobs;
                lock (_lock)
                {
                    _checkingVersion.Remove(job.Stos);
                    if (_disposed || job.Generation != _generation)
                        return;

                    version = version ?? string.Empty;
                    if (_versions.TryGetValue(job.Stos, out string known) && known != version)
                    {
                        _log?.Invoke($"Correction set for '{job.Stos}' changed ('{known}' to '{version}'); dropping cached lattices.");
                        DropLatticesLocked(job.Stos);
                    }

                    _versions[job.Stos] = version;
                    _nextVersionCheckUtc[job.Stos] = DateTime.UtcNow + _versionCheckInterval;
                    if (_view != null && _view.Stos == job.Stos)
                        PlanLocked(_view, Settings.Enabled ? Settings.Weight(_view.Downsample) : 0);
                    jobs = TakeDownloadsLocked();
                }

                ApplyForLatestView();
                Start(null, jobs);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _checkingVersion.Remove(job.Stos);
                    _backoffUntilUtc = DateTime.UtcNow + FailureBackoff;
                }

                _log?.Invoke($"Section correction version check failed: {ex.Message}");
            }
        }

        async Task DownloadAsync(Download job)
        {
            var clock = Stopwatch.StartNew();
            try
            {
                SectionLatticeField field = await _source
                    .GetLatticeAsync(job.Url, job.Database, job.Stos, job.Z, job.Token).ConfigureAwait(false);
                if (job.Token.IsCancellationRequested)
                    return;

                List<Download> jobs;
                lock (_lock)
                {
                    _downloading.Remove((job.Stos, job.Z));
                    if (_disposed || job.Generation != _generation)
                        return;

                    // A section the service has no map for is stored as "no correction" so it is not asked again.
                    _lattices[(job.Stos, job.Z)] = new LatticeEntry(field) { Used = ++_tick };
                    EvictLocked();
                    jobs = TakeDownloadsLocked();
                }

                _log?.Invoke($"Lattice for z={job.Z}: {(field == null ? "none" : field.NodeCount + " nodes")} in {clock.ElapsedMilliseconds} ms");
                ApplyForLatestView();
                Start(null, jobs);
            }
            catch (OperationCanceledException)
            {
                lock (_lock)
                    _downloading.Remove((job.Stos, job.Z));
            }
            catch (Exception ex)
            {
                lock (_lock)
                {
                    _downloading.Remove((job.Stos, job.Z));
                    _backoffUntilUtc = DateTime.UtcNow + FailureBackoff;
                }

                _log?.Invoke($"Section correction lattice download for z={job.Z} failed: {ex.Message}");
            }
        }

        void ApplyForLatestView()
        {
            SectionLatticeField field = null;
            View view;
            double weight;
            double nmPerPixel;
            lock (_lock)
            {
                if (_view == null || _disposed)
                    return;
                view = _view;
                weight = Settings.Enabled ? Settings.Weight(view.Downsample) : 0;
                nmPerPixel = _nmPerPixel;
                field = TouchLocked((view.Stos, view.Z))?.Field;
            }

            ApplyFaded(field == null ? (ShimSample?)null : Reduce(field, view, nmPerPixel), weight, nmPerPixel);
        }

        /// <summary>
        /// The mean offset of the trusted samples at the view's centre and four corners, so a point far from any
        /// support cannot drag the whole view. Null when no sample is trusted.
        /// </summary>
        static ShimSample? Reduce(SectionLatticeField field, View view, double nmPerPixel)
        {
            double sumX = 0;
            double sumY = 0;
            int count = 0;
            void Add(double x, double y)
            {
                (double dx, double dy, bool trusted) = field.Sample(x * nmPerPixel, y * nmPerPixel);
                if (!trusted)
                    return;
                sumX += dx;
                sumY += dy;
                count++;
            }

            Add(view.CenterX, view.CenterY);
            foreach (double sx in new[] { -1.0, 1.0 })
            {
                foreach (double sy in new[] { -1.0, 1.0 })
                    Add(view.CenterX + sx * view.HalfWidth, view.CenterY + sy * view.HalfHeight);
            }

            return count == 0 ? (ShimSample?)null : new ShimSample(sumX / count, sumY / count);
        }

        void ApplyFaded(ShimSample? raw, double weight, double nmPerPixel)
        {
            if (raw is ShimSample s && weight > 0)
                SetApplied(s.DxNm / nmPerPixel * weight, s.DyNm / nmPerPixel * weight);
            else
                SetApplied(0, 0);
        }

        void SetApplied(double dx, double dy)
        {
            lock (_lock)
            {
                if (dx == _appliedX && dy == _appliedY)
                    return;
                _appliedX = dx;
                _appliedY = dy;
            }

            _apply(dx, dy);
        }

        /// <summary>Drops the least recently used lattices beyond <see cref="MaxCachedSections"/>. Caller holds <see cref="_lock"/>.</summary>
        void EvictLocked()
        {
            while (_lattices.Count > MaxCachedSections)
            {
                var oldest = _lattices.OrderBy(kv => kv.Value.Used).First().Key;
                _lattices.Remove(oldest);
            }
        }

        void DropLatticesLocked(string stos)
        {
            foreach (var key in _lattices.Keys.Where(k => k.Stos == stos).ToList())
                _lattices.Remove(key);
            // Downloads started before the change must not store their older lattices.
            _generation++;
            _downloading.RemoveWhere(k => k.Stos == stos);
        }

        void DropEverythingLocked()
        {
            _cancellation.Cancel();
            _cancellation.Dispose();
            _cancellation = new CancellationTokenSource();
            _generation++;
            _lattices.Clear();
            _downloading.Clear();
            _versions.Clear();
            _nextVersionCheckUtc.Clear();
            _checkingVersion.Clear();
            _wanted = new List<(string, long)>();
        }

        /// <summary>Cancels any download in flight.</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _cancellation.Cancel();
                _cancellation.Dispose();
            }
        }

        sealed class View
        {
            public View(string stos, long z, double centerX, double centerY, double halfWidth, double halfHeight, double downsample,
                IReadOnlyList<long> neighborZs, View previous)
            {
                Stos = stos;
                Z = z;
                CenterX = centerX;
                CenterY = centerY;
                HalfWidth = halfWidth;
                HalfHeight = halfHeight;
                Downsample = downsample;

                IEnumerable<long> neighbors = (neighborZs ?? Array.Empty<long>()).Where(n => n != z).Distinct();
                // Stepping in a direction predicts the next sections better than the ones behind.
                int direction = previous == null || previous.Stos != stos ? 0 : Math.Sign(z - previous.Z);
                NeighborZs = direction == 0
                    ? neighbors.ToArray()
                    : neighbors.OrderBy(n => Math.Sign(n - z) == direction ? 0 : 1).ToArray();
            }

            /// <summary>Sections to download after <see cref="Z"/>, most likely next first.</summary>
            public IReadOnlyList<long> NeighborZs { get; }

            public string Stos { get; }
            public long Z { get; }
            public double CenterX { get; }
            public double CenterY { get; }
            public double HalfWidth { get; }
            public double HalfHeight { get; }
            public double Downsample { get; }
        }

        sealed class LatticeEntry
        {
            public LatticeEntry(SectionLatticeField field) => Field = field;

            /// <summary>The lattice, or null when the service had no correction for the section (kept so it is not asked again).</summary>
            public SectionLatticeField Field { get; }

            /// <summary>Order of last use, for eviction.</summary>
            public long Used { get; set; }
        }

        sealed class Download
        {
            public Download(string stos, long z, Uri url, string database, int generation, CancellationToken token)
            {
                Stos = stos;
                Z = z;
                Url = url;
                Database = database;
                Generation = generation;
                Token = token;
            }

            public string Stos { get; }
            public long Z { get; }
            public Uri Url { get; }
            public string Database { get; }
            public int Generation { get; }
            public CancellationToken Token { get; }
        }

        sealed class ProbeJob
        {
            public ProbeJob(string stos, Uri url, string database, int generation, CancellationToken token)
            {
                Stos = stos;
                Url = url;
                Database = database;
                Generation = generation;
                Token = token;
            }

            public string Stos { get; }
            public Uri Url { get; }
            public string Database { get; }
            public int Generation { get; }
            public CancellationToken Token { get; }
        }
    }
}
