namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
    public static class BoundingRectangleExtensions
    {
        public static BoundingRectangle ToBoundingRectangle(this global::Geometry.IRectangle src)
        {
            var value = new BoundingRectangle
            {
                Xmin = src.Left,
                Ymin = src.Bottom,
                Xmax = src.Right,
                Ymax = src.Top,
            };
            return value;
        }
    }

    public partial class BoundingRectangle
    {
        public static implicit operator BoundingRectangle(global::Geometry.GridRectangle src)
        {
            var value = new BoundingRectangle { 
                Xmin = src.Left,
                Ymin = src.Bottom,
                Xmax = src.Right,
                Ymax = src.Top,
            };
            return value;
        }



        public static implicit operator global::Geometry.GridRectangle(BoundingRectangle src)
        {
            var value = new global::Geometry.GridRectangle(
                left: src.Xmin,
                right: src.Xmax,
                bottom: src.Ymin,
                top: src.Ymax
            );
            return value;
        }

    }
}

