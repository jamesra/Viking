namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class LocationLink
    {
        public static implicit operator LocationLink(global::Viking.gRPC.AnnotationTypes.LocationLink src)
        {
            var value = new LocationLink {
                SourceId = src.SourceID,
                TargetId = src.TargetID,
            };
            return value;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.LocationLink(LocationLink src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.LocationLink {
                SourceID = src.SourceId,
                TargetID = src.TargetId,
            };
            return value;
        }

    }
}

