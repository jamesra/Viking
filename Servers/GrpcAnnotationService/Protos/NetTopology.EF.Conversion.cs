using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.Interfaces;
using GeoVector2 = Geometry.Vector2;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using ProtoCoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;

namespace gRPCAnnotationService.Protos
{
    /// <summary>
    /// The database boundary: NTS geometry (what EF reads and writes) ↔ wire <see cref="ProtoShape"/>, through
    /// <see cref="AnnotationShapes"/>. Circles never come through here: NTS cannot hold a CurvePolygon, so
    /// <see cref="LocationEFExtensions"/> builds them from the location's position and its rim point.
    /// </summary>
    public static class NetTopologyGeometryExtensions
    {
        static readonly GeometryFactory Factory = new();

        /// <summary>
        /// The wire shape of a stored geometry of a location of <paramref name="type"/> in <paramref name="space"/>.
        /// </summary>
        /// <exception cref="ArgumentException">The geometry is a kind no annotation stores (multi-geometries).</exception>
        public static ProtoShape ToShape(this NtsGeometry src, LocationType type, ProtoCoordinateSpace space, double? width)
        {
            switch (src)
            {
                case null:
                    return null;
                case Point point:
                    return AnnotationShapes.PointShape(point.X, point.Y);
                case LineString line:
                    return AnnotationShapes.LineShape(type, space, ToVectors(line.Coordinates), width);
                case Polygon polygon:
                    return AnnotationShapes.PolygonShape(type, space,
                        ToVectors(polygon.ExteriorRing.Coordinates),
                        polygon.InteriorRings.Select(r => (IReadOnlyList<GeoVector2>)ToVectors(r.Coordinates)),
                        width);
                default:
                    throw new ArgumentException($"Stored geometry {src.GeometryType} is not an annotation shape", nameof(src));
            }
        }

        /// <summary>
        /// The NTS geometry to store for a wire point, line or polygon. Closed lines and rings repeat their first
        /// vertex, as SQL Server requires.
        /// </summary>
        /// <exception cref="ArgumentException">The shape is a circle or an ellipse (no NTS form) or empty.</exception>
        public static NtsGeometry ToNetTopologyGeometry(this ProtoShape src)
        {
            switch (src?.KindCase ?? ProtoShape.KindOneofCase.None)
            {
                case ProtoShape.KindOneofCase.Point:
                    return Factory.CreatePoint(new Coordinate(src.Point.X, src.Point.Y));
                case ProtoShape.KindOneofCase.Line:
                    return Factory.CreateLineString(ToCoordinates(src.Line.StoredLinePoints()));
                case ProtoShape.KindOneofCase.Polygon:
                    return Factory.CreatePolygon(
                        Factory.CreateLinearRing(ToCoordinates(src.Polygon.Exterior.StoredRing())),
                        src.Polygon.Holes.Select(h => Factory.CreateLinearRing(ToCoordinates(h.StoredRing()))).ToArray());
                default:
                    throw new ArgumentException($"Shape case {src?.KindCase} has no stored NTS geometry", nameof(src));
            }
        }

        /// <summary>The SQL bounding polygon of a wire rectangle, for the region stored procedures.</summary>
        public static NtsGeometry ToNetTopologyGeometry(this Viking.gRPC.Geometry.V1.Rectangle src) =>
            Factory.ToGeometry(new Envelope(src.XMin, src.XMax, src.YMin, src.YMax));

        static GeoVector2[] ToVectors(Coordinate[] coordinates) =>
            coordinates.Select(c => new GeoVector2(c.X, c.Y)).ToArray();

        static Coordinate[] ToCoordinates(GeoVector2[] points) =>
            points.Select(p => new Coordinate(p.X, p.Y)).ToArray();
    }
}
