using AnnotationService.Types;

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
                XMin = src.Left,
                YMin = src.Bottom,
                XMax = src.Right,
                YMax = src.Top,
            };
            return value;
        }



        public static implicit operator global::Geometry.GridRectangle(BoundingRectangle src)
        {
            var value = new global::Geometry.GridRectangle(
                left: src.XMin,
                right: src.XMax,
                bottom: src.YMin,
                top: src.YMax
            );
            return value;
        }

        // Conversion from protobuf-net BoundingRectangle to Google.Protobuf BoundingRectangle
        public static implicit operator BoundingRectangle(global::AnnotationService.Types.BoundingRectangle src)
        {
            return new BoundingRectangle { 
                XMin = src.XMin,
                YMin = src.YMin,
                XMax = src.XMax,
                YMax = src.YMax,
            };
        }

        // Conversion from Google.Protobuf BoundingRectangle to protobuf-net BoundingRectangle
        public static implicit operator global::AnnotationService.Types.BoundingRectangle(BoundingRectangle src)
        {
            return new global::AnnotationService.Types.BoundingRectangle(
                xmin: src.XMin,
                ymin: src.YMin,
                xmax: src.XMax,
                ymax: src.YMax
            );
        }

    }
}

