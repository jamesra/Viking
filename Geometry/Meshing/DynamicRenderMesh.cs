using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    public class DynamicRenderMesh<T> : DynamicRenderMesh
    {
        
        public new Vertex<T> this[int key]
        {
            get
            {
                return Verticies[key] as Vertex<T>;
            }
            set
            {
                Verticies[key] = value;
            }
        }
    }

    public class DynamicRenderMesh
    {
        public readonly List<Vertex> Verticies = new List<Vertex>();
        public readonly SortedSet<Edge> Edges = new SortedSet<Edge>();
        public readonly SortedSet<Face> Faces = new SortedSet<Face>();

        public virtual Vertex this[int key]
        {
            get
            {
                return Verticies[key] as Vertex;
            }
            set
            {
                Verticies[key] = value;
            }
        }

        public DynamicRenderMesh()
        {

        }

        public int AddVertex(Vertex v)
        {
            Verticies.Add(v);
            return Verticies.Count - 1; 
        }

        /// <summary>
        /// Add a collection of verticies to the mesh
        /// </summary>
        /// <param name="v"></param>
        /// <returns>The index the first element was inserted at</returns>
        public int AddVertex(ICollection<Vertex> v)
        {
            int iStart = Verticies.Count;
            Verticies.AddRange(v);
            return iStart;
        }

        public void AddEdge(int A, int B)
        {
            Edge e = new Edge(A, B);
            Edges.Add(e);
        }

        public void AddEdge(Edge e)
        {
            Debug.Assert(Edges.Contains(e) == false);
            Edges.Add(e);

            Verticies[e.A].AddEdge(e);
            Verticies[e.B].AddEdge(e);
        }

        /// <summary>
        /// Add a face. Creates edges if they aren't in the face
        /// </summary>
        /// <param name="f"></param>
        public void AddFace(Face f)
        {
            Debug.Assert(Faces.Contains(f) == false);

            foreach(Edge e in f.Edges)
            {
                if (!Edges.Contains(e))
                    Edges.Add(e);
            }

            Faces.Add(f);
        }
    }
}
