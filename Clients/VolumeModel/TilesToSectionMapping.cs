using Geometry;
using Geometry.Transforms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

        private GridRectangle _VolumeBounds;
        public override GridRectangle ControlBounds => _VolumeBounds;

        private GridRectangle _SectionBounds;
        public override GridRectangle? SectionBounds => _SectionBounds;

        public override GridRectangle? VolumeBounds => _VolumeBounds;

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
                await _InitializeSemaphore.WaitAsync(token);
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
                await Initialize(token);
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

        public override bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP)
        {
            transformedP = P;
            return true;
        }

        public override bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP)
        {
            transformedP = P;
            return true;
        }

        public override GridVector2[] VolumeToSection(GridVector2[] P)
        {
            GridVector2[] transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }

        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TryVolumeToSection(in GridVector2[] P, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return [.. P.Select(p => true)];
        }

        public override GridVector2[] SectionToVolume(GridVector2[] P)
        {
            GridVector2[] transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return transformedP;
        }

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(in GridVector2[] P, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return [.. P.Select(p => true)];
        }


        public override async Task FreeMemory()
        {
            try
            {
                await LoadTransformSemaphore.WaitAsync();

                Interlocked.CompareExchange(ref _TileTransforms, null, _TileTransforms);
            }
            finally
            {
                LoadTransformSemaphore.Release();
            }
        }

        private static HttpClient CreateRequest()
        {
            //var request = new HttpClient();
            var request = Viking.Common.SharedResources.HttpClient;

            //if (uri.Scheme.ToLower() == "https")
            //    request.Credentials = this.Section.volume.UserCredentials;

            //request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            //request.Timeout = 300000;
            //request.AutomaticDecompression = System.Net.DecompressionMethods.GZip; 
            //request.

            return request;
        }

        private static async Task<DateTime> ServerSideLastModifed(Uri uri, CancellationToken token)
        {
            //HttpWebRequest headerRequest = CreateRequest(uri);
            using var headerRequest = CreateRequest();
            //headerRequest.Method = "HEAD";

            var headerResponse =
                await headerRequest.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            {
                if (false == headerResponse.IsSuccessStatusCode)
                    return DateTime.MinValue;

                var lastModified = headerResponse.Content.Headers.LastModified;
                if (lastModified.HasValue)
                {
                    return lastModified.Value.UtcDateTime;
                }

                return DateTime.MaxValue;
            }
            /*
            using (HttpWebResponse headerResponse = await headerRequest.GetResponseAsync() as HttpWebResponse)
            {
                return headerResponse.LastModified.ToUniversalTime();
            }
            */
        }

        /// <summary>
        /// Loads the transform from the storage device
        /// </summary>
        public async Task<ITransform[]> LoadTransform(CancellationToken token)
        {
            Uri mosaicURI = new(this.RootPath + '/' + MosaicPath);
            DateTime serverlastModified = DateTime.MaxValue;
            serverlastModified = await TilesToSectionMapping.ServerSideLastModifed(mosaicURI, token);
            if (token.IsCancellationRequested)
                return null;

            bool CachedFileUseable;

            //Do we need to delete a stale version of the cache file?
            CachedFileUseable = Geometry.Global.IsCacheFileValid(CachedTransformsFileName,
                [serverlastModified, Global.OldestValidCachedTransform]);

            if (CachedFileUseable == false)
            {
                Trace.WriteLine($"Deleting stale cache file: {this.CachedTransformsFileName}");
                Geometry.Global.TryDeleteCacheFile(CachedTransformsFileName);
            }

            if (CachedFileUseable)
            {
                try
                {
                    var loadedTransforms = LoadFromCache();
                    var loadedFromCache = loadedTransforms != null;
                    if (loadedFromCache)
                    {
                        this._LastModified = System.IO.File.GetLastWriteTimeUtc(this.CachedTransformsFileName);
                        return loadedTransforms;
                    }
                }
                catch (Exception)
                {
                    //On any error, use the traditional path
                    this._TileTransforms = null;
                    Trace.WriteLine($"Could not load {CachedTransformsFileName} from cache even though file existed");
                    Geometry.Global.TryDeleteCacheFile(CachedTransformsFileName);
                }
            }

            //Not in the local cache
            var transforms = await LoadTransforms(mosaicURI, RootPath, serverlastModified, token);
            bool loadedFromServer = transforms != null;
            if (loadedFromServer)
            {
                Task.Run(() => SaveToCache(this.CachedTransformsFileName, transforms));
                return transforms;
            }

            Trace.WriteLine($"Unable to load transform from server: {mosaicURI}");
            return null;
        }

        private static async Task<ITransform[]> LoadTransforms(Uri mosaicURI, string RootPath, DateTime serverlastModified, CancellationToken token)
        {
            try
            {
                var request = CreateRequest();
                using var MosaicDataStream = await request.GetStreamAsync(mosaicURI);
                string[] MosaicLines = await MosaicDataStream.ToLinesAsync();
                return TransformFactory.LoadMosaic(RootPath, MosaicLines, serverlastModified);
                /*
                HttpWebRequest request = CreateRequest(mosaicURI);
                using (WebResponse response = await request.GetResponseAsync())
                {
                    using (Stream MosaicDataStream = response.GetResponseStream())
                    {
                        string[] MosaicLines = await MosaicDataStream.ToLinesAsync(); 
                        return TransformFactory.LoadMosaic(RootPath, MosaicLines, serverlastModified);
                    }
                }
                */
            }
            catch (System.Net.WebException webException)
            {
                Trace.WriteLine("Could not load transform: " + mosaicURI);
                Trace.WriteLine(webException.ToString());
            }

            return null;
        }

        public override TilePyramid VisibleTiles(GridRectangle VisibleBounds, double DownSample) => base.VisibleTiles(VisibleBounds, null, DownSample);

        public override System.Threading.Tasks.Task<TilePyramid> VisibleTilesAsync(GridRectangle VisibleBounds, double DownSample) => Task.Run(() => base.VisibleTiles(VisibleBounds, null, DownSample));
    }
}
