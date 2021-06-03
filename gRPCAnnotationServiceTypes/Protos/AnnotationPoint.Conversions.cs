namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class AnnotationPoint
    {
        public static implicit operator AnnotationPoint(global::Viking.gRPC.AnnotationTypes.AnnotationPoint src)
        {
            var value = new AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.AnnotationPoint(AnnotationPoint src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.AnnotationPoint {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
            return value;
        }

    }
}

