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
        public readonly SortedList<EdgeKey, Edge> Edges = new SortedList<EdgeKey, Edge>();
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
            EdgeKey e = new EdgeKey(A, B);
            AddEdge(e);
        }

        public void AddEdge(EdgeKey e)
        {
            if (Edges.ContainsKey(e))
                return;

            if (e.A >= Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            Edge newEdge = new Meshing.Edge(e); 
            Edges.Add(e, newEdge);

            Verticies[e.A].AddEdge(e);
            Verticies[e.B].AddEdge(e);
        }

        public void AddEdge(Edge e)
        {
            if (Edges.ContainsKey(e.Key))
                return;

            if (e.A >= Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            Edges.Add(e.Key, e);

            Verticies[e.A].AddEdge(e.Key);
            Verticies[e.B].AddEdge(e.Key);
        }

        /// <summary>
        /// Add a face. Creates edges if they aren't in the face
        /// </summary>
        /// <param name="face"></param>
        public void AddFace(Face face)
        {
            Debug.Assert(Faces.Contains(face) == false);
              
            foreach(EdgeKey e in face.Edges)
            {
                AddEdge(e);
                Edges[e].Faces.Add(face);
            }

            Faces.Add(face);
        }

        public void AddFace(int A, int B, int C)
        {
            Face face = new Face(A, B, C);
            Debug.Assert(Faces.Contains(face) == false);
              
            foreach (EdgeKey e in face.Edges)
            {
                AddEdge(e);
                Edges[e].Faces.Add(face);
            }

            Faces.Add(face);
        }

        public void AddFaces(ICollection<Face> faces)
        {
            foreach(Face f in faces)
            {
                AddFace(f);
            }
        }

        public void RemoveFace(Face f)
        {
            if(Faces.Contains(f))
            {
                Faces.Remove(f);
            }

            foreach(EdgeKey e in f.Edges)
            {
                Edge existing = Edges[e];
                existing.RemoveFace(f);
            }
        }

        public void RemoveEdge(EdgeKey e)
        {
            if(Edges.ContainsKey(e))
            {
                Edge removedEdge = Edges[e];

                Edges.Remove(e);

                this[removedEdge.A].RemoveEdge(e);
                this[removedEdge.B].RemoveEdge(e);
            }
        }

        public IEnumerable<Vertex> GetVerts(ICollection<int> vertIndicies)
        {
            return vertIndicies.Select(i => this.Verticies[i]);
        }

        public void ConvertAllFacesToTriangles()
        {
            IEnumerable<Face> quadFaces = this.Faces.Where(f => !f.IsTriangle).ToList();
            
            foreach (Face f in quadFaces)
            {
                this.SplitFace(f);
            }
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces...
        /// </summary>
        /// <returns></returns>
        public void SplitFace(Face face)
        {
            if (face.IsTriangle)
                return;

            if(face.IsQuad)
            {
                Faces.Remove(face);

                GridVector3[] positions = GetVerts(face.iVerts).Select(v => v.Position).ToArray();
                if(GridVector3.Distance(positions[0], positions[2]) < GridVector3.Distance(positions[1], positions[3]))
                {
                    Face ABC = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[2]);
                    Face ACD = new Face(face.iVerts[0], face.iVerts[2], face.iVerts[3]);

                    Faces.Add(ABC);
                    Faces.Add(ACD);
                }
                else
                {
                    Face ABD = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[3]);
                    Face BCD = new Face(face.iVerts[1], face.iVerts[2], face.iVerts[3]);

                    Faces.Add(ABD);
                    Faces.Add(BCD);
                }
            }
        }

        /// <summary>
        /// Recalculate normals based on the faces touching each vertex
        /// </summary>
        public void RecalculateNormals()
        {
            //Calculate normals for all faces
            Dictionary<Face, GridVector3> normals = new Dictionary<Meshing.Face, Geometry.GridVector3>(this.Faces.Count);

            for(int i = 0; i < Faces.Count; i++)
            {
                Face f = this.Faces.ElementAt(i);
                Vertex[] verticies = GetVerts(f.iVerts).ToArray();
                GridVector3 normal = GridVector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
                normals.Add(f, normal);
            }

            for(int i = 0; i < Verticies.Count; i++)
            {
                SortedSet<Face> vertFaces = new SortedSet<Meshing.Face>();
                Vertex v = Verticies[i];
                
                foreach(EdgeKey ek in v.Edges)
                {
                    vertFaces.UnionWith(Edges[ek].Faces);
                }

                GridVector3 avgNormal = GridVector3.Zero;
                foreach(Face f in vertFaces)
                {
                    avgNormal += normals[f];
                }

                avgNormal.Normalize();

                v.Normal = avgNormal;                
            }
        }
    }
}
