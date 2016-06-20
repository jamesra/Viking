using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Diagnostics;
using System.IO; 
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Viking.VolumeModel
{
    public interface IVolumeTransformProvider
    {
        IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber);
    }

    public interface IVolumeToSectionTransform
    {
        /// <summary>
        /// A unique ID for the transforming object
        /// </summary>
        /// <returns></returns>
        long ID
        {
            get;
        }

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        bool[] TrySectionToVolume(GridVector2[] Points, out GridVector2[] transformedP);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        bool[] TryVolumeToSection(GridVector2[] Points, out GridVector2[] transformedP);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        GridVector2 SectionToVolume(GridVector2 P);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        GridVector2 VolumeToSection(GridVector2 P);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        GridVector2[] SectionToVolume(GridVector2[] Points);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        GridVector2[] VolumeToSection(GridVector2[] Points);


    }

    /// <summary>
    /// Mapping base encapsulates the transforms required to map all tiles in a section to mosaic or volume space
    /// </summary>
    public abstract class MappingBase : IVolumeToSectionTransform
    {
        /// <summary>
        /// This records the modified date of the file the transform was loaded from
        /// </summary>
        public DateTime LastModified
        {
            get
            {
                return _LastModified;
            }
        }

        protected DateTime _LastModified = DateTime.MinValue;

        public readonly string Name;

        /// <summary>
        /// This is the name, based on the "name" tag in the XML, which should be unique from all other MappingBase objects
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if(Section != null)
                return Section.ToString() + " " + Name; 

            return Name; 
        }

        /// <summary>
        /// Contains the URI or directory which all tiles in the mapping base reside in
        /// </summary>
        public string TilePath
        {
            get
            {
                return this.Section.Path;
            }
        }

        /// <summary>
        /// Prefix to prepend to all tile file names
        /// </summary>
        internal readonly string TilePrefix;

        /// <summary>
        /// Postfix to append to all tile file names
        /// </summary>
        internal readonly string TilePostfix;

        public abstract GridRectangle Bounds
        {
            get;
        }

        /// <summary>
        /// A sorted list of available downsample levels
        /// </summary>
        public abstract int[] AvailableLevels
        {
            get;
        }

        /// <summary>
        /// Returns the nearest available downsample level the 
        /// </summary>
        /// <param name="DownsampleLevel"></param>
        /// <returns></returns>
        public int NearestAvailableLevel(double requestedLevel)
        {
            if (double.IsInfinity(requestedLevel))
            {
                //Return the largest downsample value we have
                return AvailableLevels[AvailableLevels.Length - 1]; 
            }
            else
            {
                int roundedRequest = (int)Math.Floor(requestedLevel);
                int[] availableLevels = AvailableLevels;
                //Debug.Assert(LevelToGridInfo.ContainsKey(roundedDownsample));
                //We may not have full-res tiles if we are using multi-resolution data
                if (availableLevels.Contains(roundedRequest))
                    return roundedRequest;

                //Find where this level fits in the list
                int iNextLowestValue = 0;
                for (int iLevel = 0; iLevel < availableLevels.Length; iLevel++)
                {
                    if (availableLevels[iLevel] <= roundedRequest)
                    {
                        iNextLowestValue = iLevel;
                    }
                    else
                        break; //List is sorted, so bail out on >=
                }

                //The variable is a little misleading, if all levels are larger than requested the returned value will be larger than the requested level
                return availableLevels[iNextLowestValue];
            }
        }
        
        
        /// <summary>
        /// Called when there is a need to free the memory used by the object, but keep the object alive
        /// </summary>
        public virtual void FreeMemory()
        {

        }

        /// <summary>
        /// The section to which the mapping applies
        /// </summary>
        protected Section Section; 

        public MappingBase(Section section, string name, string Prefix, string Postfix)
        {
            this.Name = name; 
            this.Section = section;
            this.TilePrefix = Prefix;
            this.TilePostfix = Postfix; 
        }
        
        /// <summary>
        /// Maps the provided visible bounds in volume space back to section space with the provided transform.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="VisibleBounds"></param>
        /// <returns></returns>
        protected List<MappingGridVector2> VisibleBoundsCorners(ITransform transform, GridRectangle VisibleBounds)
        {  
            List<MappingGridVector2> listBoundCorners = new List<MappingGridVector2>(4);
            //Add any corners of the VisibleBounds that we can transform to the list of points
            bool transformSuccess = false;
            GridVector2 TLowerLeft;
            GridVector2 TLowerRight;
            GridVector2 TUpperLeft;
            GridVector2 TUpperRight;


            transformSuccess = transform.TryInverseTransform(VisibleBounds.LowerLeft, out TLowerLeft);
            if(transformSuccess)
                listBoundCorners.Add(new MappingGridVector2(VisibleBounds.LowerLeft, TLowerLeft));
            
            //Add any corners of the VisibleBounds that we can transform to the list of points
            transformSuccess = transform.TryInverseTransform(VisibleBounds.LowerRight, out TLowerRight);
            if(transformSuccess)
                listBoundCorners.Add(new MappingGridVector2(VisibleBounds.LowerRight, TLowerRight));

            transformSuccess = transform.TryInverseTransform(VisibleBounds.UpperLeft, out TUpperLeft);
            if(transformSuccess)
                listBoundCorners.Add(new MappingGridVector2(VisibleBounds.UpperLeft, TUpperLeft));

            transformSuccess = transform.TryInverseTransform(VisibleBounds.UpperRight, out TUpperRight);
            if(transformSuccess)
                listBoundCorners.Add(new MappingGridVector2(VisibleBounds.UpperRight, TUpperRight));
            
            return listBoundCorners;
        }

        /// <summary>
        /// Returns a set of tiles which should be rendered in the order returned
        /// </summary>
        /// <param name="VisibleBounds">Visible region of the section</param>
        /// <returns></returns>
        public abstract TilePyramid VisibleTiles(GridRectangle VisibleBounds,
                                                 double DownSample
                                                 );


        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public GridVector2 SectionToVolume(GridVector2 P)
        {
            GridVector2 transformedP;
            bool Success = TrySectionToVolume(P, out transformedP);
            if (!Success)
                throw new ArgumentException("Could not map section point to volume");

            return transformedP; 
        }

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public GridVector2 VolumeToSection(GridVector2 P)
        {
            GridVector2 transformedP;
            bool Success = TryVolumeToSection(P, out transformedP);
            if (!Success)
                throw new ArgumentException("Could not map volume point to section");

            return transformedP;
        }


        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        /// 
        public abstract GridVector2[] SectionToVolume(GridVector2[] P);
        /*
        public GridVector2[] SectionToVolume(GridVector2[] P)
        {
            GridVector2[] transformedP;
            bool Success = TrySectionToVolume(P, out transformedP);
            if (!Success)
                throw new ArgumentException("Could not map section point to volume");

            return transformedP;
        }
        */
        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        /// 

        public abstract GridVector2[] VolumeToSection(GridVector2[] P);

        /*

        public GridVector2[] VolumeToSection(GridVector2[] P)
        {
            GridVector2[] transformedP;
            bool Success = TryVolumeToSection(P, out transformedP);
            if (!Success)
                throw new ArgumentException("Could not map volume point to section");

            return transformedP;
        }
        */

        private long? _ID = new int?();
        /// <summary>
        /// Return an unique ID for the current transform being used so we can quickly check if we need to recalculate positions
        /// </summary>
        public long ID
        {
            get
            {
                if (!_ID.HasValue)
                {
                    this._ID = (long)this.GetHashCode();
                }

                return _ID.Value;
            }
        }

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public abstract bool TrySectionToVolume(GridVector2 P, out GridVector2 transformedP);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public abstract bool TryVolumeToSection(GridVector2 P, out GridVector2 transformedP);

        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public abstract bool[] TrySectionToVolume(GridVector2[] Points, out GridVector2[] transformedP);


        /// <summary>
        /// Maps the point from the volume to the section if this is overriden by a volume mapping class
        /// </summary>
        /// <param name="P"></param>
        /// <returns></returns>
        public abstract bool[] TryVolumeToSection(GridVector2[] Points, out GridVector2[] transformedP);
    }
}