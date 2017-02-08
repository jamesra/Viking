using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using Geometry.Transforms;
using System.Diagnostics;
using System.Web;
using System.IO;
using System.Net;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Map tiles to a section, as in a .mosaic file
    /// </summary>
    class TilesToSectionMapping : FixedTileCountMapping
    {
        /// <summary>
        /// Starts as false since we don't load transforms from the disk by default.  Once we do this it is set to true. 
        /// </summary>
        protected bool HasBeenLoaded = false;
        private object LockObj = new object();

        public override ITransform[] TileTransforms
        {
            get 
            {
                if (HasBeenLoaded == false)
                    LoadTransform();

                if (_TileTransforms == null)
                    return new ITransform[0]; 

                return _TileTransforms;
            }
        }

        protected string RootPath; 
        /// <summary>
        /// Path to the .mosaic file containing the transforms
        /// </summary>
        protected string MosaicPath;


        public override string CachedTransformsFileName
        {
            get {
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
            lock (LockObj)
            {
                HasBeenLoaded = false;
                _TileTransforms = null; 
            }
        }

        private System.Net.HttpWebRequest CreateRequest(Uri uri)
        {
            System.Net.HttpWebRequest request = System.Net.WebRequest.CreateDefault(uri) as System.Net.HttpWebRequest;
            if (uri.Scheme.ToLower() == "https")
                request.Credentials = this.Section.volume.UserCredentials;

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
        public void LoadTransform()
        {
            lock (LockObj)
            {
                if (HasBeenLoaded)
                    return;

                Uri mosaicURI = new Uri(this.RootPath + '/' + MosaicPath);


                DateTime serverlastModified = DateTime.MaxValue;
                try
                {
                    serverlastModified = ServerSideLastModifed(mosaicURI); 

                    //Do we need to delete a stale version of the cache file?
                    if (System.IO.File.Exists(this.CachedTransformsFileName))
                    {
                        if (System.IO.File.GetLastWriteTime(this.CachedTransformsFileName) < serverlastModified)
                        {
                            Trace.WriteLine("Deleting stale cache file: " + this.CachedTransformsFileName);
                            try
                            {
                                System.IO.File.Delete(this.CachedTransformsFileName);
                            }
                            catch(System.IO.IOException e)
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
                            this._TileTransforms = LoadFromCache();
                            LoadedFromCache = true;
                            this._LastModified = System.IO.File.GetLastWriteTime(this.CachedTransformsFileName);
                        }
                        catch (Exception)
                        {
                            //On any error, use the traditional path
                            this._TileTransforms = null;
                            LoadedFromCache = false;
                        }

                        if (this._TileTransforms == null)
                            LoadedFromCache = false;

                        if (LoadedFromCache)
                        {
                            this.HasBeenLoaded = true;
                            return;
                        }
                    }

                    //Not in the local cache

                    HttpWebRequest request = CreateRequest(mosaicURI);
                    using (WebResponse response = request.GetResponse())
                    {
                        using (Stream MosaicDataStream = response.GetResponseStream())
                        {
                            string[] MosaicLines = Geometry.StreamUtil.StreamToLines(MosaicDataStream);

                            this._TileTransforms = TransformFactory.LoadMosaic(this.RootPath, MosaicLines, serverlastModified);

                            HasBeenLoaded = _TileTransforms != null;
                            if (HasBeenLoaded)
                                SaveToCache();
                        }
                    }

                }
                catch (System.Net.WebException webException)
                {
                    Trace.WriteLine("Could not load transform: " + mosaicURI);
                    Trace.WriteLine(webException.ToString());
                }  
            }
        }
        
        public override TilePyramid VisibleTiles(GridRectangle VisibleBounds, double DownSample)
        {
            return base.VisibleTiles(VisibleBounds, null, DownSample);
        }
    }
}
