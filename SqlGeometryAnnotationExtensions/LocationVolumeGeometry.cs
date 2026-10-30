#nullable enable
using Geometry;
using Microsoft.SqlServer.Types;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.Objects;

namespace Viking.VolumeModel
{
    /// <summary>
    /// The one forward owner of a location's volume-space geometry. Mosaic coordinates are the truth; the stored
    /// <c>VolumeShape</c>, <c>VolumeX</c> and <c>VolumeY</c> are a cache of them. Every view, label and link that draws or
    /// hit-tests a location takes its volume geometry from here and never maps or reads the stored cache itself, so a
    /// changed transform (a new .stos) re-renders every annotation at once. See
    /// <c>.cursor/rules/annotation-volume-geometry.mdc</c>.
    /// </summary>
    /// <remarks>
    /// <para>The shape is <see cref="MapShape"/>: the mosaic shape mapped with <c>TryMapShapeSectionToVolume</c> and then
    /// smoothed with <c>GetSmoothedShape</c>. The stored cache is <see cref="CacheShape"/> under the volume's default transform
    /// (<see cref="VolumeCache"/>), written by every save and by <c>VolumePositionUpdateJob</c>. A circle's centre comes from the location's X,Y and its radius from the five-point mean scale (<see cref="CircleMapping"/>).
    /// Line and curve widths scale by the five-point mean scale at the location's centre, as a circle radius does.</para>
    /// <para><b>Mapping failure policy.</b> When the strict mapping fails (a point outside the transform's hull):
    /// circles and points take the centre from the transform's unchecked <c>SectionToVolume</c> and keep the mosaic
    /// radius; every other type takes each control point from the unchecked <c>SectionToVolume</c>, with a scale of 1.
    /// The unchecked mapping may extrapolate or throw <see cref="ArgumentException"/>, which callers that build views
    /// already treat as "skip this location". <see cref="IsExact"/> is then false, and <see cref="CanDrawOnSection"/> is
    /// false for polygons, curve polygons and polylines: on the viewed section those are not drawn from an extrapolated
    /// outline, while their markers on adjacent sections, and every curve, are.</para>
    /// <para>Instances are immutable and shared between threads; the lazily computed parts are thread-safe.</para>
    /// <para>The inverse, from a geometry the user edited in volume space back to the mosaic geometry that is saved, is
    /// <see cref="ToMosaic(LocationType, SqlGeometry, IVolumeToSectionTransform, double?)"/> and its overloads.</para>
    /// </remarks>
    public sealed partial class LocationVolumeGeometry
    {
        /// <summary>
        /// A location's cached geometries, most recent first, one per transform object. More than one, because a save writes
        /// the stored cache under the volume's default transform (<c>VolumeCache</c>) while the views draw under the active
        /// one: with a single entry each save under a non-default active transform evicted the views' geometry. Replaced as a
        /// whole (copy on write), so readers on other threads see a complete array.
        /// </summary>
        private sealed class Slot
        {
            public volatile LocationVolumeGeometry[] Values = [];
        }

        /// <summary>
        /// Geometries kept per location: the active transform's (drawn), the default's (saved) and one more, so switching
        /// between two transforms does not rebuild either.
        /// </summary>
        private const int SlotCapacity = 3;

        private static readonly ConditionalWeakTable<LocationObj, Slot> Cache = new();

        private readonly IShape2D mosaicShape;
        private readonly IVolumeToSectionTransform transform;
        private readonly double? mosaicWidth;
        private readonly SqlGeometry controlShape;
        private readonly Vector2[] controlPoints;
        private readonly Lazy<SqlGeometry> shape;
        private readonly Lazy<Vector2> center;
        private readonly Lazy<Circle> inscribedCircle;
        private readonly Lazy<SqlGeometry?> outline;

        /// <summary>The location type the geometry was built for.</summary>
        public LocationType TypeCode { get; }

        /// <summary>False when <see cref="LocationVolumeGeometry"/>'s mapping failure policy supplied the geometry.</summary>
        public bool IsExact { get; }

        /// <summary>
        /// False when the location must not be drawn on the viewed section: a polygon, curve polygon or polyline whose
        /// geometry came from the mapping failure policy. Views of those throw <see cref="ArgumentException"/>.
        /// </summary>
        public bool CanDrawOnSection => IsExact || !(TypeCode is LocationType.POLYGON or LocationType.CURVEPOLYGON or LocationType.POLYLINE);

        /// <summary>
        /// The mosaic control points mapped into volume space, unsmoothed: the exterior ring of a polygon, the vertices of a
        /// line or curve, the point of a circle or point. Lines and edit handles are drawn through these.
        /// </summary>
        public IReadOnlyList<Vector2> ControlPoints => controlPoints;

        /// <summary>The volume circle of a circle or point location (X,Y mapped, radius by the five-point mean); null for other types.</summary>
        public Circle? Circle { get; }

        /// <summary>The local five-point mean scale of the transform at the location (volume units per mosaic pixel).</summary>
        public double Scale { get; }

        /// <summary>
        /// The volume radius: the circle's radius for circles and points, otherwise the location's mosaic
        /// <see cref="LocationObj.Radius"/> times <see cref="Scale"/>.
        /// </summary>
        public double Radius { get; }

        /// <summary>The volume width of a line or curve: the location's mosaic width times <see cref="Scale"/>.</summary>
        public double Width { get; }

        /// <summary>
        /// Interpolated points per control-point span in <see cref="Shape"/>: what a curve view draws its curve with, so the
        /// drawn curve is the hit-tested one. Zero for types that are not smoothed.
        /// </summary>
        public uint InterpolationPoints => TypeCode switch
        {
            LocationType.OPENCURVE => ShapeSmoothingExtensions.NumOpenCurveInterpolationPoints,
            LocationType.CLOSEDCURVE or LocationType.CURVEPOLYGON => ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints,
            _ => 0,
        };

        /// <summary>The smoothed volume shape, the value <see cref="MapShape"/> gives.</summary>
        public SqlGeometry Shape => shape.Value;

        /// <summary>
        /// The value the stored <c>VolumeShape</c> holds when this is the geometry under the volume's default transform
        /// (<see cref="VolumeCache"/>): a circle location's exact <see cref="Circle"/>, otherwise <see cref="Shape"/>.
        /// <see cref="Shape"/> smooths a circle's ring into the inscribed circle of its polygon, about 2% smaller, so a circle is
        /// stored as the circle every view draws and every circle edit saves.
        /// </summary>
        public SqlGeometry CacheShape => TypeCode == LocationType.CIRCLE && Circle is Circle circle
            ? SqlGeometryUtils.Extensions.ToCircle(circle.Center.X, circle.Center.Y, 0, circle.Radius)
            : Shape;

        /// <summary>
        /// The polygon a polygon, curve polygon or closed curve is hit-tested as on its own section, and the outline its
        /// location links leave: the mapped control polygon of a polygon or curve polygon, the ring of <see cref="Shape"/>
        /// for a closed curve. Null for other types.
        /// </summary>
        public SqlGeometry? Outline => outline.Value;

        /// <summary>
        /// The volume position labels and markers are placed at: the circle's centre for circles and points, otherwise the
        /// centroid of <see cref="Shape"/> (as <see cref="LocationObj.VolumePosition"/> is computed from a current cache).
        /// </summary>
        public Vector2 Center => center.Value;

        private LocationVolumeGeometry(LocationObj location, IShape2D mosaicKey, IVolumeToSectionTransform transform)
        {
            mosaicShape = mosaicKey;
            SqlGeometry mosaic = ToSqlGeometry(mosaicKey);
            this.transform = transform;
            mosaicWidth = location.Width;
            TypeCode = location.TypeCode;

            if (IsCircleType(TypeCode))
            {
                Circle mosaicCircle = new(location.Position, location.Radius);
                if (transform.TryMapShapeSectionToVolume(mosaicCircle) is Circle mappedCircle)
                {
                    Circle = mappedCircle;
                    Scale = mappedCircle.Radius / mosaicCircle.Radius;
                    IsExact = true;
                }
                else
                {
                    Circle = new Circle(transform.SectionToVolume(mosaicCircle.Center), mosaicCircle.Radius);
                    Scale = 1;
                }

                Radius = Circle.Value.Radius;
                controlPoints = [Circle.Value.Center];
                SqlGeometry? mappedShape = IsExact ? transform.TryMapShapeSectionToVolume(mosaic) : null;
                controlShape = mappedShape ?? (mosaic.GeometryType() == SupportedGeometryType.POINT
                    ? Circle.Value.Center.ToSqlGeometry()
                    : SqlGeometryUtils.Extensions.ToCircle(Circle.Value.Center.X, Circle.Value.Center.Y, 0, Circle.Value.Radius));
            }
            else
            {
                SqlGeometry? mapped = transform.TryMapShapeSectionToVolume(mosaic);
                IsExact = mapped is not null;
                controlShape = mapped ?? SqlGeometryUtils.Extensions.ToGeometry(mosaic.GeometryType(), transform.SectionToVolume(mosaic.ToPoints()));

                controlPoints = controlShape.ToPoints();
                double sampleRadius = IsLineType(TypeCode) ? (location.Width ?? 1) / 2.0 : location.Radius;
                Scale = LocalScale(transform, location.Position, sampleRadius);
                Radius = location.Radius * Scale;
            }

            Width = (location.Width ?? 1) * Scale;
            shape = new Lazy<SqlGeometry>(() => SmoothedShape(TypeCode, controlShape));
            center = new Lazy<Vector2>(() => Circle?.Center ?? Shape.Centroid());
            inscribedCircle = new Lazy<Circle>(() => Shape.CalculateInscribedCircle());
            outline = new Lazy<SqlGeometry?>(() => TypeCode switch
            {
                LocationType.POLYGON or LocationType.CURVEPOLYGON => controlShape,
                LocationType.CLOSEDCURVE => Shape.ToPoints().ToPolygon(),
                _ => null,
            });
        }

        /// <summary>
        /// The geometry of <paramref name="location"/> under <paramref name="transform"/>, cached per location and rebuilt
        /// when the mosaic shape, width or type changes or a different transform object is passed (a reloaded .stos makes a
        /// new transform). Throws <see cref="ArgumentException"/> when even the fallback mapping fails.
        /// </summary>
        public static LocationVolumeGeometry For(LocationObj location, IVolumeToSectionTransform transform)
        {
            if (location is null)
                throw new ArgumentNullException(nameof(location));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            Slot slot = Cache.GetValue(location, _ => new Slot());
            IShape2D mosaic = location.MosaicShape;
            LocationVolumeGeometry[] cached = slot.Values;
            foreach (LocationVolumeGeometry geometry in cached)
            {
                if (geometry.Matches(location, mosaic, transform))
                    return geometry;
            }

            LocationVolumeGeometry created = new(location, mosaic, transform);

            // A shape, width or type change leaves the older entries stale; only other transforms' current entries are kept.
            List<LocationVolumeGeometry> kept = [created];
            foreach (LocationVolumeGeometry geometry in cached)
            {
                if (kept.Count < SlotCapacity && geometry.MatchesShapeOf(location, mosaic) && !ReferenceEquals(geometry.transform, transform))
                    kept.Add(geometry);
            }

            slot.Values = [.. kept];
            return created;
        }

        /// <summary>The geometry of <paramref name="location"/> under the transform <paramref name="provider"/> gives its section.</summary>
        public static LocationVolumeGeometry For(LocationObj location, IVolumeTransformProvider provider) =>
            For(location, provider.GetSectionToVolumeTransform((int)Math.Round(location.Z)));

        /// <summary>Drops the cached geometry of <paramref name="location"/>; the next <see cref="For(LocationObj, IVolumeToSectionTransform)"/> rebuilds it.</summary>
        public static void Invalidate(LocationObj location)
        {
            if (location is not null)
                Cache.Remove(location);
        }

        /// <summary>
        /// <paramref name="mosaic"/> mapped into volume space and smoothed for <paramref name="type"/>, the value
        /// <see cref="Shape"/> has: for geometry that is not a saved location's (a preview), and to check that an edited mosaic
        /// shape maps. Null when the shape cannot be mapped. The stored cache is <see cref="CacheShape"/> (<see cref="VolumeCache"/>).
        /// </summary>
        public static SqlGeometry? MapShape(LocationType type, SqlGeometry mosaic, IVolumeToSectionTransform transform)
        {
            SqlGeometry? unsmoothed = transform.TryMapShapeSectionToVolume(mosaic);
            return unsmoothed is null ? null : SmoothedShape(type, unsmoothed);
        }

        /// <summary>
        /// The forward mapping of mosaic points that are not a saved location's geometry: an edit preview, a segmentation
        /// prompt, a region to load. The strict mapping the owner itself uses; a location's own geometry comes from
        /// <see cref="For(LocationObj, IVolumeToSectionTransform)"/>.
        /// </summary>
        /// <returns>Whether each point mapped; <paramref name="volumePoints"/> holds every mapped point at its index.</returns>
        public static bool[] TryMapPoints(IReadOnlyList<Vector2> mosaicPoints, IVolumeToSectionTransform transform, out Vector2[] volumePoints)
        {
            if (mosaicPoints is null)
                throw new ArgumentNullException(nameof(mosaicPoints));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            return transform.TrySectionToVolume(mosaicPoints as Vector2[] ?? [.. mosaicPoints], out volumePoints);
        }

        /// <summary>
        /// A mosaic circle that is not a saved location's (a prompt, a preview) mapped forward by the rule
        /// <see cref="Circle"/> uses (<see cref="CircleMapping"/>); null when its centre or every rim point fails to map.
        /// </summary>
        public static Circle? TryMapCircle(Circle mosaicCircle, IVolumeToSectionTransform transform) =>
            (transform ?? throw new ArgumentNullException(nameof(transform))).TryMapShapeSectionToVolume(mosaicCircle);

        /// <summary>
        /// A mosaic polygon that is not a saved location's (a preview while it is moved, a cut piece) mapped forward
        /// unsmoothed, holes included, as <see cref="ControlPolygon"/> maps a location's; null when any point fails to map.
        /// </summary>
        public static Polygon? TryMapPolygon(Polygon mosaicPolygon, IVolumeToSectionTransform transform) =>
            (transform ?? throw new ArgumentNullException(nameof(transform))).TryMapShapeSectionToVolume(mosaicPolygon);

        /// <summary>
        /// A mosaic shape that is not a saved location's (a linked location being placed from another section) mapped
        /// forward unsmoothed, as the owner maps a location's control shape; null when any point fails to map.
        /// </summary>
        public static SqlGeometry? TryMapControlShape(SqlGeometry mosaicShape, IVolumeToSectionTransform transform) =>
            (transform ?? throw new ArgumentNullException(nameof(transform))).TryMapShapeSectionToVolume(mosaicShape);

        /// <summary>
        /// The circle drawn for this location on an adjacent section, in volume space: a circle or point at
        /// <paramref name="circleRadiusScalar"/> times its radius; an open curve or polyline at <see cref="Center"/>, half
        /// its mapped length across; a polygon, curve polygon or closed curve as the inscribed circle of <see cref="Shape"/>.
        /// </summary>
        public Circle AdjacentMarker(double circleRadiusScalar)
        {
            if (Circle is Circle circle)
                return new Circle(circle.Center, circle.Radius * circleRadiusScalar);

            if (IsLineType(TypeCode))
                return new Circle(Center, PolylineLength(controlPoints) / 2.0);

            return inscribedCircle.Value;
        }

        /// <summary>
        /// A new copy of the mapped, unsmoothed polygon (with holes) of a polygon or curve polygon location. A copy, because
        /// <see cref="Polygon"/> is mutable and this geometry is shared.
        /// </summary>
        public Polygon ControlPolygon() => controlShape.ToPolygon();

        /// <summary>True when this was built from <paramref name="location"/>'s current mosaic shape, width and type.</summary>
        private bool MatchesShapeOf(LocationObj location, IShape2D mosaic) =>
            ReferenceEquals(mosaicShape, mosaic) && TypeCode == location.TypeCode && mosaicWidth == location.Width;

        private bool Matches(LocationObj location, IShape2D mosaic, IVolumeToSectionTransform t) =>
            ReferenceEquals(mosaicShape, mosaic) && ReferenceEquals(transform, t) &&
            TypeCode == location.TypeCode && mosaicWidth == location.Width;

        /// <summary>
        /// The SQL geometry of a location's <see cref="LocationObj.MosaicShape"/>. A point location's shape is a
        /// <see cref="Vector2"/>, which the general <c>IShape2D</c> conversion does not take.
        /// </summary>
        public static SqlGeometry ToSqlGeometry(IShape2D shape) =>
            shape is Vector2 point ? point.ToSqlGeometry() : shape.ToSqlGeometry();

        private static SqlGeometry SmoothedShape(LocationType type, SqlGeometry unsmoothed) => type.GetSmoothedShape(unsmoothed);

        private static bool IsCircleType(LocationType type) => type is LocationType.CIRCLE or LocationType.POINT;

        private static bool IsLineType(LocationType type) => type is LocationType.POLYLINE or LocationType.OPENCURVE;

        /// <summary>
        /// The five-point mean scale at <paramref name="mosaicCenter"/>, sampled at <paramref name="sampleRadius"/>
        /// (<see cref="CircleMapping"/>); 1 when that circle cannot be mapped.
        /// </summary>
        private static double LocalScale(IVolumeToSectionTransform transform, Vector2 mosaicCenter, double sampleRadius)
        {
            if (!(sampleRadius > 0))
                sampleRadius = 1;

            return transform.TryMapShapeSectionToVolume(new Circle(mosaicCenter, sampleRadius)) is Circle mapped
                ? mapped.Radius / sampleRadius
                : 1;
        }

        private static double PolylineLength(Vector2[] points)
        {
            double length = 0;
            for (int i = 1; i < points.Length; i++)
                length += Vector2.Distance(points[i - 1], points[i]);

            return length;
        }
    }
}
