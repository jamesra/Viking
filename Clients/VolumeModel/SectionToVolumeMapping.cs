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
    /// This class represents the warped version of a single section into volume space by passing a .mosaic transform through a slice-to-volume transform
    /// </summary>
    /// 
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
        /// .mosaic files load as being warped.  Volume sections have to passed through a volume transform first, which we do in a lazy fashion
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

        public override string CachedTransformsFileName => System.IO.Path.Combine(Section.volume.Paths.LocalVolumeDir, VolumeTransform.ToString() + "_stos.cache");

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
        /// If this section has not yet been warped, then do so.
        /// This method is invoked by threads.  
        /// </summary>
        public async Task<ITransform[]> WarpTransforms(CancellationToken token)
        {
            if (VolumeTransform != null)
                Trace.WriteLine("Warping section " + VolumeTransform.ToString() +/*.Info.MappedSection + */  " to volume space", "VolumeModel");

            Debug.Assert(this.VolumeTransform != null);

            if (SourceMapping.Initialized == false)
                await SourceMapping.Initialize(token).ConfigureAwait(false);

            var VolumeTransformInfo = ((ITransformInfo)VolumeTransform).Info;

            FileInfo cacheFileInfo = new(CachedTransformsFileName);
            if (cacheFileInfo.Exists)
            {
                /*Check to make sure cache file is older than both .stos modified time and mapping modified time*/
                if (cacheFileInfo.LastWriteTimeUtc >= VolumeTransformInfo.LastModified &&
                    cacheFileInfo.LastWriteTimeUtc >= SourceMapping.LastModified)
                {
                    var cachedTransforms = LoadFromCache();
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
                    catch (System.IO.IOException except)
                    {
                        Trace.WriteLine("Could not delete invalid cache file: " + this.CachedTransformsFileName);
                    }
                }
            }

            // Get the transform tiles from the source mapping, which loads the .mosaic if it hasn't alredy been loaded
            ITransform[] volTransforms = await SourceMapping.GetOrCreateTransforms(token).ConfigureAwait(false);
            if (token.IsCancellationRequested)
                return null;

            // We add transforms which surivive addition with at least three points to this list
            List<ITransform> listTiles = new(volTransforms.Length);

            for (int i = 0; i < volTransforms.Length; i++)
            {
                IControlPointTriangulation T = volTransforms[i] as IControlPointTriangulation;
                //TriangulationTransform copy = (TriangulationTransform)T.Copy();
                ITransform newTransform = null; // = (TriangulationTransform)T.Copy();


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
                    continue;

                //Don't include the tile if the mapped version doesn't have any triangles
                if (newTransform is IControlPointTriangulation cpt)
                {
                    if (cpt.MapPoints.Length > 2)
                        listTiles.Add(newTransform);
                }

                if (T is IMemoryMinimization mmt)
                {
                    mmt.MinimizeMemory();
                }

                if (newTransform is IMemoryMinimization nmmt)
                {
                    nmmt.MinimizeMemory();
                }
            }

            var result = listTiles.ToArray();
            //Try to save the transform to our cache
            SaveToCache(CachedTransformsFileName, [.. listTiles]);

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
