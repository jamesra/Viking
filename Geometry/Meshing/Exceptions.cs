using System;
using Geometry;
using Geometry.Meshing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace Geometry.Meshing
{
    /// <summary>
    /// Base class for geometry exceptions
    /// </summary>
    public abstract class GeometryMeshExceptionBase : Exception
    {
        public GeometryMeshExceptionBase()
        {
        }

        public GeometryMeshExceptionBase(string message) : base(message)
        {
        }

        public GeometryMeshExceptionBase(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected GeometryMeshExceptionBase(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

    /// <summary>
    /// This exception is raised when a corresponding point perfectly a vertex that is not an endpoint of the edge.
    /// </summary>
    internal class CorrespondingEdgeIntersectsVertexException : GeometryMeshExceptionBase
    {
        public int Vertex;

        public CorrespondingEdgeIntersectsVertexException(int iVert) : base()
        {
            Vertex = iVert;
        }

        public CorrespondingEdgeIntersectsVertexException(int iVert, string msg) : base(msg)
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

}
