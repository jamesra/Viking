using Geometry;
using Geometry.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Pyramid tiles: mosaic stos composed with a volume stos. Initialized is false until Initialize/Warp finishes.
    /// </summary>
    public class SectionToVolumeMapping(Section section, string name, FixedTileCountMapping sourceMapping, ITransform volumeTransform) : FixedTileCountMapping(section, name, sourceMapping.TilePrefix, sourceMapping.TilePostfix)
    {
        protected ITransform[] _TileTransforms = null;

        private Rectangle _VolumeBounds;
        public override Rectangle ControlBounds => _VolumeBounds;

        private Rectangle _SectionBounds;
        public override Rectangle? SectionBounds => _SectionBounds;

        public override Rectangle? VolumeBounds => _VolumeBounds;


        public override ITransform[] GetLoadedTransformsOrNull()
        {
            if (HasBeenWarped)
                return _TileTransforms;

            return null;
        }

        public override async Task<ITransform[]> GetOrCreateTransforms(CancellationToken token)
        {
            //if (HasBeenWarped == false)
            //throw new InvalidOperationException($"Mapping is not initialized");

            //return _TileTransforms;

            if (Interlocked.CompareExchange(ref _TileTransforms, _TileTransforms, null) is null)
            {
                await Initialize(token).ConfigureAwait(false);
            }

            var _transforms = Interlocked.CompareExchange(ref _TileTransforms, _TileTransforms, null) ?? [];
            return _transforms;
            /*
            try
            { 
                if (_TileTransforms is null || token.IsCancellationRequested)
                    return Array.Empty<ITransform>();

                return _TileTransforms;
            }
            finally
            {
                //rwLockObj.ExitReadLock();
            }
            */
        }
        /*
        public override ITransform[] TileTransforms
        {
            get
            {
                if (HasBeenWarped == false)
                    Warp();

                return _TileTransforms;
            }
        }*/

        /// <summary>
        /// Mosaic mappings report loaded as soon as the .mosaic is parsed. This type stays false until WarpTransforms runs.
        /// </summary>
        private bool HasBeenWarped => _Initialized > 0;

        private long _Initialized = 0;
        public override bool Initialized => Interlocked.Read(ref _Initialized) > 0;

        private long _InitializationInProgress = 0;
        private bool InitializationInProgress => Interlocked.Read(ref _InitializationInProgress) > 0;

        private readonly SemaphoreSlim _InitializeSemaphore = new(1);


        public override async Task Initialize(CancellationToken token)
        {
            if (Initialized || InitializationInProgress)
                return;

            try
            {
                await _InitializeSemaphore.WaitAsync(token).ConfigureAwait(false);
                if (Interlocked.Read(ref _Initialized) > 0)
                    return;

                Interlocked.Exchange(ref _InitializationInProgress, 1);

                _TileTransforms = await WarpTransforms(token).ConfigureAwait(false);

                if (_TileTransforms != null)
                {
                    var transformControlPoints = _TileTransforms.Cast<ITransformControlPoints>().ToArray();
                    _VolumeBounds =
                        Geometry.Transforms.ReferencePointBasedTransform.CalculateControlBounds(transformControlPoints);
                    _SectionBounds =
                        Geometry.Transforms.ReferencePointBasedTransform.CalculateMappedBounds(transformControlPoints);
                    Interlocked.Exchange(ref _Initialized, 1);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _InitializationInProgress, 0);
                _InitializeSemaphore.Release();

            }
        }

        /// <summary>
        /// The transforms applied to each tile for this section, used to generate verticies. 
        /// If the HasBeenWarped is == false these transforms are in section space and not volume space
        /// </summary>
        private readonly FixedTileCountMapping SourceMapping = sourceMapping;

        /// <summary>
        /// The transformation which will/has converted the tiles from section space into volume space.
        /// This can be null if this section is not warped into volume space. 
        /// </summary>
        public readonly ITransform VolumeTransform = volumeTransform;

        /// <summary>
        /// Warped-tile cache file. Named by the mapping (volume transform group, section and mosaic transform) as well as the
        /// stos pair: two stos groups map the same section pair, and their warped tiles must not share a file.
        /// </summary>
        public override string CachedTransformsFileName => System.IO.Path.Combine(Section.volume.Paths.LocalVolumeDir,
            SafeFileName(Name) + " " + VolumeTransform.ToString() + "_stos.cache");

        private static string SafeFileName(string name)
        {
            char[] chars = (name ?? string.Empty).ToCharArray();
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                    chars[i] = '_';
            }
            return new string(chars);
        }

        public override async Task FreeMemory()
        {
            try
            {
                await _InitializeSemaphore.WaitAsync().ConfigureAwait(false);
                if (Interlocked.CompareExchange(ref _Initialized, 0, 1) > 0)
                {
                    _TileTransforms = null;
                    await SourceMapping.FreeMemory().ConfigureAwait(false);
                }
            }
            finally
            {
                _InitializeSemaphore.Release();
            }

            return;
        }

        /// <summary>
        /// Warps one mosaic tile into volume space. Returns null when the tile does not survive (no overlap with the volume
        /// transform, or fewer than three points left). Called from a parallel loop, one tile per call.
        /// </summary>
        private ITransform WarpTile(ITransform tile, TransformBasicInfo VolumeTransformInfo)
        {
            IControlPointTriangulation T = tile as IControlPointTriangulation;
            ITransform newTransform = null;

            if (VolumeTransform != null && T != null)
            {
                TileTransformInfo originalInfo = ((ITransformInfo)T).Info as TileTransformInfo;
                TileTransformInfo info = new(originalInfo.TileFileName,
                                                               originalInfo.TileNumber,
                                                               originalInfo.LastModified < VolumeTransformInfo.LastModified ? originalInfo.LastModified : VolumeTransformInfo.LastModified,
                                                               originalInfo.ImageWidth,
                                                               originalInfo.ImageHeight);
                //FIXME
                newTransform = TriangulationTransform.Transform(this.VolumeTransform, T, info);
            }

            if (newTransform is null)
                return null;

            //Don't include the tile if the mapped version doesn't have any triangles
            ITransform kept = newTransform is IControlPointTriangulation cpt && cpt.MapPoints.Length > 2 ? newTransform : null;

            if (T is IMemoryMinimization mmt)
            {
                mmt.MinimizeMemory();
            }

            if (newTransform is IMemoryMinimization nmmt)
            {
                nmmt.MinimizeMemory();
            }

            return kept;
        }

        /// <summary>
        /// If this section has not yet been warped, then do so.
        /// This method is invoked by threads.  
        /// </summary>
        public async Task<ITransform[]> WarpTransforms(CancellationToken token)
        {
            if (VolumeTransform != null)
                Trace.WriteLine("Warping section " + VolumeTransform.ToString() +/*.Info.MappedSection + */  " to volume space", "VolumeModel");

            Debug.Assert(this.VolumeTransform != null);

            string sectionDetail = Section.Number.ToString();

            //Inverse mapping (volume to section) uses the volume transform's RTrees; build them while the mosaic loads.
            _ = (VolumeTransform as ISpatialIndexPrewarm)?.PrewarmSpatialIndexAsync();

            if (SourceMapping.Initialized == false)
            {
                using (LoadStageTimings.Start(LoadStageTimings.MosaicLoad, sectionDetail))
                    await SourceMapping.Initialize(token).ConfigureAwait(false);
            }

            var VolumeTransformInfo = ((ITransformInfo)VolumeTransform).Info;

            FileInfo cacheFileInfo = new(CachedTransformsFileName);
            if (cacheFileInfo.Exists)
            {
                /*Check to make sure cache file is older than both .stos modified time and mapping modified time*/
                if (cacheFileInfo.LastWriteTimeUtc >= VolumeTransformInfo.LastModified &&
                    cacheFileInfo.LastWriteTimeUtc >= SourceMapping.LastModified)
                {
                    ITransform[] cachedTransforms;
                    using (LoadStageTimings.Start(LoadStageTimings.WarpCacheRead, sectionDetail))
                        cachedTransforms = LoadFromCache();
                    if (cachedTransforms != null)
                        return cachedTransforms;

                    // LoadFromCache deletes corrupt entries; fall through to rebuild from source mapping.
                }
                else
                {
                    //Remove the cache file, it is stale
                    Trace.WriteLine("Deleting stale cache file: " + this.CachedTransformsFileName);
                    try
                    {
                        System.IO.File.Delete(this.CachedTransformsFileName);
                    }
                    catch (System.IO.IOException)
                    {
                        Trace.WriteLine("Could not delete invalid cache file: " + this.CachedTransformsFileName);
                    }
                }
            }

            // Get the transform tiles from the source mapping, which loads the .mosaic if it hasn't alredy been loaded
            ITransform[] volTransforms = await SourceMapping.GetOrCreateTransforms(token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return null;

            //Tiles warp independently: each reads the shared volume transform and its own tile transform. Results are
            //stored by index so the tile order matches the mosaic.
            ITransform[] warped = new ITransform[volTransforms.Length];
            var warpStage = LoadStageTimings.Start(LoadStageTimings.Warp, sectionDetail);
            Parallel.For(0, volTransforms.Length, i => warped[i] = WarpTile(volTransforms[i], VolumeTransformInfo));
            warpStage.Dispose();

            // Tiles which survive addition with at least three points
            List<ITransform> listTiles = [.. warped.Where(t => t != null)];

            var result = listTiles.ToArray();
            //Try to save the transform to our cache
            using (LoadStageTimings.Start(LoadStageTimings.WarpCacheWrite, sectionDetail))
                await SaveToCache(CachedTransformsFileName, [.. listTiles]).ConfigureAwait(false);

            //OK, overwrite the tiles in our class
            return result;
        }


        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool TryVolumeToSection(Vector2 P, out Vector2 transformedP) => this.VolumeTransform.TryInverseTransform(P, out transformedP);

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool TrySectionToVolume(Vector2 P, out Vector2 transformedP) => this.VolumeTransform.TryTransform(P, out transformedP);

        public override Vector2[] SectionToVolume(Vector2[] P) => this.VolumeTransform.Transform(P);

        public override Vector2[] VolumeToSection(Vector2[] P) => this.VolumeTransform.InverseTransform(P);

        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TryVolumeToSection(in Vector2[] P, out Vector2[] transformedP) => this.VolumeTransform.TryInverseTransform(P, out transformedP);

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(in Vector2[] P, out Vector2[] transformedP) => this.VolumeTransform.TryTransform(P, out transformedP);

        public override TilePyramid VisibleTiles(Rectangle VisibleBounds, double DownSample)
        {
            if (VolumeTransform != null)
            {
                Quad? VisibleQuad = default;
                //Add any corners of the VisibleBounds that we can transform to the list of points
                List<MappingVector2> VisiblePoints = VisibleBoundsCorners(VisibleBounds);
                if (VisiblePoints.Count == 4)
                {
                    VisiblePoints.Sort(new MappingVector2SortByMapPoints());
                    VisibleQuad = new Quad(VisiblePoints[0].MappedPoint,
                                               VisiblePoints[1].MappedPoint,
                                               VisiblePoints[2].MappedPoint,
                                               VisiblePoints[3].MappedPoint);
                }

                return VisibleTiles(VisibleBounds, VisibleQuad, DownSample);
            }
            else
            {
                return new TilePyramid(VisibleBounds);
            }
        }
    }
}
