using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Geometry.Meshing
{
    /// <summary>
    /// Base class for geometry exceptions
    /// </summary>
    public abstract class GeometryMeshExceptionBase : Exception
    {
        protected GeometryMeshExceptionBase()
        {
        }

        protected GeometryMeshExceptionBase(string message) : base(message)
        {
        }

        protected GeometryMeshExceptionBase(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GeometryMeshExceptionBase(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// This exception is raised when a corresponding point perfectly a vertex that is not an endpoint of the edge.
    /// </summary>
    internal class EdgeIntersectsVertexException : GeometryMeshExceptionBase
    {
        public int Vertex;

        public EdgeIntersectsVertexException(int iVert) : base()
        {
            Vertex = iVert;
        }

        public EdgeIntersectsVertexException(int iVert, string msg) : base(msg)
        {
            Vertex = iVert;
        }
    }

    /// <summary>
    /// Thrown when a delaunay triangulation does not conform to the delaunay requirements
    /// </summary>
    public class NonconformingTriangulationException : GeometryMeshExceptionBase
    {
        public IFace Face;

        public NonconformingTriangulationException(IFace face, string msg) : base(msg)
        {
            Face = face;
        }

        public NonconformingTriangulationException(IFace face, string message, Exception innerException) : base(message, innerException)
        {
            Face = face;
        }
    }

    /// <summary>
    /// Thrown when a delaunay triangulation does not conform to the delaunay requirements
    /// </summary>
    public class EdgesIntersectTriangulationException : GeometryMeshExceptionBase
    {
        public IEdgeKey Edge;
        public IEdgeKey[] IntersectedEdges;

        public EdgesIntersectTriangulationException(IEdgeKey edge, ICollection<IEdgeKey> intersected, string msg) : base(msg)
        {
            Edge = edge;
            IntersectedEdges = intersected.ToArray();
        }

        public EdgesIntersectTriangulationException(IEdgeKey edge, ICollection<IEdgeKey> intersected, string message, Exception innerException) : base(message, innerException)
        {
            Edge = edge;
            IntersectedEdges = intersected.ToArray();
        }
    }

}
