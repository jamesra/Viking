namespace Viking.AnnotationServiceTypes.gRPC.V1.Protos
{
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
            if (src == null) return null;
            
            var value = new BoundingRectangle { 
                XMin = src.XMin,
                YMin = src.YMin,
                XMax = src.XMax,
                YMax = src.YMax,
            };
            return value;
        }

        // Conversion from Google.Protobuf BoundingRectangle to protobuf-net BoundingRectangle
        public static implicit operator global::AnnotationService.Types.BoundingRectangle(BoundingRectangle src)
        {
            if (src == null) return null;
            
            var value = new global::AnnotationService.Types.BoundingRectangle(
                xmin: src.XMin,
                ymin: src.YMin,
                xmax: src.XMax,
                ymax: src.YMax
            );
            return value;
        }

    }
}

