namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class StructureLink
    {
        public static implicit operator StructureLink(global::Viking.gRPC.AnnotationTypes.StructureLink src)
        {
            var converted = new StructureLink
            { 
               SourceId = src.SourceID,
               TargetId = src.TargetID,
               Bidirectional = src.Bidirectional,
               Tags = src.Tags,
               Username = src.Username
            };
             
            return converted;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.StructureLink(StructureLink src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.StructureLink
            {
                SourceID = src.SourceId,
                TargetID = src.TargetId,
                Bidirectional = src.Bidirectional,
                Tags = src.Tags,
                Username = src.Username
            };
            return value;
        }

    }
}

