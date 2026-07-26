using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AnnotationService.WcfProxy.Protos
{
    public partial class Location
    {
        public static implicit operator Location(global::AnnotationService.WcfProxy.Location src)
        {
            var converted = new Location {
                ParentId = src.ParentID,
                Section = src.Section,
                Position = (Protos.AnnotationPoint)src.Position,
                VolumePosition = (Protos.AnnotationPoint)src.VolumePosition,
                MosaicShapeWkb = ByteString.CopyFrom(src.MosaicShapeWKB),
                VolumeShapeWkb = ByteString.CopyFrom(src.VolumeShapeWKB),
                Closed = src.Closed,
                AttributesXml = src.AttributesXml,
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width,
                TypeCode = (int)src.TypeCode,
                LastModified = src.LastModified,
                Username = src.Username,
            };
            
            converted.Links.AddRange(src.Links.Select(x => x));
            
            return converted;
        }


        public static implicit operator global::AnnotationService.WcfProxy.Location(Location src)
        {
            var value = new global::AnnotationService.WcfProxy.Location {
                ParentID = src.ParentId,
                Section = src.Section,
                Position = (global::AnnotationService.WcfProxy.AnnotationPoint)src.Position,
                VolumePosition = (global::AnnotationService.WcfProxy.AnnotationPoint)src.VolumePosition,
                MosaicShapeWKB = src.MosaicShapeWkb.ToByteArray(),
                VolumeShapeWKB = src.VolumeShapeWkb.ToByteArray(),
                Closed = src.Closed,
                AttributesXml = src.AttributesXml,
                Links = src.Links.ToArray(),
                Terminal = src.Terminal,
                OffEdge = src.OffEdge,
                Radius = src.Radius,
                Width = src.Width,
                TypeCode = (short)src.TypeCode,
                LastModified = src.LastModified,
                Username = src.Username,
            };
            return value;
        }

    }
}

