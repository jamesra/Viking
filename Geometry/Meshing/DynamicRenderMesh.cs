using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry.Meshing
{

    /// <summary>
    /// TODO: This class needs to be updated now that MeshBase<T> exists
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Mesh3D<VERTEX> : MeshBase3D<VERTEX>
        where VERTEX : IVertex3D
    {
        
    }

    /// <summary>
    /// TODO: This class needs to be updated now that MeshBase<T> exists
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Mesh3D : MeshBase3D<IVertex3D>
    {
    }

    /// <summary>
    /// This is a fairly generic 3D Mesh class that supports operations around merging and basic spatial manipulation of meshes
    /// </summary>
    public abstract class MeshBase3D<VERTEX> : MeshBase<VERTEX> , IMesh3D<VERTEX>
        where VERTEX : IVertex3D
    { 
        public GridBox BoundingBox { get; private set; }

        public MeshBase3D()
        {
            CreateOffsetEdge = Edge.CreateOffsetCopy;
            CreateOffsetFace = Face.CreateOffsetCopy;
             
            CreateEdge = Edge.Create;
            CreateFace = Face.Create;
        }
         
        protected void ValidateBoundingBox()
        {
            Debug.Assert(BoundingBox.MinCorner.X == this._Verticies.Select(v => v.Position.X).Min());
            Debug.Assert(BoundingBox.MinCorner.Y == this._Verticies.Select(v => v.Position.Y).Min());
            Debug.Assert(BoundingBox.MinCorner.Z == this._Verticies.Select(v => v.Position.Z).Min());
        }

        public void Scale(double scalar)
        {
            GridVector3 minCorner = BoundingBox.MinCorner;
            GridVector3 scaledCorner = minCorner.Scale(scalar);

            this._Verticies.ForEach(v => v.Position = v.Position.Scale(scalar));
            BoundingBox.Scale(scalar);

            BoundingBox = new GridBox(scaledCorner, BoundingBox.dimensions);

            ValidateBoundingBox();
        }

        public void Translate(GridVector3 translate)
        {
            foreach(IVertex3D v in _Verticies)
            {
                v.Position += translate;
            }

            BoundingBox = BoundingBox.Translate(translate);

            ValidateBoundingBox();
        }

        protected override void UpdateBoundingBox(VERTEX v)
        {
            if (BoundingBox == null)
                BoundingBox = new GridBox(v.Position, 0);
            else
            {
                BoundingBox.Union(v.Position);
            }
        }

        protected override void UpdateBoundingBox(IEnumerable<VERTEX> verts)
        {
            GridVector3[] points = verts.Select(v => v.Position).ToArray();
            if (BoundingBox == null)
                BoundingBox = points.BoundingBox();
            else
            {
                BoundingBox.Union(points);
            }
        }
        
        /// <summary>
        /// Merge the other mesh into our mesh
        /// </summary>
        /// <param name="other"></param>
        /// <returns>The merged index number of the first vertex from the mesh merged into this mesh</returns>
        public long Merge(MeshBase3D<VERTEX> other)
        {
            long iVertMergeStart = this._Verticies.Count;

            this.AddVerticies(other.Verticies);

            IFace[] duplicateFaces = other.Faces.Select(f => other.CreateOffsetFace(f, f.iVerts.Select(v => v + (int)iVertMergeStart))).ToArray();
            this.AddFaces(duplicateFaces);

            return iVertMergeStart;
        }
         
        public GridLineSegment ToSegment(IEdgeKey e)
        {
            return new GridLineSegment(_Verticies[e.A].Position, _Verticies[e.B].Position);
        }

        public GridTriangle ToTriangle(IFace f)
        {
            if (false == f.IsTriangle())
                throw new InvalidOperationException("Face is not a triangle: " + f.iVerts.ToString());

            return new GridTriangle(this[f.iVerts].Select(v => v.Position.XY()).ToArray()); 
        }

        public GridVector2 GetCentroid(IFace f)
        {
            GridVector2[] verts = this[f.iVerts].Select(v => v.Position.XY()).ToArray();
            if (f.IsQuad())
            {
                GridPolygon poly = new GridPolygon(verts);
                return poly.Centroid;
            }
            else if (f.IsTriangle())
            {
                GridTriangle tri = new GridTriangle(this[f.iVerts].Select(v => v.Position.XY()).ToArray());
                return tri.Centroid;
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
        public static IFace[] SplitFace(Mesh3D mesh, IFace face)
        {
            if (face.IsTriangle())
                return new IFace[] { face };

            if (face.IsQuad())
            {
                
                GridVector3[] positions = mesh[face.iVerts].Select(v => v.Position).ToArray();
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
        public override void SplitFace(IFace face)
        {
            if (face.IsTriangle())
                return;

            if(face.IsQuad())
            {
                RemoveFace(face);

                GridVector3[] positions = this[face.iVerts].Select(v => v.Position).ToArray();
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

        /// <summary>
        /// Returns the normal vector for a triangular face
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public GridVector3 Normal(IEnumerable<int> iVerts)
        {
            VERTEX[] verticies = this[iVerts].ToArray();
            if (verticies.Length != 3)
                throw new NotImplementedException("Normal calculation for non-triangular faces not possible.");

            GridVector3 normal = GridVector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
            normal.Normalize();
            return normal;
        }

        /// <summary>
        /// Returns the normal vector for a triangular face
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public GridVector3 Normal(IFace f)
        {
            if (f.IsTriangle() == false)
                throw new NotImplementedException("Normal calculation for non-triangular faces not possible.");

            VERTEX[] verticies = this[f.iVerts].ToArray();
            GridVector3 normal = GridVector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
            normal.Normalize();
            return normal;
        }


        /// <summary>
        /// Return the distance to travel to each of the vertex indicies 
        /// </summary>
        /// <param name="iVerts"></param>
        /// <returns></returns>
        public double PathDistance(IReadOnlyList<int> iVerts)
        {
            if (iVerts.Count < 2)
                return 0;

            IVertex3D origin = this[iVerts[0]];
            double totalDistance = 0;
            for (int i = 1; i < iVerts.Count; i++)
            {
                IVertex3D next = this[iVerts[i]];

                totalDistance += GridVector3.Distance(origin.Position, next.Position);
                origin = next;
            }

            return totalDistance;
        }

        /// <summary>
        /// This cache needs more careful analysis in the profiler
        /// </summary>
        Dictionary<IFace, GridVector3> face_normals_cache = new Dictionary<Meshing.IFace, Geometry.GridVector3>();

        /// <summary>
        /// Recalculate normals based on the faces touching each vertex
        /// </summary>
        public void RecalculateNormals()
        {
            //Calculate normals for all faces

            if (face_normals_cache.Count == 0)
            {
                foreach (IFace f in this.Faces)
                {
                    GridVector3 normal = Normal(f);
                    face_normals_cache.Add(f, normal);
                }
            }
            else
            {
                foreach (IFace f in this.Faces.Where(face => face_normals_cache.ContainsKey(face) == false))
                {
                    GridVector3 normal = Normal(f);
                    face_normals_cache.Add(f, normal);
                }
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

            for(int i = 0; i < _Verticies.Count; i++)
            {
                SortedSet<IFace> vertFaces = new SortedSet<Meshing.IFace>();
                IVertex3D v = this[i];
                  
                foreach(IEdgeKey ek in v.Edges)
                {
                    vertFaces.UnionWith(Edges[ek].Faces);
                }

                GridVector3 avgNormal = GridVector3.Zero;
                foreach(IFace f in vertFaces)
                {
                    avgNormal += face_normals_cache[f];
                }

                avgNormal.Normalize();

                v.Normal = avgNormal;                
            }
        }

        /// <summary>
        /// Recalculate normals based on the faces touching each vertex
        /// </summary>
        public void RecalculateNormals(IEnumerable<int> verticies)
        {
            //Calculate normals for all faces
            //Dictionary<IFace, GridVector3> normals = new Dictionary<Meshing.IFace, Geometry.GridVector3>(this.Faces.Count);
            /*
            foreach (IFace f in this.Faces)
            {
                GridVector3 normal = Normal(f);
                normals.Add(f, normal);
            }
            */
            /*
             * Profiling showed this implementation to be much slower
            for(int i = 0; i < Faces.Count; i++)
            {
                Face f = this.Faces.ElementAt(i);
                GridVector3 normal = Normal(f);
                normals.Add(f, normal);
            }
            */

            for (int i = 0; i < _Verticies.Count; i++)
            {
                //SortedSet<IFace> vertFaces = new SortedSet<Meshing.IFace>();
                IVertex3D v = this[i];

                IFace[] vertFaces = this[v.Edges].SelectMany(e => e.Faces).Distinct().ToArray();
                
                GridVector3 avgNormal = GridVector3.Zero;
                for(int iFace = 0; iFace < vertFaces.Length; iFace++)
                {
                    IFace f = vertFaces[iFace]; 

                    bool face_has_normal = face_normals_cache.TryGetValue(f, out GridVector3 normal);
                    if(face_has_normal == false)
                    {
                        normal = Normal(f);
                        face_normals_cache.Add(f, normal); //Populate the cache
                    }

                    avgNormal += normal;
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
        public virtual int Append(MeshBase3D<VERTEX> other)
        {
            int startingAppendIndex = this._Verticies.Count;
            this.AddVerticies(other.Verticies.Select(v =>
            {
                IVertex copy = v.ShallowCopy();
                copy.Index += startingAppendIndex;
                return (VERTEX)copy;
            }).ToList());

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
        /// Returns true if a line from A to B intersects the given face.
        /// 
        /// This function is not tested yet.  It was added as a potential Bajaj SliceChord criterion but never added.
        /// </summary>
        /// <param name="face"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public bool Intersects(IFace face, GridVector3 A, GridVector3 B)
        {
            Debug.Assert(face.iVerts.Length == 3);
            if(face.iVerts.Length != 3)
            {
                throw new ArgumentException("Intersects requires a triangular face");
            }

            GridVector3 v0 = this[face.iVerts[0]].Position;
            GridVector3 v1 = this[face.iVerts[1]].Position;
            GridVector3 v2 = this[face.iVerts[2]].Position; 

            GridVector3 direction = B - A;
            GridVector3 origin = A;

            GridVector3 v1_v0 = v1 - v0;
            GridVector3 v2_v0 = v2 - v0;

            GridVector3 d_e2_cross = GridVector3.Cross(direction, v2_v0);
            double dotProduct = GridVector3.Dot(v1_v0, d_e2_cross);

            //Check for invalid triangle
            if (dotProduct < Global.Epsilon && dotProduct > -Global.Epsilon)
                return false;

            double f = 1.0 / dotProduct;

            GridVector3 A_v0 = A - v0;

            double u = f * GridVector3.Dot(A_v0, d_e2_cross);

            //Check for invalid triangle
            if (u < 0 || u > 1.0)
                return false;

            GridVector3 A_ = GridVector3.Cross(A_v0, v1_v0);
            double v = f = GridVector3.Dot(direction, v1_v0);

            if (v < 0 || v + u > 1.0)
                return false;

            //Find intersection point on the line
            double t = f * GridVector3.Dot(v2_v0, d_e2_cross);

            if (t >= 0 && t <= 1.0) //For Ray intersection don't check t <= 1.0;
            {
                return true;
            }

            return false;
        }

    }
}
