using Geometry;
using Geometry.Transforms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Map tiles to a section, as in a .mosaic file
    /// </summary>
    class TilesToSectionMapping(Section section, string name, string rootPath, string mosaicPath, string tilePrefix, string tilePostfix) : FixedTileCountMapping(section, name, tilePrefix, tilePostfix)
    {
        private readonly SemaphoreSlim LoadTransformSemaphore = new(1, 1);

        public override bool Initialized => Interlocked.CompareExchange(ref _TileTransforms, _TileTransforms, null) != null;

        private readonly SemaphoreSlim _InitializeSemaphore = new(1);

        private Rectangle _VolumeBounds;
        public override Rectangle ControlBounds => _VolumeBounds;

        private Rectangle _SectionBounds;
        public override Rectangle? SectionBounds => _SectionBounds;

        public override Rectangle? VolumeBounds => _VolumeBounds;

        internal static HttpClient HttpClient => Viking.Common.SharedResources.HttpClient;
        /// <summary>
        /// Starts as false since we don't load transforms from the disk by default.  Once we do this it is set to true. 
        /// </summary>
        protected bool HasBeenLoaded => Interlocked.CompareExchange(ref _TileTransforms, _TileTransforms, null) != null;
        //private readonly ReaderWriterLockSlim rwLockObj = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public override ITransform[] GetLoadedTransformsOrNull()
        {
            if (HasBeenLoaded)
                return _TileTransforms;

            return null;
        }

        public override async Task Initialize(CancellationToken token)
        {
            if (Initialized)
                return;

            try
            {
                await _InitializeSemaphore.WaitAsync(token).ConfigureAwait(false);
                if (Initialized)
                    return;

                var transforms = await LoadTransform(token).ConfigureAwait(false);
                if (token.IsCancellationRequested)
                    return;

                ////Determine boundaries of the section////
                var transformControlPoints = transforms.Cast<ITransformControlPoints>().ToArray();
                _VolumeBounds =
                    Geometry.Transforms.ReferencePointBasedTransform.CalculateControlBounds(transformControlPoints);
                _SectionBounds =
                    Geometry.Transforms.ReferencePointBasedTransform.CalculateMappedBounds(transformControlPoints);
                ///////////////////////////////////////////

                Interlocked.CompareExchange(ref _TileTransforms, transforms, _TileTransforms);
            }
            catch(TaskCanceledException) {
                Debug.WriteLine($"TilesToSectionMapping Initialize call cancelled");
            }
            finally
            {
                _InitializeSemaphore.Release();
            }
        }


        protected ITransform[] _TileTransforms = null;
        public override async Task<ITransform[]> GetOrCreateTransforms(CancellationToken token)
        {
            if (Initialized == false)
            {
                await Initialize(token).ConfigureAwait(false);
            }

            try
            {
                //rwLockObj.EnterReadLock();

                if (_TileTransforms is null || token.IsCancellationRequested)
                    return [];

                return _TileTransforms;
            }
            finally
            {
                //rwLockObj.ExitReadLock();
            }
        }

        protected readonly string RootPath = rootPath;
        /// <summary>
        /// Path to the .mosaic file containing the transforms
        /// </summary>
        protected readonly string MosaicPath = mosaicPath;


        public override string CachedTransformsFileName
        {
            get
            {
                string mosaicName = System.IO.Path.GetFileNameWithoutExtension(MosaicPath);
                return System.IO.Path.Combine(Section.volume.Paths.LocalVolumeDir, Section.Number.ToString("D4") + "_" + mosaicName + ".cache");
            }
        }

        public override bool TrySectionToVolume(Vector2 P, out Vector2 transformedP)
        {
            transformedP = P;
            return true;
        }

        public override bool TryVolumeToSection(Vector2 P, out Vector2 transformedP)
        {
            transformedP = P;
            return true;
        }

        public override Vector2[] VolumeToSection(Vector2[] P)
        {
            Vector2[] transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }

        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TryVolumeToSection(in Vector2[] P, out Vector2[] transformedP)
        {
            transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return [.. P.Select(p => true)];
        }

        public override Vector2[] SectionToVolume(Vector2[] P)
        {
            Vector2[] transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(in Vector2[] P, out Vector2[] transformedP)
        {
            transformedP = new Vector2[P.Length];
            P.CopyTo(transformedP, 0);
            return [.. P.Select(p => true)];
        }


        public override async Task FreeMemory()
        {
            try
            {
                await LoadTransformSemaphore.WaitAsync().ConfigureAwait(false);

                Interlocked.CompareExchange(ref _TileTransforms, null, _TileTransforms);
                await base.FreeMemory().ConfigureAwait(false);
            }
            finally
            {
                LoadTransformSemaphore.Release();
            }
        }

        /// <summary>
        /// Loads the tile transforms with one GET of the .mosaic, sent with <c>If-Modified-Since</c> set to the server's
        /// <c>Last-Modified</c> that the cached transforms were built from (<see cref="CachedServerLastModified"/>). A 304
        /// returns the cache; a 200 parses that same response and replaces the cache. When the server cannot be reached
        /// or answers with an error, the cache is used if there is one.
        /// </summary>
        /// <exception cref="HttpRequestException">The server failed and there is no usable cache.</exception>
        public async Task<ITransform[]> LoadTransform(CancellationToken token)
        {
            Uri mosaicURI = new(VolumePath.Combine(this.RootPath, MosaicPath));
            string cacheFile = CachedTransformsFileName;
            ITransform[] cached = TryLoadUsableCache(cacheFile);
            DateTimeOffset? cachedVersion = cached is null ? null : ConditionalGet.ValidatorFor(CachedServerLastModified(cached));

            using ConditionalGetResult result =
                await ConditionalGet.SendAsync(HttpClient, mosaicURI, cachedVersion, token).ConfigureAwait(false);

            if (result.Outcome == ConditionalGetOutcome.Modified)
            {
                try
                {
                    // MaxValue (no Last-Modified) is stored as "unknown": the next load then sends no date and downloads again.
                    DateTime serverLastModified = result.LastModified?.UtcDateTime ?? DateTime.MaxValue;
                    string[] mosaicLines;
                    using (Stream mosaicDataStream = await result.Response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        mosaicLines = await mosaicDataStream.ToLinesAsync().ConfigureAwait(false);

                    ITransform[] transforms = TransformFactory.LoadMosaic(RootPath, mosaicLines, serverLastModified);
                    // SectionToVolumeMapping rebuilds its warped cache when it is older than this.
                    this._LastModified = serverLastModified == DateTime.MaxValue ? DateTime.UtcNow : serverLastModified;
                    _ = Task.Run(() => SaveToCache(cacheFile, transforms));
                    return transforms;
                }
                catch (Exception e) when (cached is not null && e is HttpRequestException or IOException)
                {
                    Trace.WriteLine($"Reading {mosaicURI} failed ({e.Message}); using the cached transforms.");
                }
            }

            if (cached is not null)
            {
                if (result.Outcome is ConditionalGetOutcome.NotFound or ConditionalGetOutcome.Failed)
                    Trace.WriteLine($"Using cached {cacheFile}: the server answered {DescribeStatus(result)} for {mosaicURI}");
                this._LastModified = System.IO.File.GetLastWriteTimeUtc(cacheFile);
                return cached;
            }

            throw new HttpRequestException($"Could not load {mosaicURI}: {DescribeStatus(result)}");
        }

        private static string DescribeStatus(ConditionalGetResult result) =>
            result.StatusCode.HasValue ? ((int)result.StatusCode.Value).ToString() : "no response";

        /// <summary>
        /// The server's <c>Last-Modified</c> the cached transforms were built from (stored in each transform's info by
        /// <see cref="TransformFactory.LoadMosaic"/>), or <see cref="DateTime.MinValue"/> when unknown.
        /// </summary>
        internal static DateTime CachedServerLastModified(ITransform[] cached) =>
            cached is { Length: > 0 } && cached[0] is ITransformInfo { Info: not null } withInfo
                ? withInfo.Info.LastModified
                : DateTime.MinValue;

        /// <summary>
        /// Loads the cache file, or returns null when it is missing, written before <see cref="Global.OldestValidCachedTransform"/>
        /// (an older format) or unreadable; the last two are deleted.
        /// </summary>
        private ITransform[] TryLoadUsableCache(string cacheFile)
        {
            if (!System.IO.File.Exists(cacheFile))
                return null;

            if (!Geometry.Global.IsCacheFileValid(cacheFile, Global.OldestValidCachedTransform))
            {
                Trace.WriteLine($"Deleting outdated cache file: {cacheFile}");
                Geometry.Global.TryDeleteCacheFile(cacheFile);
                return null;
            }

            try
            {
                return LoadFromCache();
            }
            catch (Exception e)
            {
                Trace.WriteLine($"Could not load {cacheFile} from cache even though file existed: {e.Message}");
                Geometry.Global.TryDeleteCacheFile(cacheFile);
                return null;
            }
        }

        public override TilePyramid VisibleTiles(Rectangle VisibleBounds, double DownSample) => base.VisibleTiles(VisibleBounds, null, DownSample);

        public override System.Threading.Tasks.Task<TilePyramid> VisibleTilesAsync(Rectangle VisibleBounds, double DownSample) => Task.Run(() => base.VisibleTiles(VisibleBounds, null, DownSample));
    }
}
