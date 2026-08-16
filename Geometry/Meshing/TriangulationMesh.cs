//#define TRACEDELAUNAY

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry.Meshing
{

    /// <summary>
    /// Closely related to the CompareAngle class.  In this version the vertex index can change and is determined by the 
    /// duplicate key in the compared IEdgeKeys
    /// </summary>
    public class MeshEdgeAngleComparer<VERTEX>(IMesh2D<VERTEX> mesh, Vector2 origin_line_vector, bool clockwise = false) : IComparer<IEdgeKey>
        where VERTEX : IVertex2D
    {
        readonly IMesh2D<VERTEX> Mesh = mesh;
        public readonly bool ClockwiseOrder = clockwise;

        /// <summary>
        /// A vector originating from 0,0.  It determines which edge is the first in the rotation order.
        /// </summary>
        readonly Vector2 OriginVector = origin_line_vector;


        public MeshEdgeAngleComparer(IMesh2D<VERTEX> mesh, Line origin_line_vector, bool clockwise = false) :
            this(mesh, origin_line_vector.Direction, clockwise)
        {
        }

        public int Compare(IEdgeKey A, IEdgeKey B)
        {
            int origin_vertex = A.A == B.A || A.A == B.B ? A.A : A.B;
            int APoint = A.OppositeEnd(origin_vertex);
            int BPoint = B.OppositeEnd(origin_vertex);

            Vector2 Origin = Mesh[origin_vertex].Position;
            Vector2 ComparisonPoint = Origin + OriginVector;

            double angleA = Vector2.ArcAngle(in Origin, Mesh[APoint].Position, in ComparisonPoint);
            double angleB = Vector2.ArcAngle(in Origin, Mesh[BPoint].Position, in ComparisonPoint);

            //We are measuring the angle from the line in one direction, so don't allow negative angles
            angleA = angleA < 0 ? angleA + (Math.PI * 2.0) : angleA;
            angleB = angleB < 0 ? angleB + (Math.PI * 2.0) : angleB;

            return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
        }
    }

    /// <summary>
    /// Closely related to the CompareAngle class.  Assumes the Vertex index will never change in the mesh and is able to cache appropriately.
    /// </summary>
    public class MeshEdgeAngleComparerFixedIndex<VERTEX> : IComparer<IEdgeKey>
        where VERTEX : IVertex2D
    {
        readonly IMesh<VERTEX> Mesh;
        public readonly bool ClockwiseOrder;

        private readonly int OriginVertex;

        /// <summary>
        /// A line originating from the vertex.  It determines which edge is the first in the rotation order.
        /// </summary>
        readonly Line OriginLine;

        /// <summary>
        /// Precalculated comparison point used to compare angles
        /// </summary>
        private readonly Vector2 ComparisonPoint;

        public MeshEdgeAngleComparerFixedIndex(IMesh<VERTEX> mesh, int origin_vertex, IEdgeKey origin_line, bool clockwise = false) :
            this(mesh, origin_vertex, Vector2.Normalize(mesh[origin_line.OppositeEnd(origin_vertex)].Position - mesh[origin_vertex].Position), clockwise)
        {
        }

        public MeshEdgeAngleComparerFixedIndex(IMesh<VERTEX> mesh, int origin_vertex, Line origin_line_vector, bool clockwise = false) :
            this(mesh, origin_vertex, origin_line_vector.Direction, clockwise)
        {
        }

        public MeshEdgeAngleComparerFixedIndex(IMesh<VERTEX> mesh, int origin_vertex, Vector2 origin_line_vector, bool clockwise = false)
        {
            Mesh = mesh;
            OriginVertex = origin_vertex;

            Vector2 Origin = mesh[origin_vertex].Position;
            OriginLine = new Line(Origin, origin_line_vector);

            ComparisonPoint = Origin + origin_line_vector;

            ClockwiseOrder = clockwise;
        }

        /// <summary>
        /// Measure angle of edge to the origin vector
        /// </summary>
        /// <param name="A"></param>
        /// <returns></returns>
        public double MeasureAngle(IEdgeKey A)
        {
            int APoint = A.OppositeEnd(OriginVertex);
            return MeasureAngle(APoint);
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            //return Vector2.AbsArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint, ClockwiseOrder);            
        }

        /// <summary>
        /// Measure angle of edge to the origin vector
        /// </summary>
        /// <param name="A"></param>
        /// <returns></returns>
        public double MeasureAngle(long APoint) =>
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            Vector2.AbsArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint, ClockwiseOrder);

        public int Compare(IEdgeKey A, IEdgeKey B)
        {
            int APoint = A.OppositeEnd(OriginVertex);
            int BPoint = B.OppositeEnd(OriginVertex);
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            double angleA = Vector2.AbsArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint);
            double angleB = Vector2.AbsArcAngle(OriginLine.Origin, Mesh[BPoint].Position, ComparisonPoint);

            return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
        }
    }

    public class TriangulationVertex : Vertex2D, IVertexSortEdgeByAngle
    {
        public TriangulationVertex(Vector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(p, edgeComparer)
        {
        }

        protected TriangulationVertex(int index, Vector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(index, p, edgeComparer)
        {
        }

        /// <summary>
        /// Returns the edges of this vertex sorted by increasing angle off a line originating at this vertex and projecting towards the origin_edge vertex
        /// </summary>
        /// <param name="comparer"></param>
        /// <param name="origin_edge"></param>
        /// <param name="clockwise"></param>
        /// <returns></returns>
        public IEnumerable<long> EdgesByAngle(IComparer<IEdgeKey> comparer, long origin_edge, bool clockwise)
        {
            if (false == this.Edges.Contains(new EdgeKey(this.Index, origin_edge)))
            {
                throw new ArgumentException("Non-existent edge passed as origin to EdgesByAngle.");
            }

            //Setting the comparer should update the order of the edges attribute only if necessary.
            this.EdgeComparer = comparer;

            long[] sortedEdges = [.. this._Edges.Select(e => (long)e.OppositeEnd(this.Index))];

            long iStart = Array.IndexOf<long>(sortedEdges, origin_edge);

            long[] EdgesSortedAroundOrigin = new long[this.Edges.Count];
            IIndexSet indicies = new FiniteWrappedIndexSet(0, sortedEdges.Length, iStart);

            if (clockwise)
            {
                indicies = indicies.Reverse();
            }

            for (long i = 0; i < indicies.Count; i++)
            {
                long sortedEdgeIndex = indicies[i];
                EdgesSortedAroundOrigin[i] = sortedEdges[sortedEdgeIndex];
            }

            return EdgesSortedAroundOrigin;
        }

        public override IVertex ShallowCopy()
        {
            TriangulationVertex newVertex = new(this.Index, Position)
            {
                EdgeComparer = this.EdgeComparer
            };
            return newVertex;
        }

        public override IVertex ShallowCopy(int index)
        {
            TriangulationVertex newVertex = new(index, Position)
            {
                EdgeComparer = this.EdgeComparer
            };
            return newVertex;
        }
    }

    public class TriangulationVertex<T> : TriangulationVertex, IVertex2D<T>
    {
        public T Data { get; set; }

        public TriangulationVertex(int index, Vector2 p, T data) : base(index, p)
        {
            Data = data;
        }

        public TriangulationVertex(int index, Vector2 p) : base(index, p)
        {
        }

        public override IVertex ShallowCopy()
        {
            TriangulationVertex<T> newVertex = new(Index, Position, Data)
            {
                EdgeComparer = this.EdgeComparer
            };
            return newVertex;
        }
    }


    public class MeshVertexComparerXY<VERTEX>(IMesh<VERTEX> mesh) : IComparer<long>
        where VERTEX : IVertex2D
    {
        private readonly IMesh<VERTEX> Mesh = mesh;

        public int Compare(long A, long B) => Vector2ComparerXY.CompareXY(Mesh[A].Position, Mesh[B].Position);
    }

    public class MeshVertexComparerYX<VERTEX>(IMesh<VERTEX> mesh) : IComparer<long>
         where VERTEX : IVertex2D
    {
        private readonly IMesh<VERTEX> Mesh = mesh;

        public int Compare(long A, long B) => Vector2ComparerYX.CompareYX(Mesh[A].Position, Mesh[B].Position);
    }

    public class ConstrainedEdge : Edge
    {
        public ConstrainedEdge(EdgeKey key) : base(key)
        {
        }

        public ConstrainedEdge(IEdgeKey key) : base(key)
        {
        }

        public ConstrainedEdge(int a, int b) : base(a, b)
        {
        }

        public override IEdge Clone()
        {
            ConstrainedEdge e = new(this.Key);
            return e;
        }
    }


    public class TriangleFace : Face, ITriangleFace
    {
        public int A => iVerts[0];

        public int B => iVerts[1];

        public int C => iVerts[2];

        public EdgeKey AB => new(A, B);

        public EdgeKey BC => new(B, C);

        public EdgeKey CA => new(C, A);

        public override IFace Clone()
        {
            TriangleFace f = new(this.iVerts, this.Edges);
            return f;
        }

        public TriangleFace(int A, int B, int C) : base(A, B, C)
        {
        }

        protected TriangleFace(IEnumerable<int> vertex_indicies, IEnumerable<IEdgeKey> edges) : base(vertex_indicies, edges)
        {
            if (this.iVerts.Length != 3)
                throw new ArgumentException($"Three verticies required for triangle face, got {this}");
        }

        public TriangleFace(IEnumerable<int> vertex_indicies) : base(vertex_indicies)
        {
            if (this.iVerts.Length != 3)
                throw new ArgumentException($"Three verticies required for triangle face, got {this}");
        }

        /// <summary>
        /// For a triangular face, return the vertex opposite the edge
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public int OppositeVertex(IEdgeKey edge)
        {
            Debug.Assert(this.IsTriangle());
            Debug.Assert(this.iVerts.Contains(edge.A) && this.iVerts.Contains(edge.B));

            for (int i = 0; i < iVerts.Length; i++)
            {
                if (iVerts[i] != edge.A && iVerts[i] != edge.B)
                    return iVerts[i];
            }

            return int.MinValue;
        }

        /// <summary>
        /// For a triangular face, return the vertex opposite the edge
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public IEdgeKey OppositeEdge(int vertex)
        {
            Debug.Assert(this.IsTriangle());
            Debug.Assert(this.iVerts.Contains(vertex));

            if (this.iVerts[0] == vertex)
            {
                return new EdgeKey(iVerts[1], iVerts[2]);
            }
            else if (iVerts[1] == vertex)
            {
                return new EdgeKey(iVerts[0], iVerts[2]);
            }
            else if (iVerts[2] == vertex)
            {
                return new EdgeKey(iVerts[0], iVerts[1]);
            }

            throw new ArgumentException("Vertex not found in face");
        }

        /// <summary>
        /// Returns two CCW triangles that would result from flipping edge.  Edge must have two faces
        /// 
        /// This function does not change the mesh, it indicates how the mesh would change
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Tuple<TriangleFace, TriangleFace> Flip(IEdge edge)
        {
            if (edge.Faces.Count != 2)
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has one face {1}", edge, edge.Faces.First()));

            if (!edge.Faces.All(f => f.IsTriangle()))
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has non-triangular face: {1} and/or {2}", edge, edge.Faces.First(), edge.Faces.Last()));

            TriangleFace f1 = edge.Faces[0] as TriangleFace;
            TriangleFace f2 = edge.Faces[1] as TriangleFace;

            Edge newEdge = new(f1.OppositeVertex(edge), f2.OppositeVertex(edge));

            //TODO: We need to ensure that the edge we are flippig is convex.  We cannot flip a concave quad along the interior edge.

            return Flip(edge, newEdge);
        }

        /// <summary>
        /// Returns two CCW triangles that would result from flipping edge.  Edge must have two faces.
        /// 
        /// This function does not change the mesh, it indicates how the mesh would change
        /// </summary>
        /// <param name="edge"></param>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static Tuple<TriangleFace, TriangleFace> Flip(IEdge existing, IEdge flipped)
        {
            if (existing.Faces.Count > 2)
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has more than two faces", existing));
            //return null;

            if (existing.Faces.Count < 2)
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has one face {1}", existing, existing.Faces.First()));

            if (!existing.Faces.All(f => f.IsTriangle()))
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has non-triangular face: {1} and/or {2}", existing, existing.Faces.First(), existing.Faces.Last()));

            TriangleFace f1 = existing.Faces[0] as TriangleFace;
            TriangleFace f2 = existing.Faces[1] as TriangleFace;

            InfiniteSequentialIndexSet TriangleIndexer = new(0, 3, 0);

            TriangleFace n1;
            TriangleFace n2;

            Debug.Assert(f1.iVerts.Contains(flipped.A) || f1.iVerts.Contains(flipped.B));
            Debug.Assert(f2.iVerts.Contains(flipped.A) || f2.iVerts.Contains(flipped.B));

            if (f1.iVerts.Contains(flipped.A))
            {
                int iFlipEndpointA = f1.iVerts.IndexOf(flipped.A);
                n1 = new TriangleFace(f1.iVerts[iFlipEndpointA], f1.iVerts[(int)TriangleIndexer[iFlipEndpointA + 1]], flipped.B);
                n2 = new TriangleFace(f1.iVerts[(int)TriangleIndexer[iFlipEndpointA - 1]], f1.iVerts[iFlipEndpointA], flipped.B);
            }
            else
            {
                int iFlipEndpointB = f1.iVerts.IndexOf(flipped.B);
                n1 = new TriangleFace(f1.iVerts[iFlipEndpointB], f1.iVerts[(int)TriangleIndexer[iFlipEndpointB + 1]], flipped.A);
                n2 = new TriangleFace(f1.iVerts[(int)TriangleIndexer[iFlipEndpointB - 1]], f1.iVerts[iFlipEndpointB], flipped.A);
            }

            return new Tuple<TriangleFace, TriangleFace>(n1, n2);
        }
    }

    public class TriangulationMesh<VERTEX> : Mesh2D<VERTEX>
        where VERTEX : IVertex2D
    {
        //internal IComparer<IEdgeKey> edgeAngleComparer;
        public delegate void ProgressUpdate(TriangulationMesh<VERTEX> mesh);

        internal SortedSet<long> _XSorted;
        internal SortedSet<long> _YSorted;

        private long[] _XSortedArrayCache = null;
        public long[] XSorted
        {
            get
            {
                if (_XSortedArrayCache is null)
                {
                    _XSortedArrayCache = new long[_XSorted.Count];
                    _XSorted.CopyTo(_XSortedArrayCache);
                }

                return _XSortedArrayCache;
            }
        }

        private long[] _YSortedArrayCache = null;
        public long[] YSorted
        {
            get
            {
                if (_YSortedArrayCache is null)
                {
                    _YSortedArrayCache = new long[_YSorted.Count];
                    _YSorted.CopyTo(_YSortedArrayCache);
                }

                return _YSortedArrayCache;
            }
        }

        public RTree.RTree<long> rTree = new();
        public RTree.RTree<IEdgeKey> EdgeRTree = new();

        public TriangulationMesh()
        {
            _XSorted = new SortedSet<long>(new MeshVertexComparerXY<VERTEX>(this));
            _YSorted = new SortedSet<long>(new MeshVertexComparerYX<VERTEX>(this));
        }

        /// <summary>
        /// A constructor used to clone an existing TriangulationMesh.
        /// For example, translation of vertex positions with a rigid transform.
        /// </summary>
        public TriangulationMesh<VERTEX> Clone(IEnumerable<VERTEX> verts, IEnumerable<IEdge> edges, IEnumerable<IFace> face)
        {
            TriangulationMesh<VERTEX> mesh = new();

            // Add vertices with their existing indices
            foreach (var vert in verts)
            {
                mesh.AddVertex(vert);
            }

            // Add edges
            foreach (var edge in edges)
            {
                mesh.AddEdge(edge);
            }

            // Add faces
            foreach (var f in face)
            {
                mesh.AddFace(f);
            }

            return mesh;
        }

        public override int AddVertex(VERTEX vert)
        {
            int iNew = base.AddVertex(vert);
            //_XSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new Vector2ComparerXY());
            //_YSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new Vector2ComparerYX());
            _XSorted.Add(iNew);
            _YSorted.Add(iNew);
            _XSortedArrayCache = null;
            _YSortedArrayCache = null;

            rTree.Add(vert.Position.ToRTreeRect(0), iNew);
            return iNew;
        }

        public override int AddVerticies(IEnumerable<VERTEX> verts)
        {
            int iNew = base.AddVerticies(verts);
            //_XSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new Vector2ComparerXY());
            //_YSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new Vector2ComparerYX());
            _XSorted.UnionWith(verts.Select(v => (long)v.Index));
            _YSorted.UnionWith(verts.Select(v => (long)v.Index));
            _XSortedArrayCache = null;
            _YSortedArrayCache = null;

            foreach (var v in verts)
            {
                rTree.Add(v.Position.ToRTreeRect(0), v.Index);
            }

            return iNew;
        }


        public override void AddEdge(IEdge e)
        {
            if (this.Contains(e))
                return;

            LineSegment seg = this.ToLineSegment(e);

#if DEBUG
            try
            {
                var intersected = this.FindIntersectingEdges(e, out List<IEdgeKey> intersected_edges);
                if (intersected_edges.Count > 0)
                {
                    throw new EdgesIntersectTriangulationException(e, [.. intersected_edges.Select(edge => (IEdgeKey)edge)],
                        $"New edge {e} intersects existing edges: {intersected_edges[0]}\n" +
                            $"{this[e.A]} <-> {this[e.B]}\n" +
                            $"{this[intersected_edges[0].A]} <-> {this[intersected_edges[0].B]}");
                }
            }
            catch (EdgeIntersectsVertexException)
            {
                //Should I add two edges?
                throw;
            }
#endif

            base.AddEdge(e);
            EdgeRTree.Add(seg.BoundingBox.ToRTreeRect(0), e);
        }

        public override void RemoveEdge(IEdgeKey e)
        {
            base.RemoveEdge(e);
            EdgeRTree.Delete(e, out IEdgeKey found);
        }


        public override void AddFace(IFace face)
        {
            Debug.Assert(face.IsTriangle(), "Faces in TriangulationMesh must be triangles");
#if DEBUG
            Triangle tri = this.ToTriangle(face);
            //Debug.Assert(tri.Area > 0, string.Format("Face {0} must have non-zero area", face));
            if (tri.Area == 0)
                //return;
                throw new ArgumentException(string.Format("Face {0} must have non-zero area", face));

#endif
            base.AddFace(face);

        }

        public bool IsTriangleDelaunay(Face f)
        {
            if (f.IsTriangle == false)
            {
                throw new ArgumentException(string.Format("Face passed to IsTriangleDelaunay must be a triangle {0}", f));
            }

            Vector2[] verts = [.. this[f].Select(v => v.Position)];

            Circle circle = Circle.CircleFromThreePoints(verts);

            //Build a list of possible violations of the delaunay rule, and then remove the triangle verticies themselves.
            long[] candidate_indicies = [.. this.rTree.Intersects(circle.BoundingBox.ToRTreeRect(0)).Where(c => f.iVerts.Contains((int)c) == false)];
            Vector2[] candidates = [.. this[candidate_indicies].Select(v => v.Position)];

            ShapeRelation[] results = Circle.Contains(verts, candidates);

            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] == ShapeRelation.Contained)
                {
                    //If all points are equidistant then don't call this a failure
                    double distanceSquared = Vector2.DistanceSquared(in circle.Center, in candidates[i]);
                    if (Math.Abs(distanceSquared - circle.RadiusSquared) < Global.EpsilonSquared)
                    {
                        continue;
                    }

#if TRACEDELAUNAY
                    //TODO: Check that we create the reciprocal of the circle with this point and the other two shared points that we aren't also contained
                    Debug.WriteLine(string.Format("{0} is inside {1}, not a delaunay triangle", candidate_indicies[i], f));
#endif
                    return false;
                }
            }

            return true;
        }

        public void EdgeToVertAngle(IEdge e, IVertex2D p)
        {
            Line line = ToLine(e);
        }

        public bool FindIntersectingEdges(IEdgeKey e, out List<IEdgeKey> foundEdges)
        {
            foundEdges = [];

            LineSegment seg = this.ToLineSegment(e);

            var candidates = EdgeRTree.Intersects(seg.BoundingBox.ToRTreeRect(0));

            foreach (var candidate in candidates)
            {
                if (candidate.Equals(e))
                    continue;

                if (candidate.Adjacent(e)) //If we share a vertex with the edge we won't count it as an intersection
                    continue;

                LineSegment candidate_seg = this.ToLineSegment(candidate);

                if (candidate_seg.Intersects(in seg, false, out IShape2D intersection))
                {
                    foundEdges.Add(candidate);
                    /*
                    if (intersection.ShapeType == ShapeType2D.Point)
                    {
                        IPoint2D iPoint = intersection as IPoint2D;
                        if (candidate_seg.IsEndpoint(iPoint))
                        {
                            int iIntersectedVert = candidate_seg.A == iPoint ? candidate.A : candidate.B;
                            throw new EdgeIntersectsVertexException(iIntersectedVert, string.Format("Edge {0} passes directly through vertex {1}", e, iIntersectedVert));
                        }
                    }

                    intersected_edges.Add(oppEdge);
                    */
                }
            }

            return foundEdges.Count > 0;

        }

        /// <summary>
        /// Add a constrained edge to the mesh.  If the edge A,B perfectly intersects a vertex C anywhere other than an 
        /// endpoint we will add two constrained edges, A,C & C,B.
        /// </summary>
        /// <param name="constrained_edge"></param>
        /// <param name="ReportProgress"></param>
        /// <returns>A list of edges added.  This is empty if the edge was already in the mesh and a constrained edge.  It may have two or more entries if the constrained edge intersected verticies.</returns>
        public List<IEdge> AddConstrainedEdge(IEdge constrained_edge, ProgressUpdate ReportProgress = null)
        {
            List<IEdge> EdgesAdded = new(1);
            //If the edge already exists, just return
#if TRACEDELAUNAY
            Trace.WriteLine(string.Format("Add constrained edge {0}", constrained_edge));
#endif

            //Delete all triangles that intersect the constrained edge
            if (this.Contains(constrained_edge))
            {
                IEdge existingEdge = this[constrained_edge.Key];
                if (existingEdge as ConstrainedEdge != null)
                {
                    //The edge exists and is already a Constrained Edge
                    return EdgesAdded;
                }
                else
                {
                    //Remove the non-constrained edge, and add back a constrained edge
                    var faces = existingEdge.Faces;
                    this.RemoveEdge(existingEdge);
                    this.AddEdge(constrained_edge);
                    foreach (var face in faces)
                    {
                        this.AddFace(face);
                    }

                    return EdgesAdded;
                }
            }

            LineSegment ConstrainedEdge = this.ToLineSegment(constrained_edge);

            List<IEdge> IntersectedEdges;
            try
            {
                IntersectedEdges = FindIntersectingFaceEdges(constrained_edge);
            }
            catch (EdgeIntersectsVertexException e)
            {
                //If we intersect a vertex perfectly, break the constrained edge into two parts and add both
                EdgesAdded.AddRange(AddConstrainedEdge(new ConstrainedEdge(constrained_edge.A, e.Vertex), ReportProgress));
                EdgesAdded.AddRange(AddConstrainedEdge(new ConstrainedEdge(e.Vertex, constrained_edge.B), ReportProgress));
                return EdgesAdded;
            }
            //Special case: If there is only a single edge we can do an edge flip and be done

            List<IEdge> CreatedEdges = [];

            //Quads we have tested and know flipping will not produce faces that do not cross the constraining edge.
            //In the loop below we will test untested quads before we test quads we've tried before.
            //There are edge cases where the order of edge testing matters, and we can get stuck in an endless loop if 
            //we keep testing edges in the same order
            HashSet<Face> testedQuads = [];

            int iEdge = IntersectedEdges.Count - 1;

            int IntersectedEdgeCountAtCycleStart = IntersectedEdges.Count;

            while (IntersectedEdges.Count > 0)
            {
                //IF we've checked the last edge in the list, then loop back and check if the remaining edges have changed from our other flips
                if (iEdge < 0)
                {
                    //OK, we are going to start a new pass through the loop. 
                    //Sort the list so we check any untested quads before retesting any edges

                    //Handle an edge case where edges do not intersect after flipping some edges
                    IntersectedEdges = FindIntersectingFaceEdges(constrained_edge);
                    if (IntersectedEdges.Count == 0)
                    {
                        break;
                    }

                    bool AnyEdgesRemovedThisCycle = IntersectedEdgeCountAtCycleStart != IntersectedEdges.Count;
                    //Did we make any progress or are we stuck?
                    if (AnyEdgesRemovedThisCycle == false)
                    {
                        break; //Edge case: We didn't make any progress.  Break to escape the loop. Probably from floating point error
                    }

                    iEdge = IntersectedEdges.Count - 1;
                    IntersectedEdgeCountAtCycleStart = IntersectedEdges.Count;

                    bool[] newQuad = [.. IntersectedEdges.Select((e) =>
                    {
                        Face q = new(((Edge)e).FacesBoundary());
                        return testedQuads.Contains(q);
                    })];

                    IntersectedEdges = [.. IntersectedEdges.OrderByDescending(e =>
                    {
                        Face q = new(((Edge)e).FacesBoundary());
                        return testedQuads.Contains(q);
                    })];
                }

                Edge edge = IntersectedEdges[iEdge] as Edge;

#if TRACEDELAUNAY
                Trace.WriteLine(string.Format("Check intersecting edge {0}", edge));
#endif

                //Figure out which endpoint is on which side of the line and populated the left/right list appropriately
                //VERTEX A = this[edge.A];
                //VERTEX B = this[edge.B];

                //Is the quad formed by the two faces of the edge convex?
                if (edge.Faces.Count != 2)
                {
                    string error = string.Format("Expect two faces for any edge removed for intersecting an edge constraint. {0} intersected edge {1}", constrained_edge, edge);
                    error += edge.Faces.Count > 0 ? string.Format(" with one face {0}", edge.Faces[0]) :
                                                           " with no faces";
                    throw new InvalidOperationException(error);
                }

                //Debug.Assert(edge.Faces.Count() == 2, "Expect two faces for any edge intersecting an edge constraint.");

                TriangleFace A = edge.Faces[0] as TriangleFace;
                TriangleFace B = edge.Faces[1] as TriangleFace;

                int[] quadVerts = edge.FacesBoundary();
                Face quad = new(quadVerts);
                testedQuads.Add(quad);

                Polygon poly = new(quadVerts.Select(v => this[v].Position).ToArray().EnsureClosedRing());

                //We cannot flip the edges if the polygon is not convex
                Concavity[] concavity = [.. poly.VertexConcavity(out double[] angles)];

                if (false == concavity.All(c => c == Concavity.Convex || c == Concavity.Parallel))
                {

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("Concave quad {0}, moving on", new Face(quadVerts)));
#endif
                    iEdge -= 1;
                    continue;
                }
                else
                {


                    int[] oppVerts = [A.OppositeVertex(edge), B.OppositeVertex(edge)];
                    //Flip the edge, check if the new edge still intersects the ConstraintEdge
                    var NewFacesTuple = TriangleFace.Flip(edge);

                    bool HasParallelEdges = concavity.Any(c => c == Concavity.Parallel);
                    if (HasParallelEdges)
                    {
                        double A_Area = this.ToTriangle(NewFacesTuple.Item1).Area;
                        double B_Area = this.ToTriangle(NewFacesTuple.Item2).Area;
                        if (A_Area <= Global.Epsilon || double.IsNaN(A_Area) ||
                           B_Area <= Global.Epsilon || double.IsNaN(B_Area))
                        {
                            iEdge -= 1;
                            continue;
                        }
                    }

                    Edge newEdge = new(oppVerts[0], oppVerts[1]);
                    if (newEdge == constrained_edge)
                    {
                        newEdge = new ConstrainedEdge(oppVerts[0], oppVerts[1]);
                        EdgesAdded.Add(constrained_edge);
                    }

                    //We are safe flipping the edge so remove this edge from the list of intersecting edges
                    IntersectedEdges.RemoveAt(iEdge);
                    iEdge -= 1;

                    this.RemoveEdge(edge);
                    this.AddEdge(newEdge);

                    this.AddFace(NewFacesTuple.Item1);
                    this.AddFace(NewFacesTuple.Item2);

#if TRACEDELAUNAY
                    Trace.WriteLine(string.Format("  Remove {0} Add {1} with faces {2} {3}", edge, newEdge, NewFacesTuple.Item1, NewFacesTuple.Item2));
#endif

                    ReportProgress?.Invoke(this);

                    //If the new edge intersects the constrained line, add it to the list of edges to check, 
                    //otherwise add it to the list of CreatedEdges
                    LineSegment newEdgeSeg = this.ToLineSegment(newEdge);
                    if (newEdge == constrained_edge)
                    {
#if TRACEDELAUNAY
                        Trace.WriteLine(string.Format(" {0} is constrained edge, moving on", newEdge));
#endif
                        continue;
                    }
                    else if (newEdgeSeg.Intersects(in ConstrainedEdge, true))
                    {
#if TRACEDELAUNAY
                        Trace.WriteLine(string.Format("  {0} intersects constraint {1}, adding to intersect list", newEdge, constrained_edge));
#endif
                        IntersectedEdges.Add(newEdge);
                    }
                    else
                    {
#if TRACEDELAUNAY
                        Trace.WriteLine(string.Format("  {0} added to created list", newEdge, constrained_edge));
#endif
                        CreatedEdges.Add(newEdge);
                    }
                }
            }

            //OK, phase 2, see if we can improve any triangles by flipping our created edges
            //CreatedEdges = CreatedEdges.Where(e => e != constrained_edge).ToList();
            iEdge = CreatedEdges.Count - 1;
            while (CreatedEdges.Count > 0)
            {

                iEdge = iEdge < 0 ? CreatedEdges.Count - 1 : iEdge;
                Edge edge = CreatedEdges[iEdge] as Edge;
                CreatedEdges.RemoveAt(iEdge);
                iEdge--;

                if (edge.Faces.Count < 2) //Should not be possible but added it as an edge case check because it was hit during bajaj generation
                    continue;

                TriangleFace A = edge.Faces[0] as TriangleFace;
                TriangleFace B = edge.Faces[1] as TriangleFace;

                /*
                bool CanAFlip = A.Edges.Count(e => this[e] as ConstrainedEdge != null) <= 1;
                bool CanBFlip = B.Edges.Count(e => this[e] as ConstrainedEdge != null) <= 1;

                if ((CanAFlip && CanBFlip) == false) //Don't check 
                    continue; 
                    */

                int[] oppVerts = [A.OppositeVertex(edge), B.OppositeVertex(edge)];
                int checkVert = oppVerts.Single(v => A.iVerts.Contains(v) == false);
                if (Circle.Contains([.. this[A.iVerts].Select(v => v.Position)], this[checkVert].Position) == ShapeRelation.Contained)
                {

                    //We need to ensure that the edge we are flippig is convex.  We cannot flip a concave quad along the interior edge or we get overlapping edges
                    int[] quad = edge.FacesBoundary();
                    List<Vector2> positionList = [.. this[quad].Select(v => v.Position)];
                    positionList.Add(positionList.First());

                    Polygon quadPoly = new(positionList);
                    if (false == quadPoly.IsConvex())
                    {
#if TRACEDELAUNAY
                        Trace.WriteLine(string.Format("  Cannot flip convex face {0}.  It's OK, just can't make triangle prettier", new Face(quad)));
#endif
                        continue;
                    }

                    //TODO:  This check can be removed to have an assertion thrown instead.  It should be done at some point to debug.
                    if (edge.Faces.Count != 2)
                    {
                        Trace.WriteLine($"Edge {edge} found without two faces when adding constrained edge");
                    }

                    //Flip the edge to improve the triangulation
                    var NewFacesTuple = TriangleFace.Flip(edge);

                    Edge newEdge = new(oppVerts[0], oppVerts[1]);

                    this.RemoveEdge(edge);
                    this.AddEdge(newEdge);

                    this.AddFace(NewFacesTuple.Item1);
                    this.AddFace(NewFacesTuple.Item2);

                    ReportProgress?.Invoke(this);

                    LineSegment newEdgeSeg = this.ToLineSegment(newEdge);
                    if (newEdge == constrained_edge)
                    {

                    }
                    else
                    {
                        CreatedEdges.Add(newEdge);
                    }
                }

            }

            return EdgesAdded;
        }

        /// <summary>
        /// Returns all edges that intersect the edge.  Excluding segments that only intersect at the start and origin vertex
        /// </summary>
        /// <param name="e"></param>
        public List<IEdge> FindIntersectingFaceEdges(IEdgeKey e)
        {
            //If the edge is already in the mesh return an empty list
            if (this.Contains(e))
                return [];

            List<IEdge> intersected_edges = [];
            long iStart = e.A;
            long iEnd = e.B;

            LineSegment ConstrainedEdge = this.ToLineSegment(e);

            VERTEX v = this[iStart];
            IEnumerable<IFace> faces = v.Edges.Where(vert_edge => e.Equals(vert_edge) == false).SelectMany(edge => this[edge].Faces).Distinct(); //Our edge may or may not be in the mesh, but we'll exclude any faces it is part of.

            foreach (var f in faces)
            {
                ITriangleFace face = f as ITriangleFace;
                IEdge oppEdge = this[face.OppositeEdge(v.Index)]; //Identify the edge we have a chance of intersecting.

                LineSegment oppEdgeSeg = this.ToLineSegment(oppEdge);

                //We should never intersect an endpoint, but if the mesh is not correct and an edge passes through our endpoint we may. 
                //if (ConstrainedEdge.Intersects(oppEdgeSeg, EndpointsOnRingDoNotIntersect: true)) 
                if (ConstrainedEdge.Intersects(in oppEdgeSeg, EndpointsOnRingDoNotIntersect: false, Intersection: out IShape2D intersection))
                {
                    //Todo: Handle endpoint intersection case
                    if (intersection.ShapeType == ShapeType2D.Point)
                    {
                        IPoint2D iPoint = intersection as IPoint2D;
                        if (oppEdgeSeg.IsEndpoint(iPoint))
                        {
                            int iIntersectedVert = oppEdgeSeg.A == iPoint ? oppEdge.A : oppEdge.B;
                            throw new EdgeIntersectsVertexException(iIntersectedVert, string.Format("Edge {0} passes directly through vertex {1}", e, iIntersectedVert));
                        }
                    }

                    intersected_edges.Add(oppEdge);

                    //The edge intersects, so check the opposite face for the next intersection, if any
                    FindIntersectingFaceEdges(face, e, ConstrainedEdge, oppEdge, ref intersected_edges);
                }
            }

            return intersected_edges;
        }

        private bool FindIntersectingFaceEdges(ITriangleFace previous_intersected_face, IEdgeKey constrained_edge, LineSegment constrained_seg, IEdge previous_intersected_edge, ref List<IEdge> intersected_edges)
        {
            bool new_edge_found = true;
            while (new_edge_found)
            {
                new_edge_found = false;

                if (previous_intersected_edge.OppositeFace(previous_intersected_face) is not ITriangleFace testFace)
                {
                    //Not sure how an edge that intersects a constrained edge can only have one face. Returning false for now.
                    //Later thought:  This could mean the endpoint is on the convex hull
                    // return false;
                    throw new NonconformingTriangulationException(previous_intersected_face, string.Format("Somehow an edge intersection test found an edge with only one face when testing vertex to vertex edge intersection.\nTested Edge: {0} Last Intersected Edge/Face: {1} / {2}", constrained_edge, previous_intersected_edge, previous_intersected_face));
                }

                //Check if our edge terminates on one of the opposite face's verticies
                //A bit of inside knowledge here, but the search starts at A and ends at B
                if (testFace.iVerts.Contains(constrained_edge.B))
                {
                    return true;
                }

                //Find the two edges of the opposite face our line could cross.

                foreach (var candidate in testFace.Edges.Select(e => this[e]))
                {
                    //Don't check the same edge again
                    if (candidate.Equals(previous_intersected_edge))
                        continue;

                    if (intersected_edges.Contains(candidate))
                        continue;

                    LineSegment candidateEdgeSeg = this.ToLineSegment(candidate);
                    if (constrained_seg.Intersects(in candidateEdgeSeg, EndpointsOnRingDoNotIntersect: false, Intersection: out IShape2D intersection))
                    {
                        intersected_edges.Add(candidate);

                        //Todo: Handle endpoint intersection case
                        if (intersection is IPoint2D inter && candidateEdgeSeg.IsEndpoint((IPoint2D)intersection))
                        {
                            int iIntersectedVert = candidateEdgeSeg.A == inter ? candidate.A : candidate.B;
                            //FindIntersectingEdges(testFace, constrained_edge, constrained_seg, candidate, ref intersected_edges);

                            throw new EdgeIntersectsVertexException(iIntersectedVert, string.Format("Edge {0} passes directly through vertex {1}", constrained_edge, iIntersectedVert));
                        }
                        else
                        {
                            //Update the last edge we found an continue
                            previous_intersected_edge = candidate;
                            previous_intersected_face = testFace;
                            new_edge_found = true;
                            break;
                        }
                    }
                }
            }

            return intersected_edges.Count > 0;
        }
    }

    public static class TriangleMeshExtensions<VERTEX, T>
       where VERTEX : IVertex2D, IVertex2D<T>
        where T : ICloneable
    {
        /// <summary>
        /// Returns a copy of the mesh with the verticies translated.
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="vector"></param>
        /// <param name="CloneData">If true, the data value of each vertex is cloned instead of referenced</param>
        /// <returns></returns>
        public static TriangulationMesh<VERTEX> Translate(TriangulationMesh<VERTEX> mesh, Vector2 vector, bool CloneData = false)
        {
            TriangulationMesh<VERTEX> triMesh = new();

            if (CloneData)
            {
                var translated_verts = mesh.Vertices.Select(v => (VERTEX)(IVertex2D<T>)new Vertex2D<T>(v.Index, v.Position + vector, (T)v.Data.Clone()));
                return triMesh.Clone(translated_verts, mesh.Edges.Values, mesh.Faces);
            }
            else
            {
                var translated_verts = mesh.Vertices.Select(v => (VERTEX)(IVertex2D<T>)new Vertex2D<T>(v.Index, v.Position + vector, v.Data));
                return triMesh.Clone(translated_verts, mesh.Edges.Values, mesh.Faces);
            }
        }
    }
}
