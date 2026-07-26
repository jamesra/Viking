using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationServiceCore.Client.Protos
{
    public partial class Structure
    {
        public static implicit operator Structure(global::gRPCAnnotationServiceCore.Client.Structure src)
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


        public static implicit operator global::gRPCAnnotationServiceCore.Client.Structure(Structure src)
        {
            var value = new global::gRPCAnnotationServiceCore.Client.Structure {
                TypeID = src.TypeId,
                Notes = src.Notes,
                Verified = src.Verified,
                AttributesXml = src.AttributesXml,
                Confidence = src.Confidence,
                Links = src.Links.Select(x => (global::gRPCAnnotationServiceCore.Client.StructureLink)x).ToArray(),
                ChildIDs = src.ChildIds.ToArray(),
                Label = src.Label,
                Username = src.Username,
            };
            return value;
        }

    }
}

