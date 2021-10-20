namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class AnnotationPoint
    {
        public static implicit operator AnnotationPoint(global::Geometry.GridVector3 src)
        {
            return new AnnotationPoint
            {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
        }
            
        public static implicit operator AnnotationPoint(global::Geometry.GridVector2 src)
        { 
            return new AnnotationPoint
            {
                X = src.X,
                Y = src.Y
            }; 
        }


        public static implicit operator global::Geometry.GridVector2(AnnotationPoint src)
        {
            return new global::Geometry.GridVector2(src.X, src.Y);
        }

        public static implicit operator global::Geometry.GridVector3(AnnotationPoint src)
        {
            return new global::Geometry.GridVector3(src.X, src.Y, src.Z);
        }
    }
}

