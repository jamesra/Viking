using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Geometry;
using Geometry.Transforms; 
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Viking.VolumeModel
{
    /// <summary>
    /// This class represents the warped version of a single section using a transform
    /// </summary>
    /// 
    public class SectionToVolumeMapping : FixedTileCountMapping
    {
        private object LockObj = new object(); 
        
        /// <summary>
        /// If this section is in a volume ReferencedTo indicates which section
        /// was used as a control during slice-to-slice registration.
        /// Returns -1 if the section is not in a volume
        /// </summary>
        public int ReferencedTo
        {
            get { return -1; }
        }

        public override ReferencePointBasedTransform[] TileTransforms
        {
            get
            {
                if (HasBeenWarped == false)
                    Warp();

                return _TileTransforms;
            }
        }

        /// <summary>
        /// .mosaic files load as being warped.  Volume sections have to passed through a volume transform first, which we do in a lazy fashion
        /// </summary>
        protected bool HasBeenWarped = false;

        /// <summary>
        /// The transforms applied to each tile for this section, used to generate verticies. 
        /// If the HasBeenWarped is == false these transforms are in section space and not volume space
        /// </summary>
        public FixedTileCountMapping SourceMapping;

        /// <summary>
        /// The transformation which will/has converted the tiles from section space into volume space.
        /// This can be null if this section is not warped into volume space. 
        /// </summary>
        public readonly TriangulationTransform VolumeTransform;

        public override string CachedTransformsFileName
        {
            get { return Section.volume.LocalCachePath + 
                System.IO.Path.DirectorySeparatorChar + 
                Section.volume.Name + 
                System.IO.Path.DirectorySeparatorChar + 
                VolumeTransform.ToString() + "_stos.cache"; }
        }

        public SectionToVolumeMapping(Section section, string name,  FixedTileCountMapping sourceMapping, TriangulationTransform volumeTransform) 
            : base(section, name, sourceMapping.TilePrefix, sourceMapping.TilePostfix )
        {
            HasBeenWarped = false;
            SourceMapping = sourceMapping;
            VolumeTransform = volumeTransform;            
        }

        public override void FreeMemory()
        {
            lock (LockObj)
            {
                HasBeenWarped = false;
                _TileTransforms = null;
                SourceMapping.FreeMemory();
            }
        }

        public void Warp(Object state)
        {
            Warp();
        }

        /// <summary>
        /// If this section has not yet been warped, then do so.
        /// This method is invoked by threads.  
        /// </summary>
        public void Warp()
        {
            if (HasBeenWarped)
                return;

            lock (LockObj)
            {
                if (HasBeenWarped)
                    return;

                if (VolumeTransform != null)
                    Trace.WriteLine("Warping section " + VolumeTransform.ToString() +/*.Info.MappedSection + */  " to volume space", "VolumeModel");

                Debug.Assert(this.VolumeTransform != null);

                bool LoadedFromCache = false; 
                if (System.IO.File.Exists(this.CachedTransformsFileName))
                {
                    /*Check to make sure cache file is older than both .stos modified time and mapping modified time*/
                    DateTime CacheCreationTime = System.IO.File.GetLastWriteTimeUtc(this.CachedTransformsFileName);

                    if (CacheCreationTime >= VolumeTransform.Info.LastModified &&
                        CacheCreationTime >= SourceMapping.LastModified)
                    {
                        try
                        {
                            this._TileTransforms = LoadFromCache(); 
                        }
                        catch (Exception )
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

                        LoadedFromCache = this._TileTransforms != null;
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

                if (LoadedFromCache)
                {
                    this.HasBeenWarped = true;
                    return; 
                }  

                // Get the transform tiles from the source mapping, which loads the .mosaic if it hasn't alredy been loaded
                ReferencePointBasedTransform[] volTransforms = SourceMapping.TileTransforms;

                // We add transforms which surivive addition with at least three points to this list
                List<TriangulationTransform> listTiles = new List<TriangulationTransform>(volTransforms.Length);

                for (int i = 0; i < volTransforms.Length; i++)
                {
                    TriangulationTransform T = volTransforms[i] as TriangulationTransform;
                    //TriangulationTransform copy = (TriangulationTransform)T.Copy();
                    TriangulationTransform newTransform = null; // = (TriangulationTransform)T.Copy();


                    if (VolumeTransform != null)
                    {
                        TileTransformInfo originalInfo = T.Info as TileTransformInfo;
                        TileTransformInfo info = new TileTransformInfo(originalInfo.TileFileName,
                                                                       originalInfo.TileNumber,
                                                                       originalInfo.LastModified < this.VolumeTransform.Info.LastModified ? originalInfo.LastModified : this.VolumeTransform.Info.LastModified,
                                                                       originalInfo.ImageWidth, 
                                                                       originalInfo.ImageHeight);
                        //FIXME
                        newTransform = TriangulationTransform.Transform(this.VolumeTransform as TriangulationTransform, T, info);
                    }

                    if (newTransform == null)
                        continue; 

                    //Don't include the tile if the mapped version doesn't have any triangles
                    if (newTransform.MapPoints.Length > 2)
                        listTiles.Add(newTransform);

                    T.MinimizeMemory();
                    newTransform.MinimizeMemory();
                }

                //OK, overwrite the tiles in our class
                this._TileTransforms = listTiles.ToArray();
                this.HasBeenWarped = true;

                //Try to save the transform to our cache
                SaveToCache(); 
            }
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

        public override TilePyramid VisibleTiles(GridRectangle VisibleBounds, double DownSample)
        {
            if (VolumeTransform != null)
            {
                GridQuad VisibleQuad = null;
                //Add any corners of the VisibleBounds that we can transform to the list of points
                List<MappingGridVector2> VisiblePoints = VisibleBoundsCorners(VolumeTransform, VisibleBounds);
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
