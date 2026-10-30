using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;

namespace Viking.VolumeModel
{

    /// <summary>
    /// Sets a location's shapes. Mosaic is the truth and the volume shape is always the forward owner's under the volume's
    /// default transform (<see cref="VolumeCache"/>); see <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
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
        public static void SetShapeFromPointsInVolume(this WebAnnotationModel.Objects.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Vector2[] volumePoints, ICollection<Vector2[]> volume_innerRingPoints, double? volumeWidth = null)
        {
            MosaicGeometry mosaic = LocationVolumeGeometry.ToMosaic(location.TypeCode, volumePoints, mapper, volumeWidth,
                                                                    volume_innerRingPoints is null ? null : [.. volume_innerRingPoints]);
            if (!mosaic.Succeeded)
                throw new ArgumentException(mosaic.Failure);

            location.TryApply(mosaic, mapper);
        }

        /// <summary>
        /// Sets a location from a mosaic shape, the truth: <c>MosaicShape</c> is <paramref name="shape"/> and <c>VolumeShape</c>
        /// is the cache the forward owner derives from it under the volume's <b>default</b> transform
        /// (<see cref="VolumeCache"/>), never a volume shape the caller kept and never one under the transform on screen. Set
        /// <c>TypeCode</c> (and <c>Width</c>) first: the volume shape is smoothed for the type.
        /// </summary>
        /// <exception cref="ArgumentException">The shape is not valid SQL geometry, or it does not map onto the volume under
        /// <paramref name="mapper"/>; the location is then unchanged.</exception>
        public static void SetShapeFromGeometryInSection(this WebAnnotationModel.Objects.LocationObj location, Viking.VolumeModel.IVolumeToSectionTransform mapper, Microsoft.SqlServer.Types.SqlGeometry shape) =>
            SetMosaicShape(location, mapper, shape, null);

        /// <summary>
        /// <see cref="SetShapeFromGeometryInSection"/> for a circle in mosaic space, stored as that exact
        /// <see cref="Circle"/>.
        /// </summary>
        /// <exception cref="ArgumentException">The circle does not map onto the volume; the location is then unchanged.</exception>
        public static void SetShapeFromCircleInSection(this WebAnnotationModel.Objects.LocationObj location, IVolumeToSectionTransform mapper, Circle mosaicCircle) =>
            SetMosaicShape(location, mapper, SqlGeometryUtils.Extensions.ToCircle(mosaicCircle.Center.X, mosaicCircle.Center.Y, 0, mosaicCircle.Radius), mosaicCircle);

        /// <summary>
        /// The circle a <c>CURVEPOLYGON</c> built by <c>SqlGeometryUtils.Extensions.ToCircle</c> describes, read from its
        /// defining points (east, north, west, south, east): the centre is midway between the east and west points and the
        /// radius half their distance. <c>ToShape2D</c> reads a circle from its bounding box, which SQL geometry computes for
        /// curves only approximately (about 2e-6 of the radius). Null for any other shape.
        /// </summary>
        private static Circle? ExactCircle(Microsoft.SqlServer.Types.SqlGeometry shape)
        {
            if (shape.GeometryType() != SupportedGeometryType.CURVEPOLYGON)
                return null;

            Microsoft.SqlServer.Types.SqlGeometry ring = shape.STExteriorRing();
            if (ring.IsNull || ring.STNumPoints().Value != 5)
                return null;

            Vector2 east = new(ring.STPointN(1).STX.Value, ring.STPointN(1).STY.Value);
            Vector2 west = new(ring.STPointN(3).STX.Value, ring.STPointN(3).STY.Value);
            double radius = Vector2.Distance(east, west) / 2.0;
            return radius > 0 ? new Circle((east + west) / 2.0, radius) : null;
        }

        /// <summary>
        /// Saves an edit made in volume space after <see cref="LocationVolumeGeometry.ToMosaic(Viking.AnnotationServiceTypes.Interfaces.LocationType, Microsoft.SqlServer.Types.SqlGeometry, IVolumeToSectionTransform, double?)"/>
        /// mapped it back: the mosaic shape (and width) become the truth and the forward owner derives the volume shape
        /// (<see cref="SetShapeFromGeometryInSection"/>). Set <c>TypeCode</c> first.
        /// </summary>
        /// <returns>False, with the location unchanged, when the edit could not be mapped; <see cref="MosaicGeometry.Failure"/> says why.</returns>
        /// <exception cref="ArgumentException">The mapped shape is not valid SQL geometry; the location is then unchanged.</exception>
        public static bool TryApply(this WebAnnotationModel.Objects.LocationObj location, MosaicGeometry mosaic, Viking.VolumeModel.IVolumeToSectionTransform mapper)
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
                SetMosaicShape(location, mapper, mosaic.Shape, mosaic.Circle);
            }
            catch (ArgumentException)
            {
                location.Width = oldWidth;
                throw;
            }

            return true;
        }

        /// <summary>
        /// The one place a location's <c>MosaicShape</c> is written from a mosaic shape, followed by the volume cache the
        /// forward owner derives under the volume's <b>default</b> transform (<see cref="VolumeCache"/>), never under the
        /// transform on screen. The shape must map onto the volume under <paramref name="mapper"/>, the transform the edit was
        /// made under; without an open volume that is also the cache's basis (<see cref="VolumeCache.Basis"/>). A location
        /// that had a shape keeps it when the save is refused. <paramref name="exactCircle"/> is the mosaic circle when <paramref name="shape"/> is one:
        /// the gRPC <c>LocationObj</c> stores a <see cref="Circle"/>, and one read back from SQL geometry takes a bounding-box
        /// radius that carries rounding.
        /// </summary>
        private static void SetMosaicShape(WebAnnotationModel.Objects.LocationObj location, IVolumeToSectionTransform mapper,
                                           Microsoft.SqlServer.Types.SqlGeometry shape, Circle? exactCircle)
        {
            if (!shape.STIsValid().Value)
                throw new ArgumentException("Shape must be valid SQL Geometry " + shape.IsValidDetailed());

            if (LocationVolumeGeometry.MapShape(location.TypeCode, shape, mapper) is null)
                throw new ArgumentException("The shape lies partly outside the region this section's transform maps, so it cannot be drawn on the volume.");

            IShape2D oldShape = location.MosaicShape;
            location.MosaicShape = (exactCircle ?? ExactCircle(shape)) is Circle circle ? circle : shape.ToShape2D();

            IShape2D cache;
            try
            {
                cache = VolumeCache.Shape(location, VolumeCache.Basis(location, mapper));
            }
            catch (ArgumentException e)
            {
                if (oldShape is not null)
                    location.MosaicShape = oldShape;

                throw new ArgumentException("The shape lies outside the region the volume's default section alignment can place, so its stored volume position cannot be computed.", e);
            }

            location.VolumeShape = cache;
        }

    }
}
