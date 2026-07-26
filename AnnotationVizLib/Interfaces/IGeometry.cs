using Geometry;

namespace AnnotationVizLib
{
    public interface IGeometry
    {
        Microsoft.SqlServer.Types.SqlGeometry Geometry { get; set; }

        double Z { get; }

        GridBox BoundingBox { get; }
    }
}
