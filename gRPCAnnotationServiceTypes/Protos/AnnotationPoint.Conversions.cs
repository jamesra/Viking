namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public partial class AnnotationPoint
    {
        public static implicit operator AnnotationPoint(global::Geometry.Vector3 src)
        {
            return new AnnotationPoint
            {
                X = src.X,
                Y = src.Y,
                Z = src.Z,
            };
        }
            
        public static implicit operator AnnotationPoint(global::Geometry.Vector2 src)
        { 
            return new AnnotationPoint
            {
                X = src.X,
                Y = src.Y
            }; 
        }


        public static implicit operator global::Geometry.Vector2(AnnotationPoint src)
        {
            return new global::Geometry.Vector2(src.X, src.Y);
        }

        public static implicit operator global::Geometry.Vector3(AnnotationPoint src)
        {
            return new global::Geometry.Vector3(src.X, src.Y, src.Z);
        }
    }
}

