using Geometry;
using Geometry.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VolumeModel;

namespace Viking.VolumeModel
{
    /// <summary>
    /// This is the base class for transforms that use the original tiles where the number of tiles is 
    /// fixed at each resolution and the size varies
    /// </summary>
    public abstract class FixedTileCountMapping(Section section, string name, string Prefix, string Postfix) : MappingBase(section, name, Prefix, Postfix)
    {
        public override UnitsAndScale.IAxisUnits XYScale => CurrentPyramid.XYScale;

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
                if (CurrentPyramid is null)
                    throw new InvalidOperationException("No image pyramid set in FixedTileCountMapping, not using mapping manager?");

                return [.. CurrentPyramid.GetLevels()];
            }
        }

        /// <summary>
        /// Adjust the downsample level to match the difference between the scale used in the pyramid/mapping and the default scale for the volume
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected override double AdjustDownsampleForScale(double input)
        {
            if (this.CurrentPyramid.XYScale is null)
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
            if (transforms is null)
                return null;

            if (((ITransformInfo)transforms[number]).Info is not TileTransformInfo info)
                return null;

            return info.TileFileName;
        }

        internal string TileFileName(string filename, int DownsampleLevel) => $"{CurrentPyramid.Path}{System.IO.Path.DirectorySeparatorChar}{DownsampleLevel:D3}{System.IO.Path.DirectorySeparatorChar}{filename}";

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
            //Replaced BinaryFormatter with modern JSON serialization to avoid security vulnerabilities
            if (transforms is null)
                return Task.CompletedTask;

            using (FileStream fstream = new(CachedTransformsFileName, FileMode.Create, FileAccess.Write))
            {
                JsonTransformSerializer.SerializeArray(fstream, transforms);
            }

            return Task.CompletedTask;
        }

        protected virtual ITransform[] LoadFromCache()
        {
            //Replaced BinaryFormatter with modern JSON deserialization to avoid security vulnerabilities

            ITransform[] transforms = null;

            try
            {
                using FileStream fstream = new(CachedTransformsFileName, FileMode.Open, FileAccess.Read);
                transforms = JsonTransformSerializer.DeserializeArray(fstream);
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
            TilePyramid VisibleTiles = new(VisibleBounds);

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
            if (Tranforms is null)
                return VisibleTiles;

            int ExpectedTileCount = Tranforms.Length;
#if DEBUG
            List<TileViewModel> TilesToDraw = new(ExpectedTileCount);
#endif
            //            List<Tile> TilesToLoad = new List<Tile>(ExpectedTileCount);
            List<Task<TileViewModel>> tileTasks = [];

            foreach (ITransform T in Tranforms)
            {
                if (T is IContinuousTransform T_Cont)
                {
                    if (T is ITransformInfo T_Info)
                    {
                        if (T_Info.Info is TileTransformInfo info)
                        {
                            GridVector2[] corners =
                            [
                                GridVector2.Zero,
                                new(info.ImageWidth, 0),
                                new(0, info.ImageHeight),
                                new(info.ImageWidth, info.ImageHeight)
                            ];

                            var target_corners = T.Transform(corners);
                            var target_bbox = target_corners.BoundingBox();

                            if (VisibleBounds.Intersects(target_bbox))
                            {
                                var tasks = GetOrCreateTiles(T, info, roundedDownsample);
                                tileTasks.AddRange(tasks);
                            }
                        }
                    }
                }

                if (T is IControlPointTriangulation T_Triangulation)
                {
                    //If this tile has been transformed out of existence then skip it
                    if (T_Triangulation.MapPoints.Length < 3)
                        continue;

                    if (T_Triangulation.TriangleIndicies is null)
                        continue;

                    if (T is ITransformControlPoints T_ControlPoints)
                    {
                        if (VisibleBounds.Intersects(T_ControlPoints.ControlBounds) && T is ITransformInfo T_Info)
                        {
                            if (T_Info.Info is TileTransformInfo info)
                            {
                                var tasks = GetOrCreateTiles(T, info, roundedDownsample);
                                tileTasks.AddRange(tasks);
                            }
                        }
                    }
                }
            }

            // Only include tiles already completed (cached hits via Task.FromResult).
            // Background CreateTile tasks continue asynchronously; they will populate
            // Global.TileCache and appear on the next draw cycle.
            foreach (var task in tileTasks)
            {
                if (task.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                {
                    var tile = task.Result;
#if DEBUG
                    TilesToDraw.Add(tile);
#endif
                    VisibleTiles.AddTile(tile.Downsample, tile);
                }
            }

            return VisibleTiles;
        }

        private IList<Task<TileViewModel>> GetOrCreateTiles(ITransform T, TileTransformInfo info, int roundedDownsample)
        {
            int iLowestResLevel = AvailableLevels.Length - 1;
            int lowestResLevel = AvailableLevels[iLowestResLevel];
            int level = lowestResLevel;
            int iLevel = iLowestResLevel;
            List<Task<TileViewModel>> tileTasks = [];
            while (level >= roundedDownsample)
            {
                string uniqueID = TileViewModel.CreateUniqueKey(Section.Number, Name, CurrentPyramid.Name,
                    level, info.TileFileName);

                if (Global.TileCache.TryGetValue(uniqueID, out TileViewModel tileViewModel))
                    //Add the existing tile to the task list
                    tileTasks.Add(Task.FromResult(tileViewModel));
                else
                {
                    if (T is IControlPointTriangulation T_Triangulation)
                        tileTasks.Add(Task.Run(() => CreateTile(uniqueID, level,
                            T_Triangulation, info)));
                    else if (T is IContinuousTransform T_Cont)
                        tileTasks.Add(Task.Run(() => CreateTile(uniqueID, level,
                            T_Cont, info)));
                    else
                        throw new NotImplementedException("Unknown transform type for Tiles");
                }
                /*
                if (tile != null)
                {
                    VisibleTiles.AddTile(level, tile);
                }
#if DEBUG
                TilesToDraw.Add(tile);
#endif
                */
                iLevel--;
                if (iLevel < 0)
                    break;

                level = AvailableLevels[iLevel];
            }

            return tileTasks;
        }

        private TileViewModel CreateTile(string uniqueID, int roundedScaledDownsample, in IContinuousTransform cTransform, in TileTransformInfo info)
        {
            PositionNormalTextureVertex[] verticies = TileViewModel.CalculateVerticies(cTransform, info, out int[] triangulation);
            return CreateTile(uniqueID, roundedScaledDownsample, verticies, triangulation, info);
        }

        private TileViewModel CreateTile(string uniqueID, int roundedScaledDownsample, in IControlPointTriangulation ctrlTriangulation, in TileTransformInfo info)
        {
            //First create a new tile
            //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
            PositionNormalTextureVertex[] verticies = TileViewModel.CalculateVerticies(ctrlTriangulation, info);
            return CreateTile(uniqueID, roundedScaledDownsample, verticies, ctrlTriangulation.TriangleIndicies, info);
        }

        private TileViewModel CreateTile(string uniqueID,
            int roundedScaledDownsample,
            in PositionNormalTextureVertex[] verticies,
            in int[] triangulation,
            in TileTransformInfo info)
        {
            string name = TileFileName(info.TileFileName, roundedScaledDownsample);
            //First create a new tile
            //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample); 
            int mipMapLevels = roundedScaledDownsample == this.AvailableLevels[AvailableLevels.Length - 1] ? 0 : 1; //0 = Generate mipmaps for lowest res texture, 1 == no MipMaps for higher res textures in the pyramid

            var tile = Global.TileCache.ConstructTile(uniqueID,
                verticies,
                triangulation,
                $"{TilePath}/{name}",
                name,
                //PORT TextureCacheFileName,
                this.Name,
                roundedScaledDownsample,
                mipMapLevels);

            //Check for tiles at higher resolution
            //                        int iTempX = iX / 2;
            //                        int iTempY = iY / 2;
            //                        int iTempDownsample = roundedDownsample * 2;
            return tile;

        }
    }
}
