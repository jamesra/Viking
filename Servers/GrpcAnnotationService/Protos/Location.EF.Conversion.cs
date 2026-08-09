using System.Linq;
using NetTopologySuite.Geometries;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;
using ProtoGeometry = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry;

namespace gRPCAnnotationService.Protos
{
    public static class LocationEFExtensions
    {
        public static Viking.DataModel.Annotation.Location ToLocation(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var mosaicShape = ToNetTopologyGeometry(src.MosaicShape)
                ?? PointGeometry(src.MosaicPosition);
            var volumeShape = ToNetTopologyGeometry(src.VolumeShape)
                ?? PointGeometry(src.VolumePosition)
                ?? mosaicShape;

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

            converted.LocationLinkANavigations = src.Links.Where(l => l > src.Id)
                                                                 .Select(x => new Viking.DataModel.Annotation.LocationLink() { A = src.Id, B = x })
                                                                 .ToList();
            converted.LocationLinkBNavigations = src.Links.Where(l => l < src.Id)
                                                                 .Select(x => new Viking.DataModel.Annotation.LocationLink() { A = x, B = src.Id })
                                                                 .ToList();

            return converted;
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
                MosaicShape = src.MosaicShape?.ToProtobufMessage(),
                VolumeShape = src.VolumeShape?.ToProtobufMessage(),
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
