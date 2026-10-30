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
        /// Sets a location from unsmoothed control points the user drew or accepted in volume space (a placed curve or polygon,
        /// a segmentation result): the inverse owner (<see cref="LocationVolumeGeometry.ToMosaic(Viking.AnnotationServiceTypes.Interfaces.LocationType, IReadOnlyList{Vector2}, IVolumeToSectionTransform, double?, IReadOnlyCollection{Vector2[]})"/>)
        /// maps them back to the mosaic shape that is saved and the forward owner derives the volume shape. Set
        /// <c>TypeCode</c> first.
        /// </summary>
        /// <param name="volumeWidth">The line width the user saw, in volume units, saved as the mosaic width; null keeps the width.</param>
        /// <exception cref="ArgumentException">A point cannot be mapped (the message says which) or the shape is not valid; the
        /// location is then unchanged.</exception>
        public static void SetShapeFromPointsInVolume(this WebAnnotationModel.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Vector2[] volumePoints, ICollection<Vector2[]> volume_innerRingPoints, double? volumeWidth = null)
        {
            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(location.TypeCode, volumePoints, mapper, volumeWidth,
                                                                    volume_innerRingPoints is null ? null : [.. volume_innerRingPoints]);
            if (!mosaic.Succeeded)
                throw new ArgumentException(mosaic.Failure);

            location.TryApply(mosaic, mapper);
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
            LocationVolumeGeometry geometry = LocationVolumeGeometry.For(location, mapper);

            // A circle is sent as the owner's volume circle, as circle edits always sent it. Shape (MapShape, which
            // VolumePositionUpdateJob writes) smooths a circle into the inscribed circle of its polygon, about 2% smaller;
            // TODO: settle which one the stored cache holds when the client's VolumeShape writes are reworked.
            location.VolumeShape = location.TypeCode == Viking.AnnotationServiceTypes.Interfaces.LocationType.CIRCLE && geometry.Circle is Circle circle
                ? SqlGeometryUtils.Extensions.ToCircle(circle.Center.X, circle.Center.Y, 0, circle.Radius)
                : geometry.Shape;
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

    }
}
