using System;
using System.Linq;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using ProtoCircle = Viking.gRPC.Geometry.V1.Circle;
using ProtoCoordinateSpace = Viking.gRPC.Geometry.V1.CoordinateSpace;
using ProtoShape = Viking.gRPC.Geometry.V1.Shape;
using ProtoVector2 = Viking.gRPC.Geometry.V1.Vector2;

namespace gRPCAnnotationService.Protos
{
    /// <summary>
    /// Location proto ↔ EF. SQL geometry is converted here and in <see cref="NetTopologyGeometryExtensions"/> only.
    /// </summary>
    /// <remarks>
    /// NTS cannot read or write SQL CurvePolygon, so a circle's shape columns hold its <b>east rim point</b> in EF:
    /// <see cref="Viking.DataModel.Annotation.SqlServerCircleShapeCommandInterceptor"/> substitutes it on read (from the
    /// stored curve's area), the region stored-procedure reader derives it from the stored curve, and
    /// <see cref="ToLocation"/> sets it before <see cref="Viking.DataModel.Annotation.AnnotationContext.PersistCircleShapesAsync"/>
    /// writes the real CURVEPOLYGON from <see cref="CircleShapeWkt"/>. The radius in each space is the distance from
    /// that rim point to the space's centre (X, Y or VolumeX, VolumeY), so the volume circle keeps its own radius.
    /// </remarks>
    public static class LocationEFExtensions
    {
        /// <summary>
        /// Builds the EF location from the proto. The type comes from the mosaic shape; the volume shape falls back to
        /// the mosaic shape when absent. Circles store their east rim points here (see the class remarks).
        /// </summary>
        /// <exception cref="ArgumentException">The location has no mosaic shape or position, or is a circle without a
        /// positive mosaic and volume radius.</exception>
        public static Viking.DataModel.Annotation.Location ToLocation(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var mosaic = src.MosaicGeometry;
            if (mosaic == null || mosaic.KindCase == ProtoShape.KindOneofCase.None)
            {
                if (src.MosaicPosition == null)
                    throw new ArgumentException("Locations require a mosaic shape.");
                mosaic = AnnotationShapes.PointShape(src.MosaicPosition.X, src.MosaicPosition.Y);
            }

            var volume = src.VolumeGeometry;
            if (volume == null || volume.KindCase == ProtoShape.KindOneofCase.None)
                volume = src.VolumePosition != null ? AnnotationShapes.PointShape(src.VolumePosition.X, src.VolumePosition.Y) : mosaic;

            var type = mosaic.LocationTypeOf();

            NtsGeometry mosaicShape;
            NtsGeometry volumeShape;
            if (type == LocationType.CIRCLE)
            {
                // The volume circle must be sent: falling back to the mosaic circle would copy its radius.
                var (mosaicCircle, volumeCircle) = ResolveCircles(mosaic, src.VolumeGeometry);
                mosaicShape = EastRimPoint(mosaicCircle);
                volumeShape = EastRimPoint(volumeCircle);
            }
            else
            {
                mosaicShape = mosaic.ToNetTopologyGeometry();
                volumeShape = volume.ToNetTopologyGeometry();
            }

            var converted = new Viking.DataModel.Annotation.Location
            {
                Id = src.Id,
                ParentId = src.ParentId,
                Z = src.Section,
                MosaicShape = mosaicShape,
                VolumeShape = volumeShape,
                Closed = mosaic.KindCase == ProtoShape.KindOneofCase.Line && mosaic.Line.Closed,
                Tags = string.IsNullOrWhiteSpace(src.Attributes) ? null : src.Attributes,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Width = mosaic.WidthOf(),
                TypeCode = (short)type,
                // Store Save omits LastModified; ApplyUpdate stamps UtcNow.
                LastModified = src.LastModified?.ToDateTime() ?? default,
                Username = src.Username,
            };

            // Links are persisted after identity is assigned (CreateLocation / AddLinkIfMissing).
            // Mapping them here on create (Id == 0) would insert LocationLink rows keyed by 0.

            return converted;
        }

        /// <summary>True when the proto is a circle, whose CURVEPOLYGON must be written by SQL after SaveChanges.</summary>
        public static bool IsCircle(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src) =>
            src.MosaicGeometry?.KindCase == ProtoShape.KindOneofCase.Circle;

        /// <summary>
        /// CURVEPOLYGON WKT to persist via SQL after SaveChanges: the mosaic circle and the volume circle, each with its
        /// own radius.
        /// </summary>
        public static (string MosaicWkt, string VolumeWkt) CircleShapeWkt(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var (mosaic, volume) = ResolveCircles(src.MosaicGeometry, src.VolumeGeometry);
            return (
                GeometryExtensions.ToCircleWKT(mosaic.Center.X, mosaic.Center.Y, mosaic.Radius),
                GeometryExtensions.ToCircleWKT(volume.Center.X, volume.Center.Y, volume.Radius));
        }

        public static global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location ToProtobufMessage(this Viking.DataModel.Annotation.Location src)
        {
            var compositeLinks = src.LocationLinkANavigations.ToList();
            compositeLinks.AddRange(src.LocationLinkBNavigations);

            var type = (LocationType)src.TypeCode;
            ProtoShape mosaicGeometry;
            ProtoShape volumeGeometry;
            if (type == LocationType.CIRCLE)
            {
                // The shape columns hold the east rim point (see the class remarks); the mosaic radius column is the
                // fallback for a row read without it.
                var mosaicRadius = RimRadius(src.MosaicShape, src.X, src.Y);
                mosaicGeometry = AnnotationShapes.CircleShape(src.X, src.Y, mosaicRadius > 0 ? mosaicRadius : src.Radius);
                volumeGeometry = AnnotationShapes.CircleShape(src.VolumeX, src.VolumeY, RimRadius(src.VolumeShape, src.VolumeX, src.VolumeY));
            }
            else
            {
                mosaicGeometry = src.MosaicShape.ToShape(type, ProtoCoordinateSpace.Mosaic, src.Width);
                volumeGeometry = src.VolumeShape.ToShape(type, ProtoCoordinateSpace.Volume, src.Width);
            }

            var value = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location
            {
                Id = src.Id,
                ParentId = src.ParentId,
                Section = src.Z,
                MosaicPosition = new ProtoVector2 { X = src.X, Y = src.Y },
                VolumePosition = new ProtoVector2 { X = src.VolumeX, Y = src.VolumeY },
                MosaicGeometry = mosaicGeometry,
                VolumeGeometry = volumeGeometry,
                Attributes = src.Tags ?? string.Empty,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                LastModified = ToUtcTimestamp(src.LastModified),
                Username = src.Username,
            };

            value.Links.AddRange(compositeLinks.Select(ll => ll.A == src.Id ? ll.B : ll.A));

            return value;
        }

        private static Google.Protobuf.WellKnownTypes.Timestamp ToUtcTimestamp(System.DateTime dateTime) =>
            Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(
                System.DateTime.SpecifyKind(dateTime, System.DateTimeKind.Utc));

        /// <summary>Distance from a circle's rim point (its shape column in EF) to its centre; 0 when the column is not a point.</summary>
        private static double RimRadius(NtsGeometry rim, double centerX, double centerY)
        {
            if (rim is not Point point)
                return 0;

            double dx = point.X - centerX;
            double dy = point.Y - centerY;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static Point EastRimPoint(ProtoCircle circle) => new(circle.Center.X + circle.Radius, circle.Center.Y);

        private static (ProtoCircle Mosaic, ProtoCircle Volume) ResolveCircles(ProtoShape mosaic, ProtoShape volume)
        {
            if (mosaic?.KindCase != ProtoShape.KindOneofCase.Circle || mosaic.Circle.Center == null || !(mosaic.Circle.Radius > 0))
                throw new ArgumentException("Circle locations require a mosaic circle with a centre and a positive radius.");

            // Never substitute the mosaic radius: the volume radius differs by the section's scale.
            if (volume?.KindCase != ProtoShape.KindOneofCase.Circle || volume.Circle.Center == null || !(volume.Circle.Radius > 0))
                throw new ArgumentException("Circle locations require a volume circle with a centre and a positive radius.");

            return (mosaic.Circle, volume.Circle);
        }
    }
}
