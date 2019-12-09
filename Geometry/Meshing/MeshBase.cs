using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    
    public interface IMesh<VERTEX>
        where VERTEX : IVertex
    {
        List<VERTEX> Verticies { get; }
        SortedList<IEdgeKey, IEdge> Edges { get; }
        SortedSet<IFace> Faces { get; }

        VERTEX this[long index] { get; }
        VERTEX this[int index] { get; }

        IEnumerable<VERTEX> this[IEnumerable<int> vertIndicies] { get; }
        IEnumerable<VERTEX> this[IEnumerable<long> vertIndicies] { get; }

        IEdge this[IEdgeKey key] { get; }

        bool Contains(IEdgeKey key);
        bool Contains(IFace key);

        bool Contains(int A, int B);

        /// <summary>
        /// Add vertex to the mesh
        /// </summary>
        /// <param name="v"></param>
        /// <returns>Index of vertex</returns>
        int AddVertex(VERTEX v);

        int AddVerticies(ICollection<VERTEX> verts);

        void AddEdge(int A, int B);

        void AddEdge(IEdgeKey e);

        void AddEdge(IEdge e);

        void RemoveEdge(IEdgeKey e);

        void AddFace(IFace face);

        void AddFace(int A, int B, int C);

        void AddFaces(ICollection<IFace> faces);

        void RemoveFace(IFace f);
    }

    public class MeshBase
    {

    }
}
