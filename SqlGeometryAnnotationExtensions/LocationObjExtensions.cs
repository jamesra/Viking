using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;

namespace Viking.VolumeModel
{

    /// <summary>
    /// Sets a location's shapes. Mosaic is the truth and the volume shape is always the forward owner's
    /// (<see cref="LocationVolumeGeometry"/>); see <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    public static class LocationObjExtensions
    {

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromPointsInVolume(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Vector2[] volumePoints, ICollection<Vector2[]> volume_innerRingPoints)
        {
            Vector2[] mosaic_points = mapper.VolumeToSection(volumePoints);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volumePoints, volume_innerRingPoints);
            location.MosaicShape = location.TypeCode.GetShape(mosaic_points, VolumeInnerRingPointsToSection(mapper, volume_innerRingPoints));

            return;
        }

        /// <summary>
        /// Sets a location from a mosaic shape, the truth: <c>MosaicShape</c> is <paramref name="shape"/> and <c>VolumeShape</c>
        /// is what the forward owner (<see cref="LocationVolumeGeometry"/>) derives from it, never a volume shape the caller
        /// kept. Set <c>TypeCode</c> (and <c>Width</c>) first: the volume shape is smoothed for the type.
        /// </summary>
        /// <exception cref="ArgumentException">The shape is not valid SQL geometry, or it does not map onto the volume under
        /// <paramref name="mapper"/>; the location is then unchanged.</exception>
        public static void SetShapeFromGeometryInSection(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            if (!shape.STIsValid().Value)
                throw new ArgumentException("Shape must be valid SQL Geometry " + shape.IsValidDetailed());

            if (LocationVolumeGeometry.MapShape(location.TypeCode, shape, mapper) is null)
                throw new ArgumentException("The shape lies partly outside the region this section's transform maps, so it cannot be drawn on the volume.");

            location.MosaicShape = shape;
            location.VolumeShape = LocationVolumeGeometry.For(location, mapper).Shape;
        }

        /// <summary>
        /// Saves an edit made in volume space after <see cref="LocationVolumeGeometry.ToMosaic(Viking.AnnotationServiceTypes.Interfaces.LocationType, Microsoft.SqlServer.Types.SqlGeometry, IVolumeToSectionTransform, double?)"/>
        /// mapped it back: the mosaic shape (and width) become the truth and the forward owner derives the volume shape
        /// (<see cref="SetShapeFromGeometryInSection"/>). Set <c>TypeCode</c> first.
        /// </summary>
        /// <returns>False, with the location unchanged, when the edit could not be mapped; <see cref="MosaicGeometry.Failure"/> says why.</returns>
        /// <exception cref="ArgumentException">The mapped shape is not valid SQL geometry; the location is then unchanged.</exception>
        public static bool TryApply(this WebAnnotationModel.LocationObj location, MosaicGeometry mosaic, Viking.VolumeModel.IVolumeToSectionTransform mapper)
        {
            if (location is null)
                throw new ArgumentNullException(nameof(location));
            if (mosaic is null)
                throw new ArgumentNullException(nameof(mosaic));

            if (!mosaic.Succeeded)
                return false;

            double? oldWidth = location.Width;
            if (mosaic.Width is double width)
                location.Width = width;

            try
            {
                location.SetShapeFromGeometryInSection(mapper, mosaic.Shape);
            }
            catch (ArgumentException)
            {
                location.Width = oldWidth;
                throw;
            }

            return true;
        }

        /// <summary>
        /// Takes unsmoothed points and sets both the mosaic and volume shape for a locationObj
        /// </summary>
        /// <param name="mapper"></param>
        /// <param name="location"></param>
        /// <param name="volumePoints"></param>
        /// <param name="volume_innerRingPoints"></param>
        public static void SetShapeFromGeometryInVolume(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry volume_shape)
        {
            if (!volume_shape.STIsValid().Value)
                throw new ArgumentException("Shape must be valid SQL Geometry " + volume_shape.IsValidDetailed());

            Microsoft.SqlServer.Types.SqlGeometry mosaic_shape = mapper.TryMapShapeVolumeToSection(volume_shape);

            location.VolumeShape = location.TypeCode.GetSmoothedShape(volume_shape);
            location.MosaicShape = mosaic_shape;

            return;
        }

        private static List<Vector2[]> VolumeInnerRingPointsToSection(Viking.VolumeModel.IVolumeToSectionTransform mapper, ICollection<Vector2[]> volume_innerRingPoints)
        {
            if (volume_innerRingPoints is null)
                return null;

            List<Vector2[]> mosaic_innerRingPoints = new(volume_innerRingPoints.Count);
            foreach (Vector2[] volume_ring in volume_innerRingPoints)
            {
                mosaic_innerRingPoints.Add(mapper.VolumeToSection(volume_ring));
            }

            return mosaic_innerRingPoints;
        }

    }
}
