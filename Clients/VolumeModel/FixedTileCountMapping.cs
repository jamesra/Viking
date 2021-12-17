using Geometry;
using Geometry.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// This is the base class for transforms that use the original tiles where the number of tiles is 
    /// fixed at each resolution and the size varies
    /// </summary>
    public abstract class FixedTileCountMapping : MappingBase
    { 
        public override UnitsAndScale.IAxisUnits XYScale
        {
            get
            {
                return CurrentPyramid.XYScale;
            }
        }

        public abstract Task<ITransform[]> GetOrCreateTransforms(CancellationToken token);

        /// <summary>
        /// Returns NULL if transforms are not loaded
        /// </summary>
        /// <returns></returns>
        public abstract ITransform[] GetLoadedTransformsOrNull();

        /// <summary>
        /// We need to know which pyramid we are working against so we know how many levels are available
        /// </summary>
        public Pyramid CurrentPyramid { get; set; } = null;

        public override int[] AvailableLevels
        {
            get
            {
                if (CurrentPyramid == null)
                    throw new InvalidOperationException("No image pyramid set in FixedTileCountMapping, not using mapping manager?");

                return CurrentPyramid.GetLevels().ToArray();
            }
        }

        /// <summary>
        /// Adjust the downsample level to match the difference between the scale used in the pyramid/mapping and the default scale for the volume
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected override double AdjustDownsampleForScale(double input)
        {
            if (this.CurrentPyramid.XYScale == null)
                return input;

            double relative_scale = this.CurrentPyramid.XYScale.Value / this.Section.XYScale.Value;
            return input / relative_scale;
        }


        /// <summary>
        /// Filename of local cache of transforms
        /// </summary>
        public abstract string CachedTransformsFileName
        {
            get;
        }

        internal string TileTextureFileName(int number)
        {
            ITransform[] transforms = GetLoadedTransformsOrNull();
            if (transforms == null)
                return null;

            Geometry.Transforms.TileTransformInfo info = ((ITransformInfo)transforms[number]).Info as TileTransformInfo;
            if (info == null)
                return null;

            return info.TileFileName;
        }

        internal string TileFileName(string filename, int DownsampleLevel)
        { 
            return $"{CurrentPyramid.Path}{System.IO.Path.DirectorySeparatorChar}{DownsampleLevel:D3}{System.IO.Path.DirectorySeparatorChar}{filename}";  
        }

        protected FixedTileCountMapping(Section section, string name, string Prefix, string Postfix) :
            base(section, name, Prefix, Postfix)
        {
        }

        /*
        private int _Initialized = 0;

        public override bool Initialized => Interlocked.CompareExchange(ref _Initialized, 1, 1) > 0;

        private SemaphoreSlim _InitializeSemaphore = new SemaphoreSlim(1);
        
        public override async Task Initialize(CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref _Initialized, 0, 0) > 0)
                return;

            try
            {
                await _InitializeSemaphore.WaitAsync();
                if (Interlocked.CompareExchange(ref _Initialized, 0, 0) > 0)
                    return;

                var transforms = await GetOrCreateTransforms(token);
                if (token.IsCancellationRequested)
                    return;

                var transformControlPoints = transforms.Cast<ITransformControlPoints>().ToArray();
                _VolumeBounds =
                    Geometry.Transforms.ReferencePointBasedTransform.CalculateControlBounds(transformControlPoints);
                _SectionBounds =
                    Geometry.Transforms.ReferencePointBasedTransform.CalculateMappedBounds(transformControlPoints);
            }
            finally
            {
                _InitializeSemaphore.Release();
            }  
        }*/

        #region CacheIO

        protected static Task SaveToCache(in string CachedTransformsFileName, in ITransform[] transforms)
        {
            //The corrupted memory error disappeared when I stopped using the cache.  There are also 
            //memory leak issues documented on MSDN regarding BinaryFormatters
            //return;
            if (transforms == null)
                return Task.CompletedTask;

            using (FileStream fstream = new FileStream(CachedTransformsFileName, FileMode.Create, FileAccess.Write))
            {
                BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binFormatter.Serialize(fstream, transforms);
            }

            return Task.CompletedTask;
        }

        protected virtual ITransform[] LoadFromCache()
        {
            //The corrupted memory error disappeared when I stopped using the cache.  There are also 
            //memory leak issues documented on MSDN regarding BinaryFormatters
            //return null;

            ITransform[] transforms = null;

            try
            {
                using (FileStream fstream = new FileStream(CachedTransformsFileName, FileMode.Open, FileAccess.Read))
                {
                    BinaryFormatter binFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                    transforms = binFormatter.Deserialize(fstream) as ITransform[];
                }
            }
            catch (Exception e)
            {
                transforms = null;
                Trace.WriteLine(string.Format("Unable to load {0} from cache", CachedTransformsFileName));
                System.IO.File.Delete(CachedTransformsFileName);
            }

            return transforms;
        }

        #endregion

        protected virtual TilePyramid VisibleTiles(GridRectangle VisibleBounds,
                                                GridQuad? SectionVisibleBounds,
                                                double DownSample)
        {
            TilePyramid VisibleTiles = new TilePyramid(VisibleBounds);

            double scaledDownsampleLevel = AdjustDownsampleForScale(DownSample);

            //Setup a larger boundary outside of which we release textures
            GridRectangle releaseBounds = VisibleBounds; //Tiles outside this quad will have textures released
            GridRectangle loadBounds = VisibleBounds;  //Tiles inside this quad will have textures loaded
            GridRectangle abortBounds = VisibleBounds; //Tiles outside this quad will have HTTP requests aborted
            releaseBounds = GridRectangle.Scale(releaseBounds, 1.25 * scaledDownsampleLevel);
            loadBounds = GridRectangle.Scale(loadBounds, 1.1f);
            abortBounds = GridRectangle.Scale(abortBounds, 1.20f * scaledDownsampleLevel);

            //Get ready by loading a smaller texture in case the user scrolls this direction 
            //Once we have smaller textures then increase the quality
            //            int predictiveDownsample = DownSample * 4 > 64 ? 64 : (int)DownSample * 4;

            int roundedDownsample = NearestAvailableLevel(DownSample);
            int roundedScaledDownsample = NearestAvailableLevel(scaledDownsampleLevel);

            //Find the starting level of our rendering
            int iLowestResLevel = AvailableLevels.Length - 1;
            int lowestResLevel = AvailableLevels[iLowestResLevel];

            if (roundedDownsample == int.MaxValue || roundedScaledDownsample == int.MaxValue)
                return VisibleTiles;

            //TODO: Need a flag to indicate if transforms are loaded so we can skip
            ITransform[] Tranforms = GetLoadedTransformsOrNull();
            if (Tranforms == null)
                return VisibleTiles;

            int ExpectedTileCount = Tranforms.Length;
#if DEBUG
            List<Tile> TilesToDraw = new List<Tile>(ExpectedTileCount);
#endif
            //            List<Tile> TilesToLoad = new List<Tile>(ExpectedTileCount);
            List<Task<Tile>> tileTasks = new List<Task<Tile>>();

            foreach (ITransform T in Tranforms)
            {
                if (T is IControlPointTriangulation T_Triangulation)
                {
                    //If this tile has been transformed out of existence then skip it
                    if (T_Triangulation.MapPoints.Length < 3)
                        continue;

                    if (T_Triangulation.TriangleIndicies == null)
                        continue;

                    if (T is ITransformControlPoints T_ControlPoints)
                    {
                        if (VisibleBounds.Intersects(T_ControlPoints.ControlBounds) && T is ITransformInfo T_Info)
                        {
                            if (T_Info.Info is TileTransformInfo info)
                            {
                                int level = lowestResLevel;
                                int iLevel = iLowestResLevel;
                                while (level >= roundedDownsample)
                                {
                                 
                                    /*
                                    if (SectionVisibleBounds != null)
                                    {
                                        GridRectangle MosaicPosition = new GridRectangle(T.mapPoints[0].ControlPoint, T.ImageWidth, T.ImageHeight);
                                        if (SectionVisibleBounds.Contains(MosaicPosition) == false)
                                        {
                                            name = TileFileName(T.Number,predictiveDownsample);
                                            LoadOnly = true; 
                                            continue;
                                        }
                                    }
                                     */

                                    string uniqueID = Tile.CreateUniqueKey(Section.Number, Name, CurrentPyramid.Name,
                                        level, info.TileFileName);
                                    Tile tile = Global.TileCache.Fetch(uniqueID);
                                    if (tile == null && Global.TileCache.ContainsKey(uniqueID) == false)
                                    {
                                        /*
                                           //First create a new tile
                                        PositionNormalTextureVertex[]
                                            verticies = Tile.CalculateVerticies(T_ControlPoints, info);
    
                                        tile = Global.TileCache.ConstructTile(uniqueID,
                                            verticies,
                                            T_Triangulation.TriangleIndicies,
                                            $"{TilePath}/{name}",
                                            name,
                                            //PORT: TileCacheName(T.Number, roundedDownsample),
                                            this.Name,
                                            roundedScaledDownsample,
                                            MipMapLevels);
                                        */
                                        tileTasks.Add(Task.Run(() => CreateTile(uniqueID, level,
                                            T_Triangulation, T_ControlPoints, info)));
                                    }

                                    if (tile != null)
                                    {
                                        VisibleTiles.AddTile(level, tile);
                                    }
#if DEBUG
                                    TilesToDraw.Add(tile);
#endif

                                    iLevel--;
                                    if (iLevel < 0)
                                        break;

                                    level = AvailableLevels[iLevel];
                                }
                            }
                        }
                    }
                }
            }
            
            Task[] tileTaskArray = tileTasks.Cast<Task>().ToArray();
            Task.WaitAll(tileTaskArray);
#if DEBUG
            TilesToDraw.AddRange(tileTasks.Select(t => t.Result));
#endif

            foreach (var task in tileTasks)
            {
                var tile = task.Result;
                VisibleTiles.AddTile(tile.Downsample, tile);
            }

            return VisibleTiles;
        }

        private Tile CreateTile(string uniqueID, int roundedScaledDownsample, in IControlPointTriangulation ctrlTriangulation, in ITransformControlPoints ctrlPoints, in TileTransformInfo info)
        {
            string name = TileFileName(info.TileFileName, roundedScaledDownsample); 
            //First create a new tile
            //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
            PositionNormalTextureVertex[] verticies = Tile.CalculateVerticies(ctrlPoints, info);
            int MipMapLevels = roundedScaledDownsample == this.AvailableLevels[AvailableLevels.Length - 1] ? 0 : 1; //0 = Generate mipmaps for lowest res texture, 1 == no MipMaps for higher res textures in the pyramid

            var tile = Global.TileCache.ConstructTile(uniqueID,
                verticies,
                ctrlTriangulation.TriangleIndicies,
                $"{TilePath}/{name}",
                name,
                //PORT TextureCacheFileName,
                this.Name,
                roundedScaledDownsample,
                MipMapLevels);

            //Check for tiles at higher resolution
            //                        int iTempX = iX / 2;
            //                        int iTempY = iY / 2;
            //                        int iTempDownsample = roundedDownsample * 2;
            return tile;

        }
    }
}
