using Geometry;

namespace AnnotationVizLib
{
    public interface IGeometry
    {
        IShape2D Geometry { get; set; }

        double Z { get; }

        GridBox BoundingBox { get; }
    }
}
