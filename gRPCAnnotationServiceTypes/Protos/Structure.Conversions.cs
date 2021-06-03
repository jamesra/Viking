using System.Linq;

namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class Structure
    {
        public static implicit operator Structure(global::Viking.gRPC.AnnotationTypes.Structure src)
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
            
            converted.Links.AddRange(src.Links.Select(x => (Viking.gRPC.AnnotationTypes.V1.Protos.StructureLink)x));
            converted.ChildIds.AddRange(src.ChildIDs.Select(x => x));
            
            return converted;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.Structure(Structure src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.Structure {
                TypeID = src.TypeId,
                Notes = src.Notes,
                Verified = src.Verified,
                AttributesXml = src.AttributesXml,
                Confidence = src.Confidence,
                Links = src.Links.Select(x => (global::Viking.gRPC.AnnotationTypes.StructureLink)x).ToArray(),
                ChildIDs = src.ChildIds.ToArray(),
                Label = src.Label,
                Username = src.Username,
            };
            return value;
        }

    }
}

