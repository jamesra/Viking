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

        public Func<IVertex,int,IVertex> CreateOffsetVertex { get; set; }
        public Func<IEdge, int, int, IEdge> CreateOffsetEdge { get; set; }

        public Func<IFace, IEnumerable<int>, IFace> CreateOffsetFace { get; set; }

        public Func<int, IVertex> CreateVertex { get; set; }
        public Func<int, int, IEdge> CreateEdge { get; set; }

        public Func<IEnumerable<int>, IFace> CreateFace { get; set; }

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

        public virtual IEdge this[IEdgeKey key]
        {
            get { return this.Edges[key]; }
        }

        public DynamicRenderMesh()
        {
            CreateOffsetVertex = Vertex.CreateOffsetCopy;
            CreateOffsetEdge = Edge.CreateOffsetCopy;
            CreateOffsetFace = Face.CreateOffsetCopy;
             
            CreateEdge = Edge.Create;
            CreateFace = Face.Create;
        }

        public virtual bool Contains(IEdgeKey key)
        {
            return Edges.ContainsKey(key);
        }

        public virtual bool Contains(IFace face)
        {
            return Faces.Contains(face);
        }

        public virtual bool Contains(int A, int B)
        {
            return Edges.ContainsKey(new EdgeKey(A, B));
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
            if(e.A == e.B)
                throw new ArgumentException("Edges cannot have the same start and end point");

            if (CreateOffsetEdge == null)
                throw new InvalidOperationException("DuplicateEdge function not specified for DynamicRenderMesh");

            if (Edges.ContainsKey(e))
                return;

            if (e.A >= Verticies.Count || e.A < 0)
                throw new ArgumentException(string.Format("Edge vertex A references non-existent vertex {0}", e));

            if (e.B >= Verticies.Count || e.B < 0)
                throw new ArgumentException(string.Format("Edge vertex B references non-existent vertex {0}", e));

            IEdge newEdge = CreateOffsetEdge(null, e.A, e.B);
            Edges.Add(e, newEdge);

            Verticies[(int)e.A].AddEdge(e);
            Verticies[(int)e.B].AddEdge(e);
        }
        

        public void AddEdge(IEdge e)
        {
            if (e.A == e.B)
                throw new ArgumentException("Edges cannot have the same start and end point");

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
        /// Merge the other mesh into our mesh
        /// </summary>
        /// <param name="other"></param>
        /// <returns>The merged index number of the first vertex from the mesh merged into this mesh</returns>
        public long Merge(DynamicRenderMesh other)
        {
            long iVertMergeStart = this.Verticies.Count;

            this.AddVerticies(other.Verticies);

            IFace[] duplicateFaces = other.Faces.Select(f => other.DuplicateFace(f, f.iVerts.Select(v => v + (int)iVertMergeStart))).ToArray();
            this.AddFaces(duplicateFaces);

            return iVertMergeStart;
        }

        /// <summary>
        /// Return true if an edge exists between the two verticies
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public bool IsAnEdge(int A, int B)
        {
            EdgeKey AB = new EdgeKey(A, B);
            EdgeKey BA = new EdgeKey(B, A);

            return Edges.ContainsKey(AB) || Edges.ContainsKey(BA);
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
            IFace face = CreateFace(new int[] { A, B, C });
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

        public GridTriangle ToTriangle(IFace f)
        {
            if (false == f.IsTriangle())
                throw new InvalidOperationException("Face is not a triangle: " + f.iVerts.ToString());

            return new GridTriangle(f.iVerts.Select(v => this.Verticies[v].Position.XY()).ToArray()); 
        }

        public GridVector2 GetCentroid(IFace f)
        {
            GridVector2[] verts = f.iVerts.Select(v => this.Verticies[v].Position.XY()).ToArray();
            if (f.IsQuad())
            {
                GridPolygon poly = new GridPolygon(verts);
                return poly.Centroid;
            }
            else if (f.IsTriangle())
            {
                GridTriangle tri = new GridTriangle(f.iVerts.Select(v => this.Verticies[v].Position.XY()).ToArray());
                return tri.BaryToVector(new GridVector2(1 / 3.0, 1 / 3.0));

            }
            else
            {
                throw new InvalidOperationException("Face is not a triangle or quad: " + f.iVerts.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="FaceDuplicator">Constructor to use when replacing the original face with the new split face</param>
        public void ConvertAllFacesToTriangles()
        {
            if (CreateOffsetFace == null)
                throw new InvalidOperationException("No duplication method in DynamicRenderMesh specified for faces");

            IEnumerable<IFace> quadFaces = this.Faces.Where(f => !f.IsTriangle()).ToList();
            
            foreach (IFace f in quadFaces)
            {
                this.SplitFace(f);
            }
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces with 3 or 4 verticies.  It does not remove or add the face from the mesh
        /// </summary>
        /// <param name="Duplicator">A constructor that can copy attributes of a face object</param>
        /// <returns></returns>
        public static IFace[] SplitFace(DynamicRenderMesh mesh, IFace face)
        {
            if (face.IsTriangle())
                return new IFace[] { face };

            if (face.IsQuad())
            {
                GridVector3[] positions = mesh.GetVerts(face.iVerts).Select(v => v.Position).ToArray();
                if (GridVector3.Distance(positions[0], positions[2]) < GridVector3.Distance(positions[1], positions[3]))
                { 
                    IFace ABC = mesh.CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[2] });
                    IFace ACD = mesh.CreateFace(new int[] { face.iVerts[0], face.iVerts[2], face.iVerts[3] });

                    return new IFace[] { ABC, ACD };
                }
                else
                {  
                    IFace ABD = mesh.CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[3] });
                    IFace BCD = mesh.CreateFace(new int[] { face.iVerts[1], face.iVerts[2], face.iVerts[3] });

                    return new IFace[] { ABD, BCD };
                }
            }

            throw new NotImplementedException("Face has too many verticies to split");
        }

        /// <summary>
        /// Given a face that is not a triangle, return an array of triangles describing the face.
        /// For now this assumes convex faces with 3 or 4 verticies.  It removes the face and adds the split faces from the mesh
        /// </summary>
        /// <param name="Duplicator">A constructor that can copy attributes of a face object</param>
        /// <returns></returns>
        public void SplitFace(IFace face)
        {
            if (face.IsTriangle())
                return;

            if(face.IsQuad())
            {
                RemoveFace(face);

                GridVector3[] positions = GetVerts(face.iVerts).Select(v => v.Position).ToArray();
                if(GridVector3.Distance(positions[0], positions[2]) < GridVector3.Distance(positions[1], positions[3]))
                {
                    //Face ABC = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[2]);
                    //Face ACD = new Face(face.iVerts[0], face.iVerts[2], face.iVerts[3]);

                    IFace ABC = CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[2] });
                    IFace ACD = CreateFace(new int[] { face.iVerts[0], face.iVerts[2], face.iVerts[3] });
                    AddFace(ABC);
                    AddFace(ACD);
                }
                else
                {
                    //Face ABD = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[3]);
                    //Face BCD = new Face(face.iVerts[1], face.iVerts[2], face.iVerts[3]);

                    IFace ABD = CreateFace(new int[] { face.iVerts[0], face.iVerts[1], face.iVerts[3] });
                    IFace BCD = CreateFace(new int[] { face.iVerts[1], face.iVerts[2], face.iVerts[3] });
                    AddFace(ABD);
                    AddFace(BCD);
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
        public virtual int Append(DynamicRenderMesh other)
        {
            int startingAppendIndex = this.Verticies.Count;
            this.AddVerticies(other.Verticies.Select(v => CreateOffsetVertex(v, startingAppendIndex)).ToList());

            foreach(IEdge e in other.Edges.Values)
            {
                IEdge newEdge = CreateOffsetEdge(e, e.A + startingAppendIndex, e.B + startingAppendIndex);
                this.AddEdge(newEdge);
            }

            foreach(IFace f in other.Faces)
            {
                IFace newFace = CreateOffsetFace(f, f.iVerts.Select(i => i + startingAppendIndex));
                this.AddFace(newFace);
            }

            return startingAppendIndex;
        }

        /// <summary>
        /// Find all edges that enclose triangles or quads and create faces if they don't exist
        /// </summary>
        public void CloseFaces(IEnumerable<IVertex> VertsToClose=null)
        {
            if(VertsToClose == null)
            {
                VertsToClose = this.Verticies;
            }

            foreach (var v in VertsToClose)
            {
                //Identify edges missing faces
                List<IEdge> edges = v.Edges.Select(key => Edges[key]).Where(e => e.Faces.Count < 2).ToList();

                foreach (var edge in edges)
                {
                    List<int> Face = FindCloseableFace(v.Index, this[edge.OppositeEnd(v.Index)], edge);
                    if (Face != null)
                    {
                        Debug.Assert(Face.Count == 3 || Face.Count == 4);
                        if (Face.Count == 4)
                            continue;

                        IFace f = this.CreateFace(Face);
                        if(this.Faces.Contains(f) == false)
                            this.AddFace(f);

                        if (f.iVerts.Length == 4)
                            this.SplitFace(f);
                    }
                }
            }
        }

        /// <summary>
        /// If there are verticies with two edges that have a missing face, and the opposite end of the edges are on different shapes, and we can create a face that does not contain any other verticies then do so.
        /// </summary>
        public void CloseShapeCrossings()
        {
            
        }

        /// <summary>
        /// Identify if there are faces that could be created using the specified verticies
        /// </summary>
        /// <param name="TargetVert"></param>
        /// <param name="current"></param>
        /// <param name="testEdge"></param>
        /// <param name="CheckedEdges"></param>
        /// <param name="Path"></param>
        /// <returns></returns>
        private List<int> FindCloseableFace(int TargetVert, IVertex current, IEdge testEdge, SortedSet<IEdgeKey> CheckedEdges = null, Stack<int> Path = null)
        {
            if (CheckedEdges == null)
            {
                CheckedEdges = new SortedSet<IEdgeKey>();
            }

            if (Path == null)
            {
                Path = new Stack<int>();
                Path.Push(TargetVert);
            }

            //Make sure the face formed by the top three entries in the path is not already present in the mesh

            List<int> FaceTest = StackExtensions<int>.Peek(Path, 3);
            if (FaceTest.Count == 3)
            {
                if (this.Contains(new Face(FaceTest)))
                    return null;
            }

            /////////////////////////////////////////////////////////////

            CheckedEdges.Add(testEdge.Key);
            if (Path.Count > 4) //We must return only triangles or quads, and we return closed loops
                return null;

            if(current.Index == TargetVert)
            {
                return Path.ToList();
            }
            else
            {
                Path.Push(current.Index);
            }
            
            //Test all of the edges we have not examined yet who do not have two faces already
            List<int> ShortestFace = null;
            foreach(IEdge edge in current.Edges.Where(e => !CheckedEdges.Contains(e)).Select(e => this.Edges[e]).Where(e => e.Faces.Count < 2))
            {
                List<int> Face = FindCloseableFace(TargetVert, this[edge.OppositeEnd(current.Index)], edge, new SortedSet<IEdgeKey>(CheckedEdges), new Stack<int>(Path));

                if (Face != null)
                {
                    if (ShortestFace == null)
                    {
                        ShortestFace = Face;
                    }
                    else
                    {
                        if (ShortestFace.Count > Face.Count)
                        {
                            ShortestFace = Face;
                        }
                    }
                }

            }

            if (ShortestFace != null)
            {
                return ShortestFace;
            }

            //Take this index off the stack since we did not locate a path
            Path.Pop();

            return null; 
        }
    }
}
