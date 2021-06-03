using System;
using System.Linq;

namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class Location
    {
        public static implicit operator Location(global::Viking.gRPC.AnnotationTypes.Location src)
        {
            var converted = new Location {
                ParentId = src.ParentID,
                Section = src.Section,
                Position = (Protos.AnnotationPoint)src.Position,
                VolumePosition = (Protos.AnnotationPoint)src.VolumePosition,
                //MosaicShapeWkb = ByteString.CopyFrom(src.MosaicShapeWKB),
                //VolumeShapeWkb = ByteString.CopyFrom(src.VolumeShapeWKB),
                Closed = src.Closed,
                AttributesXml = src.AttributesXml,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width ?? -1,
                TypeCode = (AnnotationType)(int)src.TypeCode,
                LastModified = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.FromFileTime(src.LastModified)),
                Username = src.Username,
            };
            
            converted.Links.AddRange(src.Links.Select(x => x));
            
            return converted;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.Location(Location src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.Location {
                ParentID = src.ParentId,
                Section = src.Section,
                Position = (global::Viking.gRPC.AnnotationTypes.AnnotationPoint)src.Position,
                VolumePosition = (global::Viking.gRPC.AnnotationTypes.AnnotationPoint)src.VolumePosition,
                //MosaicShapeWKB = src.MosaicShapeWkb.ToByteArray(),
                //VolumeShapeWKB = src.VolumeShapeWkb.ToByteArray(),
                Closed = src.Closed,
                AttributesXml = src.AttributesXml,
                Links = src.Links.ToArray(),
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width,
                TypeCode = (Viking.gRPC.AnnotationTypes.AnnotationType)((int)src.TypeCode),
                LastModified = src.LastModified.ToDateTime().Ticks,
                Username = src.Username,
            };
            return value;
        }

    }
}

