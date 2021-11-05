using Geometry;
using Geometry.Transforms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Map tiles to a section, as in a .mosaic file
    /// </summary>
    class TilesToSectionMapping : FixedTileCountMapping
    {
        private readonly SemaphoreSlim LoadTransformSemaphore = new SemaphoreSlim(1 , 1);
        /// <summary>
        /// Starts as false since we don't load transforms from the disk by default.  Once we do this it is set to true. 
        /// </summary>
        protected bool HasBeenLoaded = false;
        private readonly ReaderWriterLockSlim rwLockObj = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

        public override ITransform[] GetLoadedTransformsOrNull()
        {
            if (HasBeenLoaded)
                return _TileTransforms;

            return null;
        }

        protected ITransform[] _TileTransforms = null;
        public async override Task<ITransform[]> GetOrCreateTransforms()
        {
            if (HasBeenLoaded == false)
            {
                await LoadTransform().ConfigureAwait(false);
            }

            try
            {
                rwLockObj.EnterReadLock();

                if (_TileTransforms == null)
                    return Array.Empty<ITransform>();

                return _TileTransforms;
            }
            finally
            {
                rwLockObj.ExitReadLock();
            }
        }

        /*
        public override ITransform[] TileTransforms
        {
            get
            { 
                if (HasBeenLoaded == false)
                {
                    var t = Task.Run(() => LoadTransform());
                    t.Wait();
                }

                try
                {
                    rwLockObj.EnterReadLock();

                    if (_TileTransforms == null)
                        return new ITransform[0];

                    return _TileTransforms;
                }
                finally
                {
                    rwLockObj.ExitUpgradeableReadLock();
                } 
            }
        }
        */

        protected string RootPath;
        /// <summary>
        /// Path to the .mosaic file containing the transforms
        /// </summary>
        protected string MosaicPath;


        public override string CachedTransformsFileName
        {
            get
            {
                string mosaicName = System.IO.Path.GetFileNameWithoutExtension(MosaicPath);
                return System.IO.Path.Combine(Section.volume.Paths.LocalVolumeDir, Section.Number.ToString("D4") + "_" + mosaicName + ".cache");
            }
        }



        public TilesToSectionMapping(Section section, string name, string rootPath, string mosaicPath, string tilePrefix, string tilePostfix) :
            base(section, name, tilePrefix, tilePostfix)
        {
            this.RootPath = rootPath;
            this.MosaicPath = mosaicPath;
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
        public override bool[] TryVolumeToSection(GridVector2[] P, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return P.Select(p => { return true; }).ToArray();
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
        public override bool[] TrySectionToVolume(GridVector2[] P, out GridVector2[] transformedP)
        {
            transformedP = new GridVector2[P.Length];
            P.CopyTo(transformedP, 0);
            return P.Select(p => { return true; }).ToArray();
        }


        public override void FreeMemory()
        {
            lock (rwLockObj)
            {
                HasBeenLoaded = false;
                _TileTransforms = null;
            }
        }

        private static System.Net.HttpWebRequest CreateRequest(Uri uri)
        {
            System.Net.HttpWebRequest request = System.Net.WebRequest.CreateDefault(uri) as System.Net.HttpWebRequest;
            //if (uri.Scheme.ToLower() == "https")
            //    request.Credentials = this.Section.volume.UserCredentials;

            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.Revalidate);
            request.Timeout = 300000;
            request.AutomaticDecompression = System.Net.DecompressionMethods.GZip;

            return request;
        }

        private DateTime ServerSideLastModifed(Uri uri)
        {
            HttpWebRequest headerRequest = CreateRequest(uri);
            headerRequest.Method = "HEAD";

            using (HttpWebResponse headerResponse = headerRequest.GetResponse() as HttpWebResponse)
            {
                return headerResponse.LastModified.ToUniversalTime();
            }
        }

        /// <summary>
        /// Loads the transform from the storage device
        /// </summary>
        public async Task LoadTransform()
        {
            try
            { 
                await LoadTransformSemaphore.WaitAsync();

                try
                {
                    rwLockObj.EnterReadLock();
                    if (HasBeenLoaded)
                        return;
                }
                finally
                {
                    rwLockObj.EnterReadLock();
                }

                Uri mosaicURI = new Uri(this.RootPath + '/' + MosaicPath);
                DateTime serverlastModified = DateTime.MaxValue; 
                serverlastModified = ServerSideLastModifed(mosaicURI);

                //Do we need to delete a stale version of the cache file?
                if (System.IO.File.Exists(this.CachedTransformsFileName))
                {
                    if (System.IO.File.GetLastWriteTimeUtc(this.CachedTransformsFileName) < serverlastModified)
                    {
                        Trace.WriteLine("Deleting stale cache file: " + this.CachedTransformsFileName);
                        try
                        {
                            System.IO.File.Delete(this.CachedTransformsFileName);
                        }
                        catch (System.IO.IOException e)
                        {
                            Trace.WriteLine("Failed to delete stale cache file: " + this.CachedTransformsFileName);
                        }
                    }
                }

                //The file was in the internet cache.  Do we have a pre-parsed local copy we've processed in our cache?
                bool LoadedFromCache = false;
                if (System.IO.File.Exists(this.CachedTransformsFileName))
                {
                    try
                    {
                        var loadedTransforms = LoadFromCache();
                        LoadedFromCache = loadedTransforms != null;
                        if (LoadedFromCache)
                        {
                            try
                            {
                                rwLockObj.EnterWriteLock();
                                this._TileTransforms = loadedTransforms;
                                LoadedFromCache = true;
                                this._LastModified = System.IO.File.GetLastWriteTimeUtc(this.CachedTransformsFileName);
                                this.HasBeenLoaded = true;
                                return;
                            }
                            finally
                            {
                                rwLockObj.ExitWriteLock();
                            }
                        }
                    }
                    catch (Exception)
                    {
                        //On any error, use the traditional path
                        this._TileTransforms = null;
                        LoadedFromCache = false;
                        Trace.WriteLine(string.Format("Could not load {0} from cache even though file existed", CachedTransformsFileName));
                    } 
                }

                //Not in the local cache
                var transforms = await LoadTransforms(mosaicURI, RootPath, serverlastModified);
                bool LoadedFromServer = transforms != null;
                if (LoadedFromServer)
                { 
                    SaveToCache(this.CachedTransformsFileName, transforms);

                    try
                    {
                        rwLockObj.EnterWriteLock();
                        this._TileTransforms = transforms;
                        HasBeenLoaded = true;
                    }
                    finally
                    {
                        rwLockObj.ExitWriteLock();
                    }
                }  
            }
            finally
            {
                LoadTransformSemaphore.Release();
            }
        }

        private static async Task<ITransform[]> LoadTransforms(Uri mosaicURI, string RootPath, DateTime serverlastModified)
        {
            try
            {
                HttpWebRequest request = CreateRequest(mosaicURI);
                using (WebResponse response = await request.GetResponseAsync())
                {
                    using (Stream MosaicDataStream = response.GetResponseStream())
                    {
                        string[] MosaicLines = await Geometry.StreamUtil.StreamToLines(MosaicDataStream);

                        return TransformFactory.LoadMosaic(RootPath, MosaicLines, serverlastModified);
                    }
                }
            }
            catch (System.Net.WebException webException)
            {
                Trace.WriteLine("Could not load transform: " + mosaicURI);
                Trace.WriteLine(webException.ToString());
            }

            return null;
        }

        public override TilePyramid VisibleTiles(in GridRectangle VisibleBounds, double DownSample)
        {
            return base.VisibleTiles(VisibleBounds, null, DownSample);
        }

        public override System.Threading.Tasks.Task<TilePyramid> VisibleTilesAsync(GridRectangle VisibleBounds, double DownSample)
        {
            var vb = VisibleBounds;
            return Task.Run(() => base.VisibleTiles(vb, null, DownSample));
        }
    }
}
