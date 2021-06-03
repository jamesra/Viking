namespace Viking.gRPC.AnnotationTypes.V1.Protos
{
    public partial class BoundingRectangle
    {
        public static implicit operator BoundingRectangle(global::Viking.gRPC.AnnotationTypes.BoundingRectangle src)
        {
            var value = new BoundingRectangle {
                Xmin = src.XMin,
                Ymin = src.YMin,
                Xmax = src.XMax,
                Ymax = src.YMax,
            };
            return value;
        }


        public static implicit operator global::Viking.gRPC.AnnotationTypes.BoundingRectangle(BoundingRectangle src)
        {
            var value = new global::Viking.gRPC.AnnotationTypes.BoundingRectangle {
                XMin = src.Xmin,
                YMin = src.Ymin,
                XMax = src.Xmax,
                YMax = src.Ymax,
            };
            return value;
        }

    }
}

