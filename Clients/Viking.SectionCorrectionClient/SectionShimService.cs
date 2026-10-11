using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.SectionCorrectionClient
{
    /// <summary>
    /// Keeps a view-only translation ("shim") current for the section on screen. The host reports the scene with
    /// <see cref="SceneChanged"/> on every camera change; the service asks the correction service for the offset at the
    /// view's centre and four corners (debounced, one request in flight, results cached), fades the offset in by
    /// downsample using <see cref="Settings"/>, and hands the result to the apply callback in volume units.
    /// </summary>
    /// <remarks>
    /// Threading: <see cref="SceneChanged"/> and <see cref="Configure"/> may be called from the UI thread and are cheap
    /// (no I/O). The apply callback runs on the caller's thread for cached values and on a thread-pool thread when a
    /// request completes, so a UI host must marshal inside the callback. Nothing here touches the camera.
    /// Disposing cancels any request in flight.
    /// </remarks>
    public sealed class SectionShimService : IDisposable
    {
        const int MaxCachedViewsPerSection = 8;
        static readonly TimeSpan FailureBackoff = TimeSpan.FromSeconds(30);

        readonly ISectionShimSource _source;
        readonly Action<double, double> _apply;
        readonly Action<string> _log;
        readonly TimeSpan _debounce;
        readonly object _lock = new object();
        readonly Timer _timer;
        readonly Dictionary<(string Stos, long Z), List<CachedView>> _cache = new Dictionary<(string, long), List<CachedView>>();

        Uri _serviceUrl;
        string _database;
        double _nmPerPixel = 1;
        View _view;
        CancellationTokenSource _inFlight;
        DateTime _backoffUntilUtc = DateTime.MinValue;
        double _appliedX = double.NaN;
        double _appliedY = double.NaN;
        bool _disposed;

        /// <param name="source">Where offsets come from.</param>
        /// <param name="apply">Receives the faded shim in volume units (the camera's units). See the threading remarks.</param>
        /// <param name="settings">Fade preferences; may be changed at any time.</param>
        /// <param name="log">Optional diagnostics sink.</param>
        /// <param name="debounce">Quiet time after the last scene change before a request is sent.</param>
        public SectionShimService(
            ISectionShimSource source,
            Action<double, double> apply,
            SectionShimSettings settings = null,
            Action<string> log = null,
            TimeSpan? debounce = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _apply = apply ?? throw new ArgumentNullException(nameof(apply));
            Settings = settings ?? new SectionShimSettings();
            _log = log;
            _debounce = debounce ?? TimeSpan.FromMilliseconds(250);
            _timer = new Timer(_ => OnTimer(), null, Timeout.Infinite, Timeout.Infinite);
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
                CancelInFlightLocked();
                _cache.Clear();
                _backoffUntilUtc = DateTime.MinValue;
                _serviceUrl = enabled ? url : null;
                _database = enabled ? annotationDatabase : null;
                _nmPerPixel = enabled ? nmPerPixel : 1;
            }

            if (!enabled)
                SetApplied(0, 0);
        }

        /// <summary>
        /// Reports the current scene. Applies the cached, faded shim immediately and, when the shim is not fully
        /// faded out and nothing cached covers this view, schedules a request.
        /// </summary>
        /// <param name="stosGroup">Active transform (StosGroup) name; null or empty means no correction set.</param>
        /// <param name="z">Section number on screen.</param>
        /// <param name="centerX">View centre X in volume units, before the shim (the camera's LookAt).</param>
        /// <param name="centerY">View centre Y in volume units, before the shim.</param>
        /// <param name="halfWidth">Half the visible width in volume units.</param>
        /// <param name="halfHeight">Half the visible height in volume units.</param>
        /// <param name="downsample">Camera downsample.</param>
        public void SceneChanged(string stosGroup, long z, double centerX, double centerY, double halfWidth, double halfHeight, double downsample)
        {
            // A switched-off shim behaves as a zero weight: nothing is requested and the view is not shifted.
            double weight = Settings.Enabled ? Settings.Weight(downsample) : 0;
            ShimSample? raw = null;
            bool needRequest = false;
            lock (_lock)
            {
                if (_serviceUrl == null || _disposed)
                    return;

                _view = new View(stosGroup ?? string.Empty, z, centerX, centerY, halfWidth, halfHeight, downsample);
                if (_view.Stos.Length == 0)
                {
                    // No active StosGroup: nothing to key a set by.
                }
                else
                {
                    CachedView cached = FindLocked(_view);
                    needRequest = cached == null && weight > 0 && DateTime.UtcNow >= _backoffUntilUtc;
                    // Until the exact view is answered, the nearest answer on this section beats snapping to zero.
                    raw = (cached ?? NearestLocked(_view))?.Sample;
                }
            }

            ApplyFaded(raw, weight);
            if (needRequest)
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }

        void OnTimer()
        {
            View view;
            Uri url;
            string database;
            double nmPerPixel;
            CancellationToken token;
            lock (_lock)
            {
                if (_disposed || _serviceUrl == null || _view == null)
                    return;
                view = _view;
                url = _serviceUrl;
                database = _database;
                nmPerPixel = _nmPerPixel;
                CancelInFlightLocked();
                _inFlight = new CancellationTokenSource();
                token = _inFlight.Token;
            }

            _ = RequestAsync(view, url, database, nmPerPixel, token);
        }

        async Task RequestAsync(View view, Uri url, string database, double nmPerPixel, CancellationToken token)
        {
            try
            {
                var points = new List<(double X, double Y)>(5)
                {
                    (view.CenterX * nmPerPixel, view.CenterY * nmPerPixel)
                };
                foreach (double sx in new[] { -1.0, 1.0 })
                {
                    foreach (double sy in new[] { -1.0, 1.0 })
                        points.Add(((view.CenterX + sx * view.HalfWidth) * nmPerPixel, (view.CenterY + sy * view.HalfHeight) * nmPerPixel));
                }

                ShimSample? sample = await _source
                    .GetShimAsync(url, database, view.Stos, view.Z, points, token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return;

                lock (_lock)
                {
                    if (_disposed || !ReferenceEquals(_serviceUrl, url))
                        return;
                    StoreLocked(view, sample);
                }

                ApplyForLatestView();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                lock (_lock)
                    _backoffUntilUtc = DateTime.UtcNow + FailureBackoff;
                _log?.Invoke($"Section correction shim request failed: {ex.Message}");
            }
        }

        void ApplyForLatestView()
        {
            ShimSample? raw = null;
            double weight = 0;
            lock (_lock)
            {
                if (_view == null || _disposed)
                    return;
                weight = Settings.Enabled ? Settings.Weight(_view.Downsample) : 0;
                raw = (FindLocked(_view) ?? NearestLocked(_view))?.Sample;
            }

            ApplyFaded(raw, weight);
        }

        void ApplyFaded(ShimSample? raw, double weight)
        {
            double nmPerPixel;
            lock (_lock)
                nmPerPixel = _nmPerPixel;

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

        CachedView FindLocked(View view)
        {
            if (!_cache.TryGetValue((view.Stos, view.Z), out List<CachedView> views))
                return null;

            double extent = view.Extent;
            foreach (CachedView c in views)
            {
                double ratio = extent / c.Extent;
                if (ratio < 0.75 || ratio > 1.34)
                    continue;
                if (Math.Abs(c.CenterX - view.CenterX) <= 0.25 * c.Extent && Math.Abs(c.CenterY - view.CenterY) <= 0.25 * c.Extent)
                    return c;
            }

            return null;
        }

        CachedView NearestLocked(View view)
        {
            if (!_cache.TryGetValue((view.Stos, view.Z), out List<CachedView> views))
                return null;

            CachedView best = null;
            double bestDistance = 2 * view.Extent;
            foreach (CachedView c in views)
            {
                double dx = c.CenterX - view.CenterX;
                double dy = c.CenterY - view.CenterY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance <= bestDistance)
                {
                    best = c;
                    bestDistance = distance;
                }
            }

            return best;
        }

        void StoreLocked(View view, ShimSample? sample)
        {
            var key = (view.Stos, view.Z);
            if (!_cache.TryGetValue(key, out List<CachedView> views))
            {
                views = new List<CachedView>();
                _cache[key] = views;
            }

            views.Add(new CachedView(view.CenterX, view.CenterY, view.Extent, sample));
            if (views.Count > MaxCachedViewsPerSection)
                views.RemoveAt(0);
        }

        void CancelInFlightLocked()
        {
            _inFlight?.Cancel();
            _inFlight?.Dispose();
            _inFlight = null;
        }

        /// <summary>Stops the debounce timer and cancels any request in flight.</summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;
                _disposed = true;
                CancelInFlightLocked();
            }

            _timer.Dispose();
        }

        sealed class View
        {
            public View(string stos, long z, double centerX, double centerY, double halfWidth, double halfHeight, double downsample)
            {
                Stos = stos;
                Z = z;
                CenterX = centerX;
                CenterY = centerY;
                HalfWidth = halfWidth;
                HalfHeight = halfHeight;
                Downsample = downsample;
            }

            public string Stos { get; }
            public long Z { get; }
            public double CenterX { get; }
            public double CenterY { get; }
            public double HalfWidth { get; }
            public double HalfHeight { get; }
            public double Downsample { get; }
            public double Extent => Math.Max(2 * HalfWidth, 2 * HalfHeight);
        }

        sealed class CachedView
        {
            public CachedView(double centerX, double centerY, double extent, ShimSample? sample)
            {
                CenterX = centerX;
                CenterY = centerY;
                Extent = extent;
                Sample = sample;
            }

            public double CenterX { get; }
            public double CenterY { get; }
            public double Extent { get; }

            /// <summary>The offset, or null when the service had no correction here (cached so it is not asked again).</summary>
            public ShimSample? Sample { get; }
        }
    }
}
