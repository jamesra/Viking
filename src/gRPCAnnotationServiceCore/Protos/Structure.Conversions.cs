using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Protos
{
    public partial class Structure
    {
        public static implicit operator Structure(global::AnnotationService.Types.Structure src)
        {
            var converted = new Structure {
                TypeId = src.TypeID,
                Notes = src.Notes,
                Verified = src.Verified,
                AttributesXml = src.AttributesXml,
                Confidence = src.Confidence,
                Label = src.Label,
                Username = src.Username,
            };
            
            converted.Links.AddRange(src.Links.Select(x => (Protos.StructureLink)x));
            converted.ChildIds.AddRange(src.ChildIDs.Select(x => x));
            
            return converted;
        }


        public static implicit operator global::AnnotationService.Types.Structure(Structure src)
        {
            var value = new global::AnnotationService.Types.Structure {
                TypeID = src.TypeId,
                Notes = src.Notes,
                Verified = src.Verified,
                AttributesXml = src.AttributesXml,
                Confidence = src.Confidence,
                Links = src.Links.Select(x => (global::AnnotationService.Types.StructureLink)x).ToArray(),
                ChildIDs = src.ChildIds.ToArray(),
                Label = src.Label,
                Username = src.Username,
            };
            return value;
        }

    }
}

