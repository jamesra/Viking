using System.Linq;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;

namespace gRPCAnnotationService.Protos
{
    public static class LocationEFExtensions
    {
        public static Viking.DataModel.Annotation.Location ToLocation(this global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location src)
        {
            var converted = new Viking.DataModel.Annotation.Location
            {
                Id = src.Id,
                ParentId = src.ParentId,
                Z = src.Section,
                //VolumeShape = src.VolumeShape.ToNetTopologyGeometry(),
                //MosaicShape = src.MosaicShape.ToNetTopologyGeometry(),
                X = src.MosaicPosition.X,
                Y = src.MosaicPosition.Y,
                VolumeX = src.VolumePosition.X,
                VolumeY = src.VolumePosition.Y,
                Closed = src.Closed,
                Tags = src.Attributes,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width ?? -1,
                TypeCode = (short)src.TypeCode,
                LastModified = src.LastModified.ToDateTime(),
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
                MosaicPosition = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotationPoint { X = src.X, Y = src.Y, Z = src.Z },
                VolumePosition = new global::Viking.AnnotationServiceTypes.gRPC.V1.Protos.AnnotationPoint { X = src.VolumeX, Y = src.VolumeY, Z = src.Z },
                //MosaicShape = src.MosaicShape.ToProtobufMessage(), 
                //VolumeShape = src.VolumeShape.ToProtobufMessage(),
                Closed = src.Closed,
                Attributes = src.Tags,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width,
                TypeCode = (AnnotationType)(short)src.TypeCode,
                LastModified = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(src.LastModified),
                Username = src.Username,
            };

            value.Links.AddRange(compositeLinks.Select(ll => ll.A == src.Id ? ll.B : ll.A));

            return value;
        }

    }
}
