using Geometry;
using Geometry.Transforms;
using System;
using System.Collections.Concurrent;
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

        internal string TileFileName(string filename, int DownsampleLevel) =>
            VolumePath.JoinRelative(Section.volume?.Host, CurrentPyramid.Path, DownsampleLevel.ToString("D3"), filename);

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
            catch (Exception)
            {
                transforms = null;
                Trace.WriteLine(string.Format("Unable to load {0} from cache", CachedTransformsFileName));
                System.IO.File.Delete(CachedTransformsFileName);
            }

            return transforms;
        }

        #endregion

        /// <summary>
        /// One tile transform that can contribute tiles, with the world-space rectangle that decides whether it is visible.
        /// </summary>
        /// <param name="transform">The tile transform the entry was built from.</param>
        /// <param name="info">The tile's image information. Never null.</param>
        /// <param name="bounds">
        /// For a triangulation transform, its <see cref="ITransformControlPoints.ControlBounds"/>. For a continuous transform,
        /// the bounding box of its four mapped image corners.
        /// </param>
        private sealed class TileIndexEntry(ITransform transform, TileTransformInfo info, Rectangle bounds)
        {
            public readonly ITransform Transform = transform;
            public readonly TileTransformInfo Info = info;
            public readonly Rectangle Bounds = bounds;

            /// <summary>Non-null when the tile is built from the transform's control point triangulation, which is preferred.</summary>
            public readonly IControlPointTriangulation Triangulation = transform as IControlPointTriangulation;

            /// <summary>Used to build the tile when <see cref="Triangulation"/> is null.</summary>
            public readonly IContinuousTransform Continuous = transform as IContinuousTransform;
        }

        /// <summary>
        /// Spatial index over the tile transforms of one loaded transform array, so a frame only looks at tiles near the view.
        /// </summary>
        /// <remarks>
        /// Built once per array instance and immutable afterwards, so any number of threads can query it. The entries record each
        /// transform's bounds at build time. The mapping assumes tile transforms are not changed after they are loaded.
        /// </remarks>
        private sealed class VisibleTileIndex
        {
            /// <summary>The array the index was built from. Compared by reference to detect a reload.</summary>
            public readonly ITransform[] Source;

            private readonly TileIndexEntry[] _Entries;
            private readonly RTree.RTree<int> _Tree = new();

            /// <summary>Entries whose bounds are not finite and so cannot be held by the tree. Always tested directly.</summary>
            private readonly List<int> _Unindexed = [];

            public VisibleTileIndex(ITransform[] source)
            {
                Source = source;
                List<TileIndexEntry> entries = new(source.Length);

                foreach (ITransform T in source)
                {
                    if (T is IContinuousTransform && T is ITransformInfo continuousInfo && continuousInfo.Info is TileTransformInfo imageInfo)
                    {
                        Vector2[] corners =
                        [
                            Vector2.Zero,
                            new(imageInfo.ImageWidth, 0),
                            new(0, imageInfo.ImageHeight),
                            new(imageInfo.ImageWidth, imageInfo.ImageHeight)
                        ];

                        Rectangle target_bbox = T.Transform(corners).BoundingBox();
                        entries.Add(new TileIndexEntry(T, imageInfo, target_bbox));
                    }

                    if (T is IControlPointTriangulation T_Triangulation)
                    {
                        //If this tile has been transformed out of existence then skip it
                        if (T_Triangulation.MapPoints.Length < 3)
                            continue;

                        if (T_Triangulation.TriangleIndicies is null)
                            continue;

                        if (T is ITransformControlPoints T_ControlPoints && T is ITransformInfo T_Info && T_Info.Info is TileTransformInfo info)
                            entries.Add(new TileIndexEntry(T, info, T_ControlPoints.ControlBounds));
                    }
                }

                _Entries = [.. entries];

                for (int i = 0; i < _Entries.Length; i++)
                {
                    Rectangle bounds = _Entries[i].Bounds;
                    if (IsFinite(bounds))
                        _Tree.Add(bounds.ToRTreeRect(0), i);
                    else
                        _Unindexed.Add(i);
                }
            }

            public TileIndexEntry this[int index] => _Entries[index];

            private static bool IsFinite(in Rectangle r) =>
                !(double.IsNaN(r.Left) || double.IsInfinity(r.Left) ||
                  double.IsNaN(r.Right) || double.IsInfinity(r.Right) ||
                  double.IsNaN(r.Bottom) || double.IsInfinity(r.Bottom) ||
                  double.IsNaN(r.Top) || double.IsInfinity(r.Top));

            /// <summary>
            /// Finds the entries visible in <paramref name="visibleBounds"/>.
            /// </summary>
            /// <returns>Indices into this index, in the order the transforms appear in <see cref="Source"/>.</returns>
            public List<int> Query(in Rectangle visibleBounds)
            {
                List<int> hits = [];

                if (double.IsNaN(visibleBounds.Left) || double.IsNaN(visibleBounds.Right) ||
                    double.IsNaN(visibleBounds.Bottom) || double.IsNaN(visibleBounds.Top))
                {
                    for (int i = 0; i < _Entries.Length; i++)
                    {
                        if (visibleBounds.Intersects(_Entries[i].Bounds))
                            hits.Add(i);
                    }

                    return hits;
                }

                foreach (int i in _Tree.Intersects(visibleBounds.ToRTreeRect(0)))
                {
                    if (visibleBounds.Intersects(_Entries[i].Bounds))
                        hits.Add(i);
                }

                foreach (int i in _Unindexed)
                {
                    if (visibleBounds.Intersects(_Entries[i].Bounds))
                        hits.Add(i);
                }

                hits.Sort();
                return hits;
            }
        }

        /// <summary>
        /// Index of the tile transforms for the array last returned by <see cref="GetLoadedTransformsOrNull"/>.
        /// Replaced when that array changes and cleared when the transforms are unloaded.
        /// </summary>
        private volatile VisibleTileIndex _TileIndex;

        private VisibleTileIndex GetTileIndex(ITransform[] transforms)
        {
            VisibleTileIndex index = _TileIndex;
            if (index is not null && ReferenceEquals(index.Source, transforms))
                return index;

            index = new VisibleTileIndex(transforms);
            _TileIndex = index;
            return index;
        }

        /// <summary>
        /// Returns the tiles that are visible and already built. Tiles that are not built yet are built in the background
        /// and appear in a later call.
        /// </summary>
        /// <remarks>
        /// Safe to call from several threads. A tile whose build an earlier call started is not started again;
        /// <see cref="WhenPendingTilesComplete"/> waits for those builds. A tile the cache holds as null (too few vertices to draw)
        /// is treated as known empty and skipped.
        /// </remarks>
        protected virtual TilePyramid VisibleTiles(Rectangle VisibleBounds,
                                                Quad? SectionVisibleBounds,
                                                double DownSample)
        {
            TilePyramid VisibleTiles = new(VisibleBounds);

            //Get ready by loading a smaller texture in case the user scrolls this direction 
            //Once we have smaller textures then increase the quality
            //            int predictiveDownsample = DownSample * 4 > 64 ? 64 : (int)DownSample * 4;

            int roundedDownsample = NearestAvailableLevel(DownSample);
            int roundedScaledDownsample = NearestAvailableLevel(AdjustDownsampleForScale(DownSample));

            if (roundedDownsample == int.MaxValue || roundedScaledDownsample == int.MaxValue)
                return VisibleTiles;

            //TODO: Need a flag to indicate if transforms are loaded so we can skip
            ITransform[] Tranforms = GetLoadedTransformsOrNull();
            if (Tranforms is null)
            {
                _TileIndex = null;
                return VisibleTiles;
            }

            VisibleTileIndex index = GetTileIndex(Tranforms);

            //AvailableLevels builds a new array on every access, so read it once per call
            int[] levels = AvailableLevels;
            string pyramidName = CurrentPyramid.Name;
            int sectionNumber = Section.Number;

            List<Task<TileViewModel>> buildTasks = null;

            foreach (int iEntry in index.Query(VisibleBounds))
            {
                TileIndexEntry entry = index[iEntry];

                int iLevel = levels.Length - 1;
                int level = levels[iLevel];
                while (level >= roundedDownsample)
                {
                    var uniqueID = TileUniqueKey.Create(sectionNumber, Name, pyramidName, level, entry.Info.TileFileName);

                    if (Global.TileCache.TryGetTile(uniqueID, out TileViewModel tileViewModel))
                    {
                        if (tileViewModel is not null)
                            VisibleTiles.AddTile(tileViewModel.Downsample, tileViewModel);
                    }
                    else
                    {
                        buildTasks ??= [];
                        buildTasks.Add(GetOrStartTileBuild(uniqueID, level, entry));
                    }

                    iLevel--;
                    if (iLevel < 0)
                        break;

                    level = levels[iLevel];
                }
            }

            // Only include tiles already completed.
            // Background CreateTile tasks continue asynchronously; they will populate
            // Global.TileCache and appear on the next draw cycle.
            if (buildTasks is not null)
            {
                foreach (var task in buildTasks)
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        var tile = task.Result;
                        if (tile is not null)
                            VisibleTiles.AddTile(tile.Downsample, tile);
                    }
                }
            }

            return VisibleTiles;
        }

        /// <summary>
        /// Tile builds that have been started and have not finished, by tile. A build adds its tile to
        /// <see cref="Global.TileCache"/> before its task completes, and removes itself from here afterwards.
        /// Keeps repeated <see cref="VisibleTiles(Rectangle, Quad?, double)"/> calls from starting a second build of a tile,
        /// and is what <see cref="WhenPendingTilesComplete"/> waits on.
        /// </summary>
        private readonly ConcurrentDictionary<TileUniqueKey, Task<TileViewModel>> _InFlightTiles = new();

        public override Task WhenPendingTilesComplete() => Task.WhenAll(_InFlightTiles.Values);

        /// <summary>
        /// Returns the build already running for a tile, or starts one on the thread pool.
        /// </summary>
        /// <remarks>
        /// Callers have just missed the cache. The cache is checked again here because the build may have finished, and removed
        /// itself from <see cref="_InFlightTiles"/>, between the caller's miss and this call.
        /// </remarks>
        private Task<TileViewModel> GetOrStartTileBuild(TileUniqueKey uniqueID, int level, TileIndexEntry entry)
        {
            while (true)
            {
                if (_InFlightTiles.TryGetValue(uniqueID, out Task<TileViewModel> running))
                    return running;

                if (Global.TileCache.TryGetTile(uniqueID, out TileViewModel cached))
                    return Task.FromResult(cached);

                IControlPointTriangulation triangulation = entry.Triangulation;
                IContinuousTransform continuous = entry.Continuous;
                TileTransformInfo info = entry.Info;

                if (triangulation is null && continuous is null)
                    throw new NotImplementedException("Unknown transform type for Tiles");

                Task<TileViewModel> build = new(() => triangulation is not null
                    ? CreateTile(uniqueID, level, triangulation, info)
                    : CreateTile(uniqueID, level, continuous, info));

                if (!_InFlightTiles.TryAdd(uniqueID, build))
                    continue;

                build.ContinueWith(_ => _InFlightTiles.TryRemove(uniqueID, out _), TaskContinuationOptions.ExecuteSynchronously);
                build.Start(TaskScheduler.Default);
                return build;
            }
        }

        private TileViewModel CreateTile(TileUniqueKey uniqueID, int roundedScaledDownsample, in IContinuousTransform cTransform, in TileTransformInfo info)
        {
            PositionNormalTextureVertex[] verticies = TileViewModel.CalculateVerticies(cTransform, info, out int[] triangulation);
            return CreateTile(uniqueID, roundedScaledDownsample, verticies, triangulation, info);
        }

        private TileViewModel CreateTile(TileUniqueKey uniqueID, int roundedScaledDownsample, in IControlPointTriangulation ctrlTriangulation, in TileTransformInfo info)
        {
            //First create a new tile
            //PORT: string TextureCacheFileName = TileCacheName(iX, iY, roundedDownsample);
            PositionNormalTextureVertex[] verticies = TileViewModel.CalculateVerticies(ctrlTriangulation, info);
            return CreateTile(uniqueID, roundedScaledDownsample, verticies, ctrlTriangulation.TriangleIndicies, info);
        }

        private TileViewModel CreateTile(TileUniqueKey uniqueID,
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
