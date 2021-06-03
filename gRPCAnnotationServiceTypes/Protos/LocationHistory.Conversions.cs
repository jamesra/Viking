namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class LocationHistory
    {
        public static implicit operator LocationHistory(global::Viking.gRPC.AnnotationTypes.LocationHistory src)
        {
            var value = new LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.LocationHistory(LocationHistory src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.LocationHistory {
                ChangedColumnMask = src.ChangedColumnMask,
            };
            return value;
        }

    }
}

