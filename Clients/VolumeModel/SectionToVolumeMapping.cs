using Geometry;
using Geometry.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// This class represents the warped version of a single section into volume space by passing a .mosaic transform through a slice-to-volume transform
    /// </summary>
    /// 
    public class SectionToVolumeMapping : FixedTileCountMapping
    {  
        protected ITransform[] _TileTransforms = null;

        private GridRectangle _VolumeBounds;
        public override GridRectangle ControlBounds { get => _VolumeBounds; }

        private GridRectangle _SectionBounds;
        public override GridRectangle? SectionBounds { get => _SectionBounds; }

        public override GridRectangle? VolumeBounds => _VolumeBounds;


        public override ITransform[] GetLoadedTransformsOrNull()
        {
            if (HasBeenWarped)
                return _TileTransforms;

            return null;
        }

        public async override  Task<ITransform[]> GetOrCreateTransforms(CancellationToken token)
        {
            if (Interlocked.CompareExchange(ref _TileTransforms, _TileTransforms, null) is null)
            {
                await Initialize(token);
            }

            var _transforms = Interlocked.CompareExchange(ref _TileTransforms, _TileTransforms, null);
            if (_transforms is null)
                _transforms = Array.Empty<ITransform>();

            return _transforms;
        }
        
        /// <summary>
        /// .mosaic files load as being warped.  Volume sections have to passed through a volume transform first, which we do in a lazy fashion
        /// </summary>
        private bool HasBeenWarped => _Initialized > 0;

        private long _Initialized = 0;
        public override bool Initialized => Interlocked.Read(ref _Initialized) > 0;

        private long _InitializationInProgress = 0;
        private bool InitializationInProgress => Interlocked.Read(ref _InitializationInProgress) > 0;

        private readonly SemaphoreSlim _InitializeSemaphore = new SemaphoreSlim(1);


        public override async Task Initialize(CancellationToken token)
        {
            if (Initialized || InitializationInProgress)
                return;

            try
            {
                await _InitializeSemaphore.WaitAsync(token);
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
                    Interlocked.CompareExchange(ref _InitializationInProgress, 0, 1);
                }
            }
            finally
            {
                _InitializeSemaphore.Release();
            }
        }

        /// <summary>
        /// The transforms applied to each tile for this section, used to generate verticies. 
        /// If the HasBeenWarped is == false these transforms are in section space and not volume space
        /// </summary>
        private readonly FixedTileCountMapping SourceMapping;

        /// <summary>
        /// The transformation which will/has converted the tiles from section space into volume space.
        /// This can be null if this section is not warped into volume space. 
        /// </summary>
        public readonly ITransform VolumeTransform;

        public override string CachedTransformsFileName
        {
            get { return System.IO.Path.Combine(Section.volume.Paths.LocalVolumeDir, VolumeTransform.ToString() + "_stos.cache"); }
        }

        public SectionToVolumeMapping(Section section, string name, FixedTileCountMapping sourceMapping, ITransform volumeTransform)
            : base(section, name, sourceMapping.TilePrefix, sourceMapping.TilePostfix)
        { 
            SourceMapping = sourceMapping;
            VolumeTransform = volumeTransform;
        }

        public override async Task FreeMemory()
        {
            try
            {
                await _InitializeSemaphore.WaitAsync();
                if (Interlocked.CompareExchange(ref _Initialized, 0, 1) > 0)
                {
                    _TileTransforms = null;
                    await SourceMapping.FreeMemory();
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

            if(SourceMapping.Initialized == false)
                await SourceMapping.Initialize(token);

            var VolumeTransformInfo = ((ITransformInfo)VolumeTransform).Info;

            bool LoadedFromCache = false;
            var cacheFileInfo = new System.IO.FileInfo(CachedTransformsFileName);
            if (cacheFileInfo.Exists)
            {
                /*Check to make sure cache file is older than both .stos modified time and mapping modified time*/  
                if (cacheFileInfo.LastWriteTimeUtc >= VolumeTransformInfo.LastModified &&
                    cacheFileInfo.LastWriteTimeUtc >= SourceMapping.LastModified)
                {
                    try
                    {
                        return LoadFromCache();
                    }
                    catch (Exception)
                    {
                        //On any error, use the traditional path
                        this._TileTransforms = null;
                        LoadedFromCache = false;
                        Trace.WriteLine("Deleting invalid cache file: " + this.CachedTransformsFileName);
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
            ITransform[] volTransforms = await SourceMapping.GetOrCreateTransforms(token);
            if (token.IsCancellationRequested)
                return null;

            // We add transforms which surivive addition with at least three points to this list
            List<ITransform> listTiles = new List<ITransform>(volTransforms.Length);

            for (int i = 0; i < volTransforms.Length; i++)
            {
                IControlPointTriangulation T = volTransforms[i] as IControlPointTriangulation;
                //TriangulationTransform copy = (TriangulationTransform)T.Copy();
                ITransform newTransform = null; // = (TriangulationTransform)T.Copy();


                if (VolumeTransform != null && T != null)
                {

                    TileTransformInfo originalInfo = ((ITransformInfo)T).Info as TileTransformInfo;
                    TileTransformInfo info = new TileTransformInfo(originalInfo.TileFileName,
                                                                   originalInfo.TileNumber,
                                                                   originalInfo.LastModified < VolumeTransformInfo.LastModified ? originalInfo.LastModified : VolumeTransformInfo.LastModified,
                                                                   originalInfo.ImageWidth,
                                                                   originalInfo.ImageHeight);
                    //FIXME
                    newTransform = TriangulationTransform.Transform(this.VolumeTransform, T, info);
                }

                if (newTransform == null)
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
            Task.Run(() => SaveToCache(CachedTransformsFileName, listTiles.ToArray()));

            //OK, overwrite the tiles in our class
            return result;  
        }

         
        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP)
        {
            return this.VolumeTransform.TryInverseTransform(P, out transformedP);
        }

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP)
        {
            return this.VolumeTransform.TryTransform(P, out transformedP);
        }

        public override GridVector2[] SectionToVolume(GridVector2[] P)
        {
            return this.VolumeTransform.Transform(P);
        }

        public override GridVector2[] VolumeToSection(GridVector2[] P)
        {
            return this.VolumeTransform.InverseTransform(P);
        }

        /// <summary>
        /// Maps a point from volume space into the section space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TryVolumeToSection(in GridVector2[] P, out GridVector2[] transformedP)
        {
            return this.VolumeTransform.TryInverseTransform(P, out transformedP);
        }

        /// <summary>
        /// Maps a point from section space into the volume space
        /// </summary>
        /// <param name="?"></param>
        /// <returns></returns>
        public override bool[] TrySectionToVolume(in GridVector2[] P, out GridVector2[] transformedP)
        {
            return this.VolumeTransform.TryTransform(P, out transformedP);
        }

        public override TilePyramid VisibleTiles(in GridRectangle VisibleBounds, double DownSample)
        {
            if (VolumeTransform != null)
            {
                GridQuad? VisibleQuad = default;
                //Add any corners of the VisibleBounds that we can transform to the list of points
                List<MappingGridVector2> VisiblePoints = VisibleBoundsCorners(VisibleBounds);
                if (VisiblePoints.Count == 4)
                {
                    VisiblePoints.Sort(new MappingGridVector2SortByMapPoints());
                    VisibleQuad = new GridQuad(VisiblePoints[0].MappedPoint,
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
