using System;
using System.Linq;
using System.Text.RegularExpressions;
using Geometry;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;

namespace gRPCAnnotationService.Protos
{
    /// <summary>
    /// Location proto ↔ EF. Circles are POINT in EF (NTS cannot read SQL CurvePolygon);
    /// PersistCircleShapesAsync writes the real CURVEPOLYGON after SaveChanges.
    /// ToProtobufMessage rebuilds circle WKT from X/Y/Radius because the interceptor stripped the shape.
    /// </summary>
    public static class LocationEFExtensions
    {
        private static readonly Regex CircularStringKeyword = new(
            @"CIRCULARSTRING\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static Viking.DataModel.Annotation.Location ToLocation(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            NtsGeometry mosaicShape;
            NtsGeometry volumeShape;

            if (src.TypeCode == AnnotationType.Circle)
            {
                var (mosaic, volume) = ResolveCircleShapes(src);
                mosaicShape = new Point(mosaic.Center.X, mosaic.Center.Y);
                volumeShape = new Point(volume.Center.X, volume.Center.Y);
            }
            else
            {
                mosaicShape = ToNetTopologyGeometry(src.MosaicShape)
                    ?? PointGeometry(src.MosaicPosition);
                volumeShape = ToNetTopologyGeometry(src.VolumeShape)
                    ?? PointGeometry(src.VolumePosition)
                    ?? mosaicShape;
            }

            var converted = new Viking.DataModel.Annotation.Location
            {
                Id = src.Id,
                ParentId = src.ParentId,
                Z = src.Section,
                MosaicShape = mosaicShape,
                VolumeShape = volumeShape,
                Closed = src.Closed,
                Tags = string.IsNullOrWhiteSpace(src.Attributes) ? null : src.Attributes,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Width = src.Width,
                TypeCode = (short)src.TypeCode,
                // Store Save omits LastModified; ApplyUpdate stamps UtcNow.
                LastModified = src.LastModified?.ToDateTime() ?? default,
                Username = src.Username,
            };

            // Links are persisted after identity is assigned (CreateLocation / AddLinkIfMissing).
            // Mapping them here on create (Id == 0) would insert LocationLink rows keyed by 0.

            return converted;
        }

        /// <summary>
        /// CURVEPOLYGON WKT to persist via SQL after SaveChanges. Mosaic radius comes from
        /// proto Radius or the mosaic circle; volume radius comes only from VolumeShape.
        /// </summary>
        public static (string MosaicWkt, string VolumeWkt) CircleShapeWkt(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var (mosaic, volume) = ResolveCircleShapes(src);
            return (
                GeometryExtensions.ToCircleWKT(mosaic.Center.X, mosaic.Center.Y, mosaic.Radius),
                GeometryExtensions.ToCircleWKT(volume.Center.X, volume.Center.Y, volume.Radius));
        }


        public static global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location ToProtobufMessage(this Viking.DataModel.Annotation.Location src)
        {
            var compositeLinks = src.LocationLinkANavigations.ToList();
            compositeLinks.AddRange(src.LocationLinkBNavigations);

            var value = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location
            {  
                Id = src.Id,
                ParentId = src.ParentId,
                Section = src.Z,
                MosaicPosition = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotationPoint { X = src.X, Y = src.Y },
                VolumePosition = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotationPoint { X = src.VolumeX, Y = src.VolumeY },
                MosaicShape = ShapeToProtobuf(src, mosaic: true),
                VolumeShape = ShapeToProtobuf(src, mosaic: false),
                Closed = src.Closed,
                Attributes = src.Tags ?? string.Empty,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width,
                TypeCode = (AnnotationType)(short)src.TypeCode,
                LastModified = ToUtcTimestamp(src.LastModified),
                Username = src.Username,
            };

            value.Links.AddRange(compositeLinks.Select(ll => ll.A == src.Id ? ll.B : ll.A));

            return value;
        }

        private static Google.Protobuf.WellKnownTypes.Timestamp ToUtcTimestamp(System.DateTime dateTime) =>
            Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                System.DateTime.SpecifyKind(dateTime, System.DateTimeKind.Utc));

        private static ProtoGeometry ShapeToProtobuf(Viking.DataModel.Annotation.Location src, bool mosaic)
        {
            if (src.TypeCode == (short)AnnotationType.Circle)
            {
                var wkt = mosaic
                    ? GeometryExtensions.ToCircleWKT(src.X, src.Y, src.Radius)
                    : GeometryExtensions.ToCircleWKT(src.VolumeX, src.VolumeY, src.Radius);
                return new ProtoGeometry { Text = wkt };
            }

            var shape = mosaic ? src.MosaicShape : src.VolumeShape;
            return shape?.ToProtobufMessage();
        }

        private static (Circle Mosaic, Circle Volume) ResolveCircleShapes(
            global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var mosaic = ResolveMosaicCircle(src);
            var volume = ResolveVolumeCircle(src);
            return (mosaic, volume);
        }

        private static Circle ResolveMosaicCircle(global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var parsed = TryParseCircle(src.MosaicShape, out var fromShape);

            Vector2 center;
            if (src.MosaicPosition != null)
                center = new Vector2(src.MosaicPosition.X, src.MosaicPosition.Y);
            else if (parsed)
                center = fromShape.Center;
            else
                throw new ArgumentException("Circle locations require MosaicPosition, or a CURVEPOLYGON mosaic shape.");

            var radius = src.Radius > 0 ? src.Radius : parsed ? fromShape.Radius : 0;
            if (radius <= 0)
                throw new ArgumentException("Circle locations require a mosaic radius.");

            return new Circle(center, radius);
        }

        private static Circle ResolveVolumeCircle(global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            if (!TryParseCircle(src.VolumeShape, out var parsed) || parsed.Radius <= 0)
                throw new ArgumentException("Circle locations require a CURVEPOLYGON volume shape with a radius.");

            if (src.VolumePosition != null)
                return new Circle(src.VolumePosition.X, src.VolumePosition.Y, parsed.Radius);

            return parsed;
        }

        private static bool TryParseCircle(ProtoGeometry geometry, out Circle circle)
        {
            circle = default;
            var wkt = GeometryText(geometry);
            if (string.IsNullOrWhiteSpace(wkt))
                return false;

            // GeometryExtensions.ToCircleWKT emits CIRCULARSTRING; ParseWKT only
            // understands the five-point CURVEPOLYGON ring Viking uses for circles.
            var normalized = CircularStringKeyword.Replace(wkt, string.Empty);

            try
            {
                if (normalized.ParseWKT() is ICircle2D parsed && parsed.Radius > 0)
                {
                    circle = new Circle(parsed.Center, parsed.Radius);
                    return true;
                }
            }
            catch (FormatException)
            {
                return false;
            }

            return false;
        }

        private static string GeometryText(ProtoGeometry geometry)
        {
            if (geometry == null ||
                geometry.EncodingCase == ProtoGeometry.EncodingOneofCase.None)
                return null;

            if (geometry.EncodingCase == ProtoGeometry.EncodingOneofCase.Text)
                return geometry.Text;

            return null;
        }

        private static NtsGeometry ToNetTopologyGeometry(ProtoGeometry geometry)
        {
            if (geometry == null ||
                geometry.EncodingCase == ProtoGeometry.EncodingOneofCase.None)
                return null;

            return geometry.ToNetTopologyGeometry();
        }

        private static NtsGeometry PointGeometry(AnnotationPoint point)
        {
            if (point == null)
                return null;

            return new Point(point.X, point.Y);
        }
    }
}
