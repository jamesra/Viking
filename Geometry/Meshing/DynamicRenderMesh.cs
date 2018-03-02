using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{
    /*
    public class DynamicRenderMesh : DynamicRenderMesh<Vertex, Edge, Face>
    {
        public int Append(DynamicRenderMesh other)
        {
            return base.Append(other, Vertex.Duplicate, Edge.Duplicate, Face.Duplicate);
        }

        public void ConvertAllFacesToTriangles()
        {
            base.ConvertAllFacesToTriangles(Face.Duplicate);
        }
    }*/

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
        public readonly List<IVertex> Verticies = new List<IVertex>();
        public readonly SortedList<IEdgeKey, IEdge> Edges = new SortedList<IEdgeKey, IEdge>();
        public readonly SortedSet<IFace> Faces = new SortedSet<IFace>();

        public Func<IVertex,int,IVertex> DuplicateVertex { get; set; }
        public Func<IEdge, int, int, IEdge> DuplicateEdge { get; set; }

        public Func<IFace, IEnumerable<int>, IFace> DuplicateFace { get; set; }

        public GridBox BoundingBox = null;

        public virtual IVertex this[int key]
        {
            get
            {
                return Verticies[key];
            }
            set
            {
                Verticies[key] = value;
            }
        }

        public virtual IVertex this[long key]
        {
            get
            {
                return Verticies[(int)key];
            }
            set
            {
                Verticies[(int)key] = value;
            }
        }

        public DynamicRenderMesh()
        {
            DuplicateVertex = Vertex.Duplicate;
            DuplicateEdge = Edge.Duplicate;
            DuplicateFace = Face.Duplicate;
        }

        protected void ValidateBoundingBox()
        {
            Debug.Assert(BoundingBox.MinCorner.X == this.Verticies.Select(v => v.Position.X).Min());
            Debug.Assert(BoundingBox.MinCorner.Y == this.Verticies.Select(v => v.Position.Y).Min());
            Debug.Assert(BoundingBox.MinCorner.Z == this.Verticies.Select(v => v.Position.Z).Min());
        }

        public void Scale(double scalar)
        {
            GridVector3 minCorner = BoundingBox.MinCorner;
            GridVector3 scaledCorner = minCorner.Scale(scalar);

            this.Verticies.ForEach(v => v.Position = v.Position.Scale(scalar));
            BoundingBox.Scale(scalar);

            BoundingBox = new GridBox(scaledCorner, BoundingBox.dimensions);

            ValidateBoundingBox();
        }

        public void Translate(GridVector3 translate)
        {
            foreach(IVertex v in Verticies)
            {
                v.Position += translate;
            }

            BoundingBox = BoundingBox.Translate(translate);

            ValidateBoundingBox();
        }

        private void UpdateBoundingBox(GridVector3 point)
        {
            if (BoundingBox == null)
                BoundingBox = new GridBox(point, 0);
            else
            {
                BoundingBox.Union(point);
            }
        }

        private void UpdateBoundingBox(GridVector3[] points)
        {
            if (BoundingBox == null)
                BoundingBox = points.BoundingBox();
            else
            {
                BoundingBox.Union(points);
            }
        }

        public int AddVertex(IVertex v)
        {
            v.Index = Verticies.Count; 
            Verticies.Add(v);

            UpdateBoundingBox(v.Position);
            return Verticies.Count - 1; 
        }

        /// <summary>
        /// Add a collection of verticies to the mesh
        /// </summary>
        /// <param name="v"></param>
        /// <returns>The index the first element was inserted at</returns>
        public int AddVerticies(ICollection<IVertex> verts)
        {
            
            int iStart = Verticies.Count;
            int Offset = 0;
            foreach (IVertex v in verts)
            {
                v.Index = iStart + Offset;
                Offset += 1;
            }

            Verticies.AddRange(verts);
            UpdateBoundingBox(verts.Select(v => v.Position).ToArray());
            return iStart;
        }
        
        public void AddEdge(int A, int B)
        {
            EdgeKey e = new EdgeKey(A, B);
            AddEdge(e);
        }

        public void AddEdge(IEdgeKey e)
        {
            if (DuplicateEdge == null)
                throw new InvalidOperationException("DuplicateEdge function not specified for DynamicRenderMesh");

            if (Edges.ContainsKey(e))
                return;

            if (e.A >= Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            IEdge newEdge = DuplicateEdge(null, e.A, e.B);
            Edges.Add(e, newEdge);

            Verticies[(int)e.A].AddEdge(e);
            Verticies[(int)e.B].AddEdge(e);
        }
        

        public void AddEdge(IEdge e)
        {
            if (Edges.ContainsKey(e.Key))
                return;

            if (e.A >= Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            Edges.Add(e.Key, e);

            Verticies[(int)e.A].AddEdge(e.Key);
            Verticies[(int)e.B].AddEdge(e.Key);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        public void Update(IEdge e)
        {
            if(Edges.ContainsKey(e.Key))
            {
                Edges[e.Key] = e; 
            }
            else
            {
                throw new KeyNotFoundException("The edge to be updated was not present in the mesh:" + e.ToString());
            }
        }

        /// <summary>
        /// Add a face. Creates edges if they aren't in the face
        /// </summary>
        /// <param name="face"></param>
        public void AddFace(IFace face)
        {
            //Debug.Assert(Faces.Contains(face) == false);
              
            foreach(IEdgeKey e in face.Edges)
            {
                AddEdge(e);
                Edges[e].AddFace(face);
            }

            Faces.Add(face);
        }

        public void AddFace(int A, int B, int C)
        {
            IFace face = DuplicateFace(null, new int[] { A, B, C });
            Debug.Assert(Faces.Contains(face) == false);

            AddFace(face);
        }

        public void AddFaces(ICollection<IFace> faces)
        {
            foreach(IFace f in faces)
            {
                AddFace(f);
            }
        }

        public void RemoveFace(IFace f)
        {
            if(Faces.Contains(f))
            {
                Faces.Remove(f);
            }

            foreach(IEdgeKey e in f.Edges)
            {
                IEdge existing = Edges[e];
                existing.RemoveFace(f);
            }
        }
        
        public void RemoveEdge(IEdgeKey e)
        {
            if(Edges.ContainsKey(e))
            {
                IEdge removedEdge = Edges[e];

                foreach(IFace f in removedEdge.Faces)
                {
                    this.RemoveFace(f);
                }

                Edges.Remove(e);

                this[removedEdge.A].RemoveEdge(e);
                this[removedEdge.B].RemoveEdge(e);

                
            }
        }

        public IEnumerable<IVertex> GetVerts(IIndexSet vertIndicies)
        {
            return vertIndicies.Select(i => this.Verticies[(int)i]);
        }

        public IEnumerable<IVertex> GetVerts(ICollection<int> vertIndicies)
        {
            return vertIndicies.Select(i => this.Verticies[i]);
        }

        public IEnumerable<IVertex> GetVerts(ICollection<long> vertIndicies)
        {
            return vertIndicies.Select(i => this.Verticies[(int)i]);
        }

        public GridLineSegment ToSegment(IEdgeKey e)
        {
            return new GridLineSegment(Verticies[e.A].Position, Verticies[e.B].Position);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FaceDuplicator">Constructor to use when replacing the original face with the new split face</param>
        public void ConvertAllFacesToTriangles()
        {
            if (DuplicateFace == null)
                throw new InvalidOperationException("No duplication method in DynamicRenderMesh specified for faces");

            IEnumerable<IFace> quadFaces = this.Faces.Where(f => !f.IsTriangle()).ToList();
            
            foreach (IFace f in quadFaces)
            {
                this.SplitFace(f);
            }
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces...
        /// </summary>
        /// <param name="Duplicator">A constructor that can copy attributes of a face object</param>
        /// <returns></returns>
        public void SplitFace(IFace face)
        {
            if (face.IsTriangle())
                return;

            if(face.IsQuad())
            {
                Faces.Remove(face);

                GridVector3[] positions = GetVerts(face.iVerts).Select(v => v.Position).ToArray();
                if(GridVector3.Distance(positions[0], positions[2]) < GridVector3.Distance(positions[1], positions[3]))
                {
                    //Face ABC = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[2]);
                    //Face ACD = new Face(face.iVerts[0], face.iVerts[2], face.iVerts[3]);

                    IFace ABC = DuplicateFace(face, new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[2] });
                    IFace ACD = DuplicateFace(face, new int[] { face.iVerts[0], face.iVerts[2], face.iVerts[3] });
                    Faces.Add(ABC);
                    Faces.Add(ACD);
                }
                else
                {
                    //Face ABD = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[3]);
                    //Face BCD = new Face(face.iVerts[1], face.iVerts[2], face.iVerts[3]);

                    IFace ABD = DuplicateFace(face, new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[3] });
                    IFace BCD = DuplicateFace(face, new int[] { face.iVerts[1], face.iVerts[2], face.iVerts[3] });
                    Faces.Add(ABD);
                    Faces.Add(BCD);
                }
            }
        }

        public GridVector3 Normal(IFace f)
        {
            IVertex[] verticies = GetVerts(f.iVerts).ToArray();
            GridVector3 normal = GridVector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
            return normal;
        }

        /// <summary>
        /// Recalculate normals based on the faces touching each vertex
        /// </summary>
        public void RecalculateNormals()
        {
            //Calculate normals for all faces
            Dictionary<IFace, GridVector3> normals = new Dictionary<Meshing.IFace, Geometry.GridVector3>(this.Faces.Count);

            foreach(IFace f in this.Faces)
            {
                GridVector3 normal = Normal(f);
                normals.Add(f, normal);
            }

            /*
             * Profiling showed this implementation to be much slower
            for(int i = 0; i < Faces.Count; i++)
            {
                Face f = this.Faces.ElementAt(i);
                GridVector3 normal = Normal(f);
                normals.Add(f, normal);
            }
            */

            for(int i = 0; i < Verticies.Count; i++)
            {
                SortedSet<IFace> vertFaces = new SortedSet<Meshing.IFace>();
                IVertex v = Verticies[i];
                
                foreach(IEdgeKey ek in v.Edges)
                {
                    vertFaces.UnionWith(Edges[ek].Faces);
                }

                GridVector3 avgNormal = GridVector3.Zero;
                foreach(IFace f in vertFaces)
                {
                    avgNormal += normals[f];
                }

                avgNormal.Normalize();

                v.Normal = avgNormal;                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other"></param>
        /// <param name="VertexDuplicator">Takes a VERTEX and offset and returns a new VERTEX</param>
        /// <param name="EdgeDuplicator">Takes a EDGE and offset and returns a new EDGE, retaining all pertinent data from the original EDGE</param>
        /// <param name="FaceDuplicator">Takes a FACE and offset and returns a new FACE, retaining all pertinent data from the original FACE</param>
        /// <returns></returns>
        public int Append(DynamicRenderMesh other)
        {
            int startingAppendIndex = this.Verticies.Count;
            this.AddVerticies(other.Verticies.Select(v => DuplicateVertex(v, startingAppendIndex)).ToList());

            foreach(IEdge e in other.Edges.Values)
            {
                IEdge newEdge = DuplicateEdge(e, e.A + startingAppendIndex, e.B + startingAppendIndex);
                this.AddEdge(newEdge);
            }

            foreach(IFace f in other.Faces)
            {
                IFace newFace = DuplicateFace(f, f.iVerts.Select(i => i + startingAppendIndex));
                this.AddFace(newFace);
            }

            return startingAppendIndex;
        }
    }
}
