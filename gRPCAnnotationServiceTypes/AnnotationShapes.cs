using System;
using System.Collections.Generic;
using System.Linq;
using Viking.AnnotationServiceTypes.Interfaces;
using GeoCircle = global::Geometry.Circle;
using GeoLineSegment = global::Geometry.LineSegment;
using GeoPolygon = global::Geometry.Polygon;
using GeoPolyline = global::Geometry.Polyline;
using GeoRectangle = global::Geometry.Rectangle;
using GeoVector2 = global::Geometry.Vector2;
using ICircle2D = global::Geometry.ICircle2D;
using ILineSegment2D = global::Geometry.ILineSegment2D;
using IPoint2D = global::Geometry.IPoint2D;
using IPolygon2D = global::Geometry.IPolygon2D;
using IPolyLine2D = global::Geometry.IPolyLine2D;
using IShape2D = global::Geometry.IShape2D;
using ProtoCircle = Viking.gRPC.Geometry.V1.Circle;
using ProtoCoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using ProtoCurveFit = Viking.gRPC.Geometry.V1.CurveFit;
using ProtoCurveMethod = Viking.gRPC.Geometry.V1.CurveMethod;
using ProtoLine = Viking.gRPC.Geometry.V1.Line;
using ProtoPolygon = Viking.gRPC.Geometry.V1.Polygon;
using ProtoRectangle = Viking.gRPC.Geometry.V1.Rectangle;
using ProtoRing = Viking.gRPC.Geometry.V1.Ring;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;
using ProtoVector2 = Viking.gRPC.Geometry.V1.Vector2;

namespace Viking.AnnotationServiceTypes.gRPC.V1
{
    /// <summary>
    /// The one converter between annotation shapes on the wire (<c>viking.geometry.v1.Shape</c>) and Viking's stored
    /// shapes, used by the annotation server (whose database adapter supplies point lists) and the gRPC client (which
    /// holds <see cref="IShape2D"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mapping by <see cref="LocationType"/>: POINT is <c>Shape.point</c>; CIRCLE is <c>Shape.circle</c> (centre is the
    /// location's position, radius the true radius in that space); POLYLINE is an open <c>Line</c> without a fit;
    /// OPENCURVE an open <c>Line</c> and CLOSEDCURVE a closed <c>Line</c>, both with a Catmull-Rom fit; POLYGON a
    /// <c>Polygon</c> without a fit and CURVEPOLYGON a <c>Polygon</c> with one. The type is never sent: <see cref="LocationTypeOf"/>
    /// derives it from the mosaic shape.
    /// </para>
    /// <para>
    /// Mosaic shapes of curves are their control points, sent with <see cref="ProtoCurveMethod.CatmullRom"/> and
    /// <c>interpolations = 0</c>: the drawing density is a client display setting, not stored data. Volume shapes of
    /// curves are the fitted outline the client stores (it smooths before saving), so they are sent as they are,
    /// without a fit (<see cref="ProtoCoordinateSpace.Volume"/>).
    /// </para>
    /// <para>
    /// Rings on the wire do not repeat their first vertex; stored rings and closed lines (WKT, <see cref="GeoPolygon"/>)
    /// do, so the vertex is dropped on the way out and appended on the way in. A line's width is the location's Width;
    /// the database stores only the mosaic width, so volume lines carry width 0 (unknown).
    /// </para>
    /// <para>
    /// ELLIPSE has no producer in Viking and no <see cref="IShape2D"/> type: an ELLIPSE row is sent as the geometry it
    /// stores, and an incoming <c>Shape.ellipse</c> is refused by <see cref="ToShape2D"/>.
    /// </para>
    /// </remarks>
    public static class AnnotationShapes
    {
        static readonly ProtoCurveFit CatmullRomFit = new() { Method = ProtoCurveMethod.CatmullRom, Interpolations = 0 };

        #region Vectors and rectangles

        /// <summary>A Geometry point as a wire vector.</summary>
        public static ProtoVector2 ToProtoVector2(this IPoint2D p) => new() { X = p.X, Y = p.Y };

        /// <summary>A Geometry vector as a wire vector.</summary>
        public static ProtoVector2 ToProtoVector2(this GeoVector2 p) => new() { X = p.X, Y = p.Y };

        /// <summary>A wire vector as a Geometry vector.</summary>
        public static GeoVector2 ToVector2(this ProtoVector2 p) => new(p.X, p.Y);

        /// <summary>A Geometry rectangle as wire bounds (x_min = Left, y_min = Bottom, x_max = Right, y_max = Top).</summary>
        public static ProtoRectangle ToProtoRectangle(this GeoRectangle r) => new()
        {
            XMin = r.Left,
            YMin = r.Bottom,
            XMax = r.Right,
            YMax = r.Top,
        };

        /// <summary>Wire bounds as a Geometry rectangle.</summary>
        public static GeoRectangle ToRectangle(this ProtoRectangle r) => new(left: r.XMin, right: r.XMax, bottom: r.YMin, top: r.YMax);

        #endregion

        #region Stored shape to wire

        /// <summary>
        /// Converts a client shape of a location of <paramref name="type"/> to the wire shape for
        /// <paramref name="space"/>. Circles keep their own centre and radius (the caller holds one circle per space).
        /// </summary>
        /// <param name="shape">The stored shape: <see cref="GeoVector2"/>, <see cref="ICircle2D"/>,
        /// <see cref="ILineSegment2D"/>, <see cref="IPolyLine2D"/> or <see cref="IPolygon2D"/>.</param>
        /// <param name="type">The location's type.</param>
        /// <param name="space">Mosaic shapes of curves get a fit; volume shapes are sent as the stored outline.</param>
        /// <param name="width">The location's line width; used by line types only.</param>
        /// <returns>The wire shape, or null when <paramref name="shape"/> is null.</returns>
        /// <exception cref="ArgumentException">The shape is of a kind no annotation stores.</exception>
        public static ProtoShape ToShape(this IShape2D shape, LocationType type, ProtoCoordinateSpace space, double? width)
        {
            switch (shape)
            {
                case null:
                    return null;
                case ICircle2D circle:
                    return CircleShape(circle.Center.X, circle.Center.Y, circle.Radius);
                case GeoVector2 point:
                    return PointShape(point.X, point.Y);
                case ILineSegment2D segment:
                    return LineShape(type, space, new[] { ToGeoVector2(segment.A), ToGeoVector2(segment.B) }, width);
                case IPolyLine2D polyline:
                    return LineShape(type, space, polyline.Points.Select(ToGeoVector2).ToList(), width);
                case IPolygon2D polygon:
                    return PolygonShape(type, space,
                        polygon.ExteriorRing.Select(ToGeoVector2).ToList(),
                        polygon.InteriorRings.Select(r => (IReadOnlyList<GeoVector2>)r.Select(ToGeoVector2).ToList()).ToList(),
                        width);
                case IPoint2D point:
                    return PointShape(point.X, point.Y);
                default:
                    throw new ArgumentException($"Shape {shape.GetType().Name} is not an annotation shape", nameof(shape));
            }
        }

        /// <summary>A point annotation.</summary>
        public static ProtoShape PointShape(double x, double y) => new() { Point = new ProtoVector2 { X = x, Y = y } };

        /// <summary>A circle: <paramref name="x"/>, <paramref name="y"/> is the location's position in the space and
        /// <paramref name="radius"/> the true radius in that space.</summary>
        public static ProtoShape CircleShape(double x, double y, double radius) => new()
        {
            Circle = new ProtoCircle { Center = new ProtoVector2 { X = x, Y = y }, Radius = radius }
        };

        /// <summary>
        /// A stored line string (open, or closed with its first vertex repeated). In mosaic space a polygon type stored
        /// as a line becomes a polygon of those points so the shape agrees with the type.
        /// </summary>
        /// <param name="type">The location's type: CLOSEDCURVE lines are closed; curves get a fit in mosaic space.</param>
        /// <param name="space">See <see cref="ToShape(IShape2D, LocationType, ProtoCoordinateSpace, double?)"/>.</param>
        /// <param name="points">The stored points.</param>
        /// <param name="width">The location's width (null means 0).</param>
        public static ProtoShape LineShape(LocationType type, ProtoCoordinateSpace space, IReadOnlyList<GeoVector2> points, double? width)
        {
            bool mosaic = space != ProtoCoordinateSpace.Volume;
            if (mosaic && type is LocationType.POLYGON or LocationType.CURVEPOLYGON)
                return PolygonShape(type, space, points, Array.Empty<IReadOnlyList<GeoVector2>>(), width);

            bool closed = type == LocationType.CLOSEDCURVE;
            var line = new ProtoLine
            {
                Closed = closed,
                Width = mosaic ? width ?? 0 : 0,
            };
            line.Points.AddRange((closed ? OpenRing(points) : points).Select(p => p.ToProtoVector2()));
            if (mosaic && type is LocationType.OPENCURVE or LocationType.CLOSEDCURVE)
                line.Fit = CatmullRomFit.Clone();

            return new ProtoShape { Line = line };
        }

        /// <summary>
        /// A stored polygon (rings with their first vertex repeated). In mosaic space a line type stored as a polygon
        /// (a closed curve drawn as a polygon) becomes a line of the exterior ring so the shape agrees with the type.
        /// </summary>
        /// <param name="type">The location's type: CURVEPOLYGON gets a fit in mosaic space.</param>
        /// <param name="space">See <see cref="ToShape(IShape2D, LocationType, ProtoCoordinateSpace, double?)"/>.</param>
        /// <param name="exterior">The exterior ring.</param>
        /// <param name="holes">The interior rings.</param>
        /// <param name="width">The location's width, used when the shape becomes a line.</param>
        public static ProtoShape PolygonShape(LocationType type, ProtoCoordinateSpace space, IReadOnlyList<GeoVector2> exterior,
            IEnumerable<IReadOnlyList<GeoVector2>> holes, double? width)
        {
            bool mosaic = space != ProtoCoordinateSpace.Volume;
            if (mosaic && type is LocationType.POLYLINE or LocationType.OPENCURVE or LocationType.CLOSEDCURVE)
                return LineShape(type, space, exterior, width);

            var polygon = new ProtoPolygon { Exterior = ToRing(exterior) };
            polygon.Holes.AddRange(holes.Select(ToRing));
            if (mosaic && type == LocationType.CURVEPOLYGON)
                polygon.Fit = CatmullRomFit.Clone();

            return new ProtoShape { Polygon = polygon };
        }

        static ProtoRing ToRing(IReadOnlyList<GeoVector2> stored)
        {
            var ring = new ProtoRing();
            ring.Points.AddRange(OpenRing(stored).Select(p => p.ToProtoVector2()));
            return ring;
        }

        /// <summary>The ring without its repeated closing vertex.</summary>
        static IReadOnlyList<GeoVector2> OpenRing(IReadOnlyList<GeoVector2> points)
        {
            if (points.Count > 1 && points[0] == points[points.Count - 1])
                return points.Take(points.Count - 1).ToList();
            return points;
        }

        #endregion

        #region Wire to stored shape

        /// <summary>
        /// The location type a mosaic shape encodes: the case, <c>Line.closed</c> and the fit. A closed line is always
        /// a CLOSEDCURVE (Viking has no closed line without a fit).
        /// </summary>
        /// <exception cref="ArgumentException">The shape is empty.</exception>
        public static LocationType LocationTypeOf(this ProtoShape shape)
        {
            switch (shape?.KindCase ?? ProtoShape.KindOneofCase.None)
            {
                case ProtoShape.KindOneofCase.Point:
                    return LocationType.POINT;
                case ProtoShape.KindOneofCase.Circle:
                    return LocationType.CIRCLE;
                case ProtoShape.KindOneofCase.Ellipse:
                    return LocationType.ELLIPSE;
                case ProtoShape.KindOneofCase.Line:
                    if (shape.Line.Closed)
                        return LocationType.CLOSEDCURVE;
                    return IsFitted(shape.Line.Fit) ? LocationType.OPENCURVE : LocationType.POLYLINE;
                case ProtoShape.KindOneofCase.Polygon:
                    return IsFitted(shape.Polygon.Fit) ? LocationType.CURVEPOLYGON : LocationType.POLYGON;
                default:
                    throw new ArgumentException("The shape has no geometry", nameof(shape));
            }
        }

        /// <summary>The line width of a line shape; null for every other case (the database requires null there).</summary>
        public static double? WidthOf(this ProtoShape shape) =>
            shape?.KindCase == ProtoShape.KindOneofCase.Line ? shape.Line.Width : null;

        /// <summary>
        /// The points of a line as stored: a closed line repeats its first vertex at the end.
        /// </summary>
        public static GeoVector2[] StoredLinePoints(this ProtoLine line)
        {
            var points = line.Points.Select(ToVector2).ToList();
            if (line.Closed && points.Count > 1 && points[0] != points[points.Count - 1])
                points.Add(points[0]);
            return points.ToArray();
        }

        /// <summary>A wire ring as stored: its first vertex repeated at the end.</summary>
        public static GeoVector2[] StoredRing(this ProtoRing ring)
        {
            var points = ring.Points.Select(ToVector2).ToList();
            if (points.Count > 0 && points[0] != points[points.Count - 1])
                points.Add(points[0]);
            return points.ToArray();
        }

        /// <summary>
        /// The client shape for a wire shape, built the way the client's WKT parser builds the stored geometry: a
        /// point is a <see cref="GeoVector2"/>, a two-point open line a <see cref="GeoLineSegment"/>, other lines a
        /// <see cref="GeoPolyline"/> (closed lines repeat the first vertex), a polygon a <see cref="GeoPolygon"/> with its
        /// holes, a circle a <see cref="GeoCircle"/>.
        /// </summary>
        /// <returns>The shape, or null for a null or empty shape.</returns>
        /// <exception cref="NotSupportedException">The shape is an ellipse, which Viking cannot hold.</exception>
        public static IShape2D ToShape2D(this ProtoShape shape)
        {
            switch (shape?.KindCase ?? ProtoShape.KindOneofCase.None)
            {
                case ProtoShape.KindOneofCase.None:
                    return null;
                case ProtoShape.KindOneofCase.Point:
                    return shape.Point.ToVector2();
                case ProtoShape.KindOneofCase.Circle:
                    return new GeoCircle(shape.Circle.Center?.ToVector2() ?? default, shape.Circle.Radius);
                case ProtoShape.KindOneofCase.Line:
                {
                    var points = shape.Line.StoredLinePoints();
                    if (points.Length == 2)
                        return new GeoLineSegment(points[0], points[1]);
                    return new GeoPolyline(points);
                }
                case ProtoShape.KindOneofCase.Polygon:
                {
                    var polygon = new GeoPolygon(shape.Polygon.Exterior.StoredRing());
                    foreach (var hole in shape.Polygon.Holes)
                        polygon.AddInteriorRing(hole.StoredRing());
                    return polygon;
                }
                case ProtoShape.KindOneofCase.Ellipse:
                    throw new NotSupportedException("Viking has no ellipse shape; ellipse annotations are not supported");
                default:
                    throw new NotSupportedException($"Unexpected shape case {shape.KindCase}");
            }
        }

        static GeoVector2 ToGeoVector2(IPoint2D p) => new(p.X, p.Y);

        static bool IsFitted(ProtoCurveFit fit) => fit != null && fit.Method != ProtoCurveMethod.None;

        #endregion
    }
}
