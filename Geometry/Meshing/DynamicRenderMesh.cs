using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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
    public abstract class MeshBase3D<VERTEX> : MeshBase<VERTEX>, IMesh3D<VERTEX>
        where VERTEX : IVertex3D
    {
        public Box BoundingBox { get; private set; }

        protected MeshBase3D()
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
            Vector3 minCorner = BoundingBox.MinCorner;
            Vector3 scaledCorner = minCorner.Scale(scalar);

            this._Verticies.ForEach(v => v.Position = v.Position.Scale(scalar));
            BoundingBox = new Box(scaledCorner, BoundingBox.Scale(scalar).dimensions);

            ValidateBoundingBox();
        }

        public void Translate(Vector3 translate)
        {
            foreach (IVertex3D v in _Verticies)
            {
                v.Position += translate;
            }

            BoundingBox = BoundingBox.Translate(translate);

            ValidateBoundingBox();
        }

        protected override void UpdateBoundingBox(VERTEX v) => BoundingBox = BoundingBox.MinVals is null ? new Box(v.Position, 0) : BoundingBox.Union(v.Position, out _);

        protected override void UpdateBoundingBox(IEnumerable<VERTEX> verts)
        {
            Vector3[] points = [.. verts.Select(v => v.Position)];
            BoundingBox = BoundingBox.MinVals is null ? points.BoundingBox() : BoundingBox.Union(points, out _);
        }

        /// <summary>
        /// Merge the other mesh into our mesh
        /// </summary>
        /// <param name="other"></param>
        /// <returns>The merged index number of the first vertex from the mesh merged into this mesh</returns>
        public long Merge(MeshBase3D<VERTEX> other)
        {
            long iVertMergeStart = this._Verticies.Count;

            this.AddVerticies(other.Vertices);

            IFace[] duplicateFaces = [.. other.Faces.Select(f => other.CreateOffsetFace(f, f.iVerts.Select(v => v + (int)iVertMergeStart)))];
            this.AddFaces(duplicateFaces);

            return iVertMergeStart;
        }

        public LineSegment ToSegment(IEdgeKey e) => new LineSegment(_Verticies[e.A].Position.XY(), _Verticies[e.B].Position.XY());

        public Triangle ToTriangle(IFace f)
        {
            if (false == f.IsTriangle())
                throw new InvalidOperationException("Face is not a triangle: " + f.iVerts.ToString());

            return new Triangle([.. this[f.iVerts].Select(v => v.Position.XY())]);
        }

        public Vector2 GetCentroid(IFace f)
        {
            Vector2[] verts = [.. this[f.iVerts].Select(v => v.Position.XY())];
            if (f.IsQuad())
            {
                Polygon poly = new(verts);
                return poly.Centroid;
            }
            else if (f.IsTriangle())
            {
                Triangle tri = new([.. this[f.iVerts].Select(v => v.Position.XY())]);
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
            if (CreateOffsetFace is null)
                throw new InvalidOperationException("No duplication method in DynamicRenderMesh specified for faces");

            IEnumerable<IFace> quadFaces = [.. this.Faces.Where(f => !f.IsTriangle())];

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
                return [face];

            if (face.IsQuad())
            {

                Vector3[] positions = [.. mesh[face.iVerts].Select(v => v.Position)];
                if (Vector3.Distance(positions[0], positions[2]) < Vector3.Distance(positions[1], positions[3]))
                {
                    IFace ABC = mesh.CreateFace([face.iVerts[0], face.iVerts[1], face.iVerts[2]]);
                    IFace ACD = mesh.CreateFace([face.iVerts[0], face.iVerts[2], face.iVerts[3]]);

                    return [ABC, ACD];
                }
                else
                {
                    IFace ABD = mesh.CreateFace([face.iVerts[0], face.iVerts[1], face.iVerts[3]]);
                    IFace BCD = mesh.CreateFace([face.iVerts[1], face.iVerts[2], face.iVerts[3]]);

                    return [ABD, BCD];
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

            if (face.IsQuad())
            {
                RemoveFace(face);

                Vector3[] positions = [.. this[face.iVerts].Select(v => v.Position)];
                if (Vector3.Distance(positions[0], positions[2]) < Vector3.Distance(positions[1], positions[3]))
                {
                    //Face ABC = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[2]);
                    //Face ACD = new Face(face.iVerts[0], face.iVerts[2], face.iVerts[3]);

                    IFace ABC = CreateFace([face.iVerts[0], face.iVerts[1], face.iVerts[2]]);
                    IFace ACD = CreateFace([face.iVerts[0], face.iVerts[2], face.iVerts[3]]);
                    AddFace(ABC);
                    AddFace(ACD);
                }
                else
                {
                    //Face ABD = new Face(face.iVerts[0], face.iVerts[1], face.iVerts[3]);
                    //Face BCD = new Face(face.iVerts[1], face.iVerts[2], face.iVerts[3]);

                    IFace ABD = CreateFace([face.iVerts[0], face.iVerts[1], face.iVerts[3]]);
                    IFace BCD = CreateFace([face.iVerts[1], face.iVerts[2], face.iVerts[3]]);
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
        public Vector3 Normal(IEnumerable<int> iVerts)
        {
            VERTEX[] verticies = [.. this[iVerts]];
            if (verticies.Length != 3)
                throw new NotImplementedException("Normal calculation for non-triangular faces not possible.");

            Vector3 normal = Vector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
            return Vector3.Normalize(normal);
        }

        /// <summary>
        /// Returns the normal vector for a triangular face
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public Vector3 Normal(IFace f)
        {
            if (f.IsTriangle() == false)
                throw new NotImplementedException("Normal calculation for non-triangular faces not possible.");

            VERTEX[] verticies = [.. this[f.iVerts]];
            Vector3 normal = Vector3.Cross(verticies[0].Position, verticies[1].Position, verticies[2].Position);
            return Vector3.Normalize(normal);
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

                totalDistance += Vector3.Distance(origin.Position, next.Position);
                origin = next;
            }

            return totalDistance;
        }

        /// <summary>
        /// This cache needs more careful analysis in the profiler
        /// </summary>
        readonly Dictionary<IFace, Vector3> face_normals_cache = [];

        /// <summary>
        /// Recalculate normals based on the faces touching each vertex.
        /// The cache is keyed by <see cref="IFace"/> equality, which ignores winding, so it is cleared first:
        /// otherwise a reversed face keeps the pre-flip normal and lighting stays checkerboard.
        /// </summary>
        public void RecalculateNormals()
        {
            face_normals_cache.Clear();
            foreach (IFace f in this.Faces)
            {
                face_normals_cache.Add(f, Normal(f));
            }

            /*
             * Profiling showed this implementation to be much slower
            for(int i = 0; i < Faces.Count; i++)
            {
                Face f = this.Faces.ElementAt(i);
                Vector3 normal = Normal(f);
                normals.Add(f, normal);
            }
            */

            for (int i = 0; i < _Verticies.Count; i++)
            {
                SortedSet<IFace> vertFaces = [];
                IVertex3D v = this[i];

                foreach (IEdgeKey ek in v.Edges)
                {
                    vertFaces.UnionWith(Edges[ek].Faces);
                }

                Vector3 avgNormal = Vector3.Zero;
                foreach (IFace f in vertFaces)
                {
                    avgNormal += face_normals_cache[f];
                }

                avgNormal = Vector3.Normalize(avgNormal);

                v.Normal = avgNormal;
            }
        }

        /// <summary>
        /// Recalculate normals for <paramref name="verticies"/> only.  A vertex normal can only change when a
        /// face touching it was added or rewound, so callers merging a sub-mesh in pass the indicies the
        /// incoming geometry mapped to and the cost stays proportional to the merged side rather than to the
        /// whole composite.
        ///
        /// <see cref="Face.Equals(IFace)"/> ignores winding, so a reversed face would otherwise keep its
        /// pre-flip cached normal.  The parameterless overload clears the entire cache for that reason; here
        /// only the faces incident to the listed vertices are evicted, which covers every face that could have
        /// changed while leaving the rest of the composite's cache intact.
        /// </summary>
        public void RecalculateNormals(IEnumerable<int> verticies)
        {
            HashSet<int> affected = verticies as HashSet<int> ?? [.. verticies];

            //Accumulate in the same order as the parameterless overload so both produce identical sums.
            SortedSet<IFace> vertFaces = [];
            HashSet<IFace> evicted = [];

            foreach (int i in affected)
            {
                IVertex3D v = this[i];

                vertFaces.Clear();
                foreach (IEdgeKey ek in v.Edges)
                {
                    vertFaces.UnionWith(Edges[ek].Faces);
                }

                Vector3 avgNormal = Vector3.Zero;
                foreach (IFace f in vertFaces)
                {
                    if (evicted.Add(f))
                        face_normals_cache.Remove(f);

                    if (face_normals_cache.TryGetValue(f, out Vector3 normal) == false)
                    {
                        normal = Normal(f);
                        face_normals_cache.Add(f, normal);
                    }

                    avgNormal += normal;
                }

                avgNormal = Vector3.Normalize(avgNormal);

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
            this.AddVerticies([.. other.Vertices.Select(v =>
            {
                IVertex copy = v.ShallowCopy(v.Index + startingAppendIndex);
                return (VERTEX)copy;
            })]);

            foreach (IEdge e in other.Edges.Values)
            {
                IEdge newEdge = CreateOffsetEdge(e, e.A + startingAppendIndex, e.B + startingAppendIndex);
                this.AddEdge(newEdge);
            }

            foreach (IFace f in other.Faces)
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
        public bool Intersects(IFace face, Vector3 A, Vector3 B)
        {
            Debug.Assert(face.iVerts.Length == 3);
            if (face.iVerts.Length != 3)
            {
                throw new ArgumentException("Intersects requires a triangular face");
            }

            Vector3 v0 = this[face.iVerts[0]].Position;
            Vector3 v1 = this[face.iVerts[1]].Position;
            Vector3 v2 = this[face.iVerts[2]].Position;

            Vector3 direction = B - A;
            Vector3 origin = A;

            Vector3 v1_v0 = v1 - v0;
            Vector3 v2_v0 = v2 - v0;

            Vector3 d_e2_cross = Vector3.Cross(direction, v2_v0);
            double dotProduct = Vector3.Dot(v1_v0, d_e2_cross);

            //Check for invalid triangle
            if (dotProduct < Global.Epsilon && dotProduct > -Global.Epsilon)
                return false;

            double f = 1.0 / dotProduct;

            Vector3 A_v0 = A - v0;

            double u = f * Vector3.Dot(A_v0, d_e2_cross);

            //Check for invalid triangle
            if (u < 0 || u > 1.0)
                return false;

            Vector3 A_ = Vector3.Cross(A_v0, v1_v0);
            double v = f = Vector3.Dot(direction, v1_v0);

            if (v < 0 || v + u > 1.0)
                return false;

            //Find intersection point on the line
            double t = f * Vector3.Dot(v2_v0, d_e2_cross);

            if (t >= 0 && t <= 1.0) //For Ray intersection don't check t <= 1.0;
            {
                return true;
            }

            return false;
        }

    }
}
