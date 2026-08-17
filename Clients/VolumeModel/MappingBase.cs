using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Resolves the stos used to place a section in volume space. Annotation hit-test uses this,
    /// not MappingManager — MappingManager also picks tiles.
    /// </summary>
    public interface IVolumeTransformProvider
    {
        IVolumeToSectionTransform GetSectionToVolumeTransform(int SectionNumber);
    }

    /// <summary>
    /// Mosaic/section ↔ volume. Mosaic-only mappings are identity. Stos ITransform.Transform
    /// is section→volume (mapped→control); InverseTransform is volume→section.
    /// </summary>
    public interface IVolumeToSectionTransform
    {
        /// <summary>
        /// Stable per mapping instance. Views use this to skip recomputing positions when the transform has not changed.
        /// </summary>
        long ID
        {
            get;
        }

        /// <summary>Mosaic/section → volume. False when the point is outside a discrete stos hull.</summary>
        bool TrySectionToVolume(Vector2 P, out Vector2 transformedP);

        /// <summary>Volume → mosaic/section. False when the point is outside a discrete stos hull.</summary>
        bool TryVolumeToSection(Vector2 P, out Vector2 transformedP);

        /// <summary>Per-point mosaic/section → volume. Output array is always allocated; use the bools.</summary>
        bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP);

        /// <summary>Per-point volume → mosaic/section. Output array is always allocated; use the bools.</summary>
        bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP);

        /// <summary>Mosaic/section → volume. Throws if the point cannot be mapped; prefer Try* for hull edges.</summary>
        Vector2 SectionToVolume(Vector2 P);

        /// <summary>Volume → mosaic/section. Throws if the point cannot be mapped; prefer Try* for hull edges.</summary>
        Vector2 VolumeToSection(Vector2 P);

        Vector2[] SectionToVolume(Vector2[] Points);

        Vector2[] VolumeToSection(Vector2[] Points);

        /// <summary>
        /// Mosaic/section-space bounds. Null for a continuous transform with no hull.
        /// </summary>
        Rectangle? SectionBounds { get; }

        /// <summary>
        /// Volume-space bounds. Null for a continuous transform with no hull.
        /// </summary>
        Rectangle? VolumeBounds { get; }

    }

    /// <summary>
    /// Tiles for one section plus mosaic↔volume. Tileset mappings are Initialized immediately;
    /// pyramid+stos mappings stay false until Initialize completes — DrawTiles must start that work.
    /// </summary>
    public abstract class MappingBase(Section section, string name, string Prefix, string Postfix) : IVolumeToSectionTransform
    {
        /// <summary>
        /// This records the modified date of the file the transform was loaded from
        /// </summary>
        public DateTime LastModified => _LastModified;

        protected DateTime _LastModified = DateTime.MinValue;

        public readonly string Name = name;

        /// <summary>
        /// This is the name, based on the "name" tag in the XML, which should be unique from all other MappingBase objects
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Section != null)
                return Section.ToString() + " " + Name;

            return Name;
        }

        /// <summary>
        /// Contains the URI or directory which all tiles in the mapping base reside in
        /// </summary>
        public string TilePath => this.Section.Path;

        /// <summary>
        /// Prefix to prepend to all tile file names
        /// </summary>
        internal readonly string TilePrefix = Prefix;

        /// <summary>
        /// Postfix to append to all tile file names
        /// </summary>
        internal readonly string TilePostfix = Postfix;

        /// <summary>
        /// Bounds used to fit the camera. Volume-space after a stos warp (SectionToVolumeMapping);
        /// mosaic grid for tilesets, including TileGridToVolumeMapping which does not recompute this.
        /// </summary>
        public abstract Rectangle ControlBounds
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

        protected UnitsAndScale.IAxisUnits _XYScale;
        public virtual UnitsAndScale.IAxisUnits XYScale => _XYScale;

        /// <summary>
        /// Adjust a viewer downsample level to match the difference between the scale used in the pyramid/mapping and the maximum resolution scale for the volume
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        protected virtual double AdjustDownsampleForScale(double input)
        {
            if (this.XYScale is null)
                return input;

            if (this.Section.XYScale is null)
                return input;

            double relative_scale = this.XYScale.Value / this.Section.XYScale.Value;
            return input / relative_scale;
        }

        /// <summary>
        /// Returns the nearest available downsample level with a higher resolution than the viewers downsample.  Override when the section does not use the maximum X/Y Resolution of the volume.
        /// </summary>
        /// <param name="DownsampleLevel"></param>
        /// <returns>MaxVal if there is no level available, otherwise the nearest value</returns>
        public virtual int NearestAvailableLevel(double requestedLevel)
        {
            if (AvailableLevels.Length == 0)
                return int.MaxValue;

            if (double.IsInfinity(requestedLevel))
            {
                //Return the largest downsample value we have
                return AvailableLevels[AvailableLevels.Length - 1];
            }
            else
            {
                double scaledRequestedLevel = AdjustDownsampleForScale(requestedLevel);

                int roundedRequest = (int)Math.Floor(scaledRequestedLevel);
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
        public virtual Task FreeMemory() => Task.CompletedTask;

        /// <summary>
        /// The section to which the mapping applies
        /// </summary>
        protected readonly Section Section = section;

        /// <summary>
        /// Loads mosaic/stos math. Tileset mappings no-op and report Initialized immediately.
        /// SectionSceneRenderer starts this; skipping it leaves DrawTiles returning every frame.
        /// </summary>
        public abstract Task Initialize(CancellationToken token);

        /// <summary>
        /// True when VisibleTiles and ControlBounds are usable. False is not an error — call Initialize.
        /// </summary>
        public abstract bool Initialized { get; }

        /// <summary>
        /// Maps the provided visible bounds in volume space back to section space with the provided transform.
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="VisibleBounds"></param>
        /// <returns></returns>
        protected List<MappingVector2> VisibleBoundsCorners(Rectangle VisibleBounds)
        {
            Vector2[] volumeRectCorners = [   VisibleBounds.LowerLeft,
                                                                    VisibleBounds.LowerRight,
                                                                    VisibleBounds.UpperLeft,
                                                                    VisibleBounds.UpperRight ];
            var mapped = TryVolumeToSection(volumeRectCorners, out var mosaicRectCorners);

            List<MappingVector2> mappedMosaicCorners = [.. mosaicRectCorners.Select((p, i) => new MappingVector2(volumeRectCorners[i], mosaicRectCorners[i])).Where((p, i) => mapped[i])];
            return mappedMosaicCorners;
        }

        /// <summary>
        /// Tiles overlapping the camera. VisibleBounds is volume/world space (Scene.VisibleWorldBounds), not mosaic.
        /// </summary>
        public abstract TilePyramid VisibleTiles(Rectangle VisibleBounds,
                                                 double DownSample
                                                 );

        /// <summary>
        /// Returns a set of tiles which should be rendered in the order returned
        /// </summary>
        /// <param name="VisibleBounds">Visible region of the section</param>
        /// <returns></returns>
        public virtual System.Threading.Tasks.Task<TilePyramid> VisibleTilesAsync(Rectangle VisibleBounds,
                                                 double DownSample
                                                 ) => System.Threading.Tasks.Task<TilePyramid>.Run(() => VisibleTiles(VisibleBounds, DownSample));


        public Vector2 SectionToVolume(Vector2 P)
        {
            return TrySectionToVolume(P, out Vector2 transformedP)
                ? transformedP
                : throw new ArgumentException("Could not map section point to volume");
        }

        public Vector2 VolumeToSection(Vector2 P)
        {
            return TryVolumeToSection(P, out Vector2 transformedP)
                ? transformedP
                : throw new ArgumentException("Could not map volume point to section");
        }

        public abstract Vector2[] SectionToVolume(Vector2[] P);
        /*
        public Vector2[] SectionToVolume(Vector2[] P)
        {
            Vector2[] transformedP;
            bool Success = TrySectionToVolume(P, out transformedP);
            if (!Success)
                throw new ArgumentException("Could not map section point to volume");

            return transformedP;
        }
        */
        public abstract Vector2[] VolumeToSection(Vector2[] P);

        /*

        public Vector2[] VolumeToSection(Vector2[] P)
        {
            Vector2[] transformedP;
            bool Success = TryVolumeToSection(P, out transformedP);
            if (!Success)
                throw new ArgumentException("Could not map volume point to section");

            return transformedP;
        }
        */

        /*private long _ID = new int?();

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
        */

        private readonly long _ID = Interlocked.Increment(ref _NextID);
        private static long _NextID = 0;
        public long ID => _ID;

        public abstract Rectangle? SectionBounds { get; }
        public abstract Rectangle? VolumeBounds { get; }

        public abstract bool TrySectionToVolume(Vector2 P, out Vector2 transformedP);

        public abstract bool TryVolumeToSection(Vector2 P, out Vector2 transformedP);

        public abstract bool[] TrySectionToVolume(in Vector2[] Points, out Vector2[] transformedP);

        public abstract bool[] TryVolumeToSection(in Vector2[] Points, out Vector2[] transformedP);
    }
}