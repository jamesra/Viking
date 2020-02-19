//#define TRACEDDELAUNAY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Geometry.Meshing
{
    /// <summary>
    /// This exception is raised when a corresponding point perfectly a vertex that is not an endpoint of the edge.
    /// </summary>
    internal class CorrespondingEdgeIntersectsVertexException : Exception
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
    /// Closely related to the CompareAngle class.  In this version the vertex index can change and is determined by the 
    /// duplicate key in the compared IEdgeKeys
    /// </summary>
    public class MeshEdgeAngleComparer<VERTEX> : IComparer<IEdgeKey>
        where VERTEX : IVertex2D
    {
        IMesh2D<VERTEX> Mesh;
        public readonly bool ClockwiseOrder = true;

        /// <summary>
        /// A vector originating from 0,0.  It determines which edge is the first in the rotation order.
        /// </summary>
        GridVector2 OriginVector;


        public MeshEdgeAngleComparer(IMesh2D<VERTEX> mesh, GridLine origin_line_vector, bool clockwise = false) :
            this(mesh, origin_line_vector.Direction, clockwise)
        {
        }

        public MeshEdgeAngleComparer(IMesh2D<VERTEX> mesh, GridVector2 origin_line_vector, bool clockwise = false)
        {
            Mesh = mesh;
            OriginVector = origin_line_vector; 
            ClockwiseOrder = clockwise;
        }

        public int Compare(IEdgeKey A, IEdgeKey B)
        {
            int origin_vertex = A.A == B.A || A.A == B.B ? A.A : A.B;
            int APoint = A.OppositeEnd(origin_vertex);
            int BPoint = B.OppositeEnd(origin_vertex);

            GridVector2 Origin = Mesh[origin_vertex].Position;
            GridVector2 ComparisonPoint = Origin + OriginVector;

            double angleA = GridVector2.ArcAngle(Origin, Mesh[APoint].Position, ComparisonPoint);
            double angleB = GridVector2.ArcAngle(Origin, Mesh[BPoint].Position, ComparisonPoint);

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
        IMesh<VERTEX> Mesh; 
        public readonly bool ClockwiseOrder = true;

        private int OriginVertex; 
        /// <summary>
        /// A line originating from the vertex.  It determines which edge is the first in the rotation order.
        /// </summary>
        GridLine OriginLine;

        /// <summary>
        /// Precalculated comparison point used to compare angles
        /// </summary>
        private GridVector2 ComparisonPoint;

        public MeshEdgeAngleComparerFixedIndex(IMesh<VERTEX> mesh, int origin_vertex, IEdgeKey origin_line, bool clockwise = false) :
            this(mesh, origin_vertex, GridVector2.Normalize(mesh[origin_line.OppositeEnd(origin_vertex)].Position - mesh[origin_vertex].Position), clockwise)
        {
        }

        public MeshEdgeAngleComparerFixedIndex(IMesh<VERTEX> mesh, int origin_vertex, GridLine origin_line_vector, bool clockwise = false) :
            this(mesh, origin_vertex,  origin_line_vector.Direction, clockwise)
        { 
        }

        public MeshEdgeAngleComparerFixedIndex(IMesh<VERTEX> mesh, int origin_vertex, GridVector2 origin_line_vector, bool clockwise = false) 
        {
            Mesh = mesh;
            OriginVertex = origin_vertex;

            GridVector2 Origin = mesh[origin_vertex].Position;
            OriginLine = new GridLine(Origin, origin_line_vector);

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
            //return GridVector2.AbsArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint, ClockwiseOrder);            
        }

        /// <summary>
        /// Measure angle of edge to the origin vector
        /// </summary>
        /// <param name="A"></param>
        /// <returns></returns>
        public double MeasureAngle(long APoint)
        { 
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            return GridVector2.AbsArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint, ClockwiseOrder);
        }

        public int Compare(IEdgeKey A, IEdgeKey B)
        { 
            int APoint = A.OppositeEnd(OriginVertex);
            int BPoint = B.OppositeEnd(OriginVertex);
            //We are measuring the angle from the line in one direction, so don't allow negative angles
            double angleA = GridVector2.AbsArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint);
            double angleB = GridVector2.AbsArcAngle(OriginLine.Origin, Mesh[BPoint].Position, ComparisonPoint);

            return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
        }
    }

    public class TriangulationVertex : Vertex2D, IVertexSortEdgeByAngle
    {
        public TriangulationVertex(GridVector2 p, IComparer<IEdgeKey> edgeComparer = null) : base(p, edgeComparer)
        {
        }

        public IEnumerable<long> EdgesByAngle(IComparer<IEdgeKey> comparer, long origin_edge, bool clockwise)
        {
            if (false == this.Edges.Contains(new EdgeKey(this.Index, origin_edge)))
            {
                throw new ArgumentException("Non-existent edge passed as origin to EdgesByAngle.");
            }

            //Setting the comparer should update the order of the edges attribute only if necessary.
            this.EdgeComparer = comparer;

            long[] sortedEdges = this._Edges.Select(e => (long)e.OppositeEnd(this.Index)).ToArray();

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
            TriangulationVertex newVertex = new TriangulationVertex(Position);
            newVertex.EdgeComparer = this.EdgeComparer;
            return newVertex;
        }
    }

    public class TriangulationVertex<T> : TriangulationVertex, IVertex2D<T>
    {
        public T Data { get; set; }

        public TriangulationVertex(GridVector2 p, T data) : base(p)
        {

        }

        public TriangulationVertex(GridVector2 p) : base(p)
        {
        }

        public override IVertex ShallowCopy()
        {
            TriangulationVertex<T> newVertex = new TriangulationVertex<T>(Position, Data);
            newVertex.EdgeComparer = this.EdgeComparer;
            return newVertex;
        } 
    }


    public class MeshVertexComparerXY<VERTEX> : IComparer<long>
        where VERTEX : IVertex2D
    {
        private IMesh<VERTEX> Mesh; 

        public MeshVertexComparerXY(IMesh<VERTEX> mesh)
        {
            Mesh = mesh; 
        }

        public int Compare(long A, long B)
        {
            return GridVectorComparerXY.CompareXY(Mesh[A].Position, Mesh[B].Position);
        }
    }

    public class MeshVertexComparerYX<VERTEX> : IComparer<long>
         where VERTEX : IVertex2D
    {
        private IMesh<VERTEX> Mesh;

        public MeshVertexComparerYX(IMesh<VERTEX> mesh)
        {
            Mesh = mesh;
        }

        public int Compare(long A, long B)
        {
            return GridVectorComparerYX.CompareYX(Mesh[A].Position, Mesh[B].Position);
        }
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
            ConstrainedEdge e = new ConstrainedEdge(this.Key);
            return e;
        }
    }


    public class TriangleFace : Face, ITriangleFace
    {
        public int A
        {
            get { return iVerts[0]; }
        }

        public int B
        {
            get { return iVerts[1]; }
        }

        public int C
        {
            get { return iVerts[2]; }
        }

        public EdgeKey AB
        {
            get { return new EdgeKey(A, B); }
        }

        public EdgeKey BC
        {
            get { return new EdgeKey(B,C); }
        }
        public EdgeKey CA
        {
            get { return new EdgeKey(C,A); }
        }

        public override IFace Clone()
        {
            var f = new TriangleFace(this.iVerts, this.Edges);
            return f;
        }

        public TriangleFace(int A, int B, int C) : base(A,B,C)
        {
        }

        protected TriangleFace(IEnumerable<int> vertex_indicies, IEnumerable<IEdgeKey> edges) : base(vertex_indicies, edges)
        {
            if (this.iVerts.Length != 3)
                throw new ArgumentException(string.Format("Three verticies required for triangle face, got {0}", this.ToString()));
        }

        public TriangleFace(IEnumerable<int> vertex_indicies) : base(vertex_indicies)
        {
            if (this.iVerts.Length != 3)
                throw new ArgumentException(string.Format("Three verticies required for triangle face, got {0}", this.ToString()));
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
            if (edge.Faces.Count() != 2)
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has one face {1}", edge, edge.Faces.First()));
            
            if(!edge.Faces.All(f => f.IsTriangle()))
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has non-triangular face: {1} and/or {2}", edge, edge.Faces.First(), edge.Faces.Last()));

            TriangleFace f1 = edge.Faces[0] as TriangleFace;
            TriangleFace f2 = edge.Faces[1] as TriangleFace;

            Edge newEdge = new Edge(f1.OppositeVertex(edge), f2.OppositeVertex(edge));

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
            if (existing.Faces.Count() != 2)
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has one face {1}", existing, existing.Faces.First()));

            if (!existing.Faces.All(f => f.IsTriangle()))
                throw new ArgumentException(string.Format("Edge cannot flip unless it has two triangular faces. {0} has non-triangular face: {1} and/or {2}", existing, existing.Faces.First(), existing.Faces.Last()));

            TriangleFace f1 = existing.Faces[0] as TriangleFace;
            TriangleFace f2 = existing.Faces[1] as TriangleFace;

            InfiniteSequentialIndexSet TriangleIndexer = new InfiniteSequentialIndexSet(0, 3, 0);

            TriangleFace n1;
            TriangleFace n2;

            Debug.Assert(f1.iVerts.Contains(flipped.A) || f1.iVerts.Contains(flipped.B));
            Debug.Assert(f2.iVerts.Contains(flipped.A) || f2.iVerts.Contains(flipped.B));

            if (f1.iVerts.Contains(flipped.A))
            {
                int iFlipEndpointA = f1.iVerts.IndexOf(flipped.A);
                n1 = new TriangleFace(f1.iVerts[iFlipEndpointA], f1.iVerts[(int)TriangleIndexer[iFlipEndpointA + 1]], flipped.B);
                n2 = new TriangleFace(f1.iVerts[(int)TriangleIndexer[iFlipEndpointA -1]], f1.iVerts[iFlipEndpointA], flipped.B);
            }
            else
            {
                int iFlipEndpointB = f1.iVerts.IndexOf(flipped.B);
                n1 = new TriangleFace(f1.iVerts[iFlipEndpointB], f1.iVerts[(int)TriangleIndexer[iFlipEndpointB + 1]], flipped.A);
                n2 = new TriangleFace(f1.iVerts[(int)TriangleIndexer[iFlipEndpointB -1]], f1.iVerts[iFlipEndpointB], flipped.A);
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
                if (_XSortedArrayCache == null)
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
                if (_YSortedArrayCache == null)
                {
                    _YSortedArrayCache = new long[_YSorted.Count];
                    _YSorted.CopyTo(_YSortedArrayCache);
                }

                return _YSortedArrayCache;
            }
        }
          
        public RTree.RTree<long> rTree = new RTree.RTree<long>();

        public TriangulationMesh()
        {
            //edgeAngleComparer = new MeshEdgeAngleComparer<VERTEX>(this, GridVector2.UnitY);
            _XSorted = new SortedSet<long>(new MeshVertexComparerXY<VERTEX>(this));
            _YSorted = new SortedSet<long>(new MeshVertexComparerYX<VERTEX>(this));
        }
          
        public override int AddVertex(VERTEX vert)
        {
            int iNew = base.AddVertex(vert);
            //_XSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new GridVectorComparerXY());
            //_YSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new GridVectorComparerYX());
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
            //_XSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new GridVectorComparerXY());
            //_YSorted = this._Verticies.Select(v => v.Position).SortAndIndex(new GridVectorComparerYX());
            _XSorted.UnionWith(verts.Select(v => (long)v.Index));
            _YSorted.UnionWith(verts.Select(v => (long)v.Index));
            _XSortedArrayCache = null;
            _YSortedArrayCache = null;

            foreach(var v in verts)
            {
                rTree.Add(v.Position.ToRTreeRect(0), v.Index);
            }

            return iNew;
        }

        public bool IsTriangleDelaunay(Face f)
        {
            if(f.IsTriangle == false)
            {
                throw new ArgumentException(string.Format("Face passed to IsTriangleDelaunay must be a triangle {0}", f));
            }

            GridVector2[] verts = this[f].Select(v => v.Position).ToArray();

            GridCircle circle = GridCircle.CircleFromThreePoints(verts);

            //Build a list of possible violations of the delaunay rule, and then remove the triangle verticies themselves.
            long[] candidate_indicies = this.rTree.Intersects(circle.BoundingBox).Where(c => f.iVerts.Contains((int)c) == false).ToArray();
            GridVector2[] candidates = this[candidate_indicies].Select(v => v.Position).ToArray();

            OverlapType[] results = GridCircle.Contains(verts, candidates);

            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] == OverlapType.CONTAINED)
                {
                    Debug.WriteLine(string.Format("{0} is inside {1}, not a delaunay triangle", candidate_indicies[i], f));
                    return false;
                }
            }

            return true;
        }

        public void EdgeToVertAngle(IEdge e, IVertex2D p)
        {
            GridLine line = ToGridLine(e);
        }

        /// <summary>
        /// Add a constrained edge to the mesh.  If the edge A,B perfectly intersects a vertex C anywhere other than an 
        /// endpoint we will add two constrained edges, A,C & C,B.
        /// </summary>
        /// <param name="constrained_edge"></param>
        /// <param name="ReportProgress"></param>
        public void AddConstrainedEdge(IEdge constrained_edge, ProgressUpdate ReportProgress = null)
        {
            //If the edge already exists, just return
#if TRACEDDELAUNAY
            Trace.WriteLine(string.Format("Add constrained edge {0}", constrained_edge));
#endif

            //Delete all triangles that intersect the constrained edge
            if (this.Contains(constrained_edge))
            {
                return;
            }

            GridLineSegment ConstrainedEdge = this.ToGridLineSegment(constrained_edge);

            List<IEdge> IntersectedEdges;
            try
            {
                IntersectedEdges = FindIntersectingEdges(constrained_edge);
            }
            catch (CorrespondingEdgeIntersectsVertexException e)
            {
                //If we intersect a vertex perfectly, break the constrained edge into two parts and add both
                AddConstrainedEdge(new ConstrainedEdge(constrained_edge.A, e.Vertex), ReportProgress);
                AddConstrainedEdge(new ConstrainedEdge(e.Vertex, constrained_edge.B), ReportProgress);
                return;
            }
            //Special case: If there is only a single edge we can do an edge flip and be done

            List<IEdge> CreatedEdges = new List<IEdge>();

            //Quads we have tested and know flipping will not produce faces that do not cross the constraining edge.
            //In the loop below we will test untested quads before we test quads we've tried before.
            //There are edge cases where the order of edge testing matters, and we can get stuck in an endless loop if 
            //we keep testing edges in the same order
            HashSet<Face> testedQuads = new HashSet<Face>();

            int iEdge = IntersectedEdges.Count-1;
            while (IntersectedEdges.Count > 0)
            {
                //IF we've checked the last edge in the list, then loop back and check if the remaining edges have changed from our other flips
                if(iEdge < 0)
                {
                    //OK, we are going to start a new pass through the loop. 
                    //Sort the list so we check any untested quads before retesting any edges

                    //Handle an edge case where edges do not intersect after flipping some edges
                    IntersectedEdges = FindIntersectingEdges(constrained_edge);
                    if(IntersectedEdges.Count == 0)
                    {
                        break;
                    }

                    iEdge = IntersectedEdges.Count - 1;
                    bool[] newQuad = IntersectedEdges.Select((e) =>
                    {
                        Face q = new Face(((Edge)e).FacesBoundary());
                        return testedQuads.Contains(q);
                    }).ToArray();

                    IntersectedEdges = IntersectedEdges.OrderByDescending(e =>
                    {
                        Face q = new Face(((Edge)e).FacesBoundary());
                        return testedQuads.Contains(q);
                    }).ToList();
                } 

                Edge edge = IntersectedEdges[iEdge] as Edge;

#if TRACEDDELAUNAY
                Trace.WriteLine(string.Format("Check intersecting edge {0}", edge));
#endif

                //Figure out which endpoint is on which side of the line and populated the left/right list appropriately
                //VERTEX A = this[edge.A];
                //VERTEX B = this[edge.B];

                //Is the quad formed by the two faces of the edge convex?
                Debug.Assert(edge.Faces.Count() == 2, "Expect two faces for any edge intersecting an edge constraint.");

                TriangleFace A = edge.Faces[0] as TriangleFace;
                TriangleFace B = edge.Faces[1] as TriangleFace;


                int[] quadVerts = edge.FacesBoundary();
                Face quad = new Face(quadVerts);
                testedQuads.Add(quad);

                GridPolygon poly = new GridPolygon(quadVerts.Select(v => this[v].Position).ToArray().EnsureClosedRing());

                //We cannot flip the edges if the polygon is not convex
                Concavity[] concavity = poly.VertexConcavity(out double[] angles).ToArray();
                
                if (false == concavity.All(c => c == Concavity.CONVEX || c == Concavity.PARALLEL))
                {

#if TRACEDDELAUNAY
                    Trace.WriteLine(string.Format("Concave quad {0}, moving on", quadVerts));
#endif
                    iEdge -= 1;
                    continue;
                }
                else
                {
                    int[] oppVerts = new int[] { A.OppositeVertex(edge), B.OppositeVertex(edge) };
                    //Flip the edge, check if the new edge still intersects the ConstraintEdge
                    var NewFacesTuple = TriangleFace.Flip(edge);

                    bool HasParallelEdges = concavity.Any(c => c == Concavity.PARALLEL);
                    if(HasParallelEdges)
                    {
                        if(this.ToTriangle(NewFacesTuple.Item1).Area == 0 ||
                           this.ToTriangle(NewFacesTuple.Item2).Area == 0)
                        {
                            iEdge -= 1;
                            continue;
                        }
                    }

                    Edge newEdge = new Edge(oppVerts[0], oppVerts[1]);
                    if(newEdge == constrained_edge)
                    {
                        newEdge = new ConstrainedEdge(oppVerts[0], oppVerts[1]); 
                    }
                    
                    //We are safe flipping the edge so remove this edge from the list of intersecting edges
                    IntersectedEdges.RemoveAt(iEdge);
                    iEdge -= 1;

                    this.RemoveEdge(edge);
                    this.AddEdge(newEdge);

                    this.AddFace(NewFacesTuple.Item1);
                    this.AddFace(NewFacesTuple.Item2);
                     
#if TRACEDDELAUNAY
                    Trace.WriteLine(string.Format("  Remove {0} Add {1}", edge, newEdge));
#endif

                    if (ReportProgress != null)
                    {
                        ReportProgress(this);
                    }

                    //If the new edge intersects the constrained line, add it to the list of edges to check, 
                    //otherwise add it to the list of CreatedEdges
                    GridLineSegment newEdgeSeg = this.ToGridLineSegment(newEdge);
                    if(newEdge == constrained_edge)
                    {
#if TRACEDDELAUNAY
                        Trace.WriteLine(string.Format(" {0} is constrained edge, moving on", newEdge));
#endif
                        continue;
                    }
                    else if (newEdgeSeg.Intersects(ConstrainedEdge, true))
                    {
#if TRACEDDELAUNAY
                        Trace.WriteLine(string.Format("  {0} intersects constraint {1}, adding to intersect list", newEdge, constrained_edge));
#endif
                        IntersectedEdges.Add(newEdge);
                    }
                    else
                    {
#if TRACEDDELAUNAY
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

                TriangleFace A = edge.Faces[0] as TriangleFace;
                TriangleFace B = edge.Faces[1] as TriangleFace;

                int[] oppVerts = new int[] { A.OppositeVertex(edge), B.OppositeVertex(edge) };
                if (GridCircle.Contains(this[A.iVerts].Select(v => v.Position).ToArray(), this[oppVerts[0]].Position) == OverlapType.CONTAINED)
                {
                    //Flip the edge to improve the triangulation
                    var NewFacesTuple = TriangleFace.Flip(edge);

                    Edge newEdge = new Edge(oppVerts[0], oppVerts[1]);

                    this.RemoveEdge(edge);
                    this.AddEdge(newEdge);

                    this.AddFace(NewFacesTuple.Item1);
                    this.AddFace(NewFacesTuple.Item2);

                    if (ReportProgress != null)
                    {
                        ReportProgress(this);
                    }

                    GridLineSegment newEdgeSeg = this.ToGridLineSegment(newEdge);
                    if (newEdge == constrained_edge)
                    {
                        
                    }
                    else
                    {
                        CreatedEdges.Add(newEdge);
                    }
                }

                iEdge = iEdge - 1;
            }
        }

        /// <summary>
        /// Delete all triangles that intersect the edge
        /// </summary>
        /// <param name="e"></param>
        private List<IEdge> FindIntersectingEdges(IEdge e)
        {
            //If the edge is already in the mesh return an empty list
            if (this.Contains(e))
                return new List<IEdge>();

            List<IEdge> intersected_edges = new List<IEdge>();
            long iStart = e.A;
            long iEnd = e.B;

            GridLineSegment ConstrainedEdge = this.ToGridLineSegment(e);

            VERTEX v = this[iStart];
            IEnumerable<IFace> faces = v.Edges.SelectMany(edge => this[edge].Faces).Distinct();

            foreach(var f in faces)
            {
                ITriangleFace face = f as ITriangleFace;
                IEdge oppEdge = this[face.OppositeEdge(v.Index)];

                GridLineSegment oppEdgeSeg = this.ToGridLineSegment(oppEdge);

                //We should never intersect an endpoint, but if the mesh is not correct and an edge passes through our endpoint we may. 
                //if (ConstrainedEdge.Intersects(oppEdgeSeg, EndpointsOnRingDoNotIntersect: true)) 
                if (ConstrainedEdge.Intersects(oppEdgeSeg, EndpointsOnRingDoNotIntersect: false))
                {
                    intersected_edges.Add(oppEdge);
                    //Todo: Handle endpoint intersection case

                    //The edge intersects, so check the opposite face for the next intersection, if any
                    FindIntersectingEdges(face, e, ConstrainedEdge, oppEdge, ref intersected_edges);
                }
            }

            return intersected_edges;
        }

        private bool FindIntersectingEdges(ITriangleFace previous_intersected_face, IEdge constrained_edge, GridLineSegment constrained_seg, IEdge previous_intersected_edge, ref List<IEdge> intersected_edges)
        {
            bool new_edge_found = true;
            while (new_edge_found)
            {
                new_edge_found = false;
                ITriangleFace testFace = previous_intersected_edge.OppositeFace(previous_intersected_face) as ITriangleFace;

                if (testFace == null)
                {
                   //Not sure how an edge that intersects a constrained edge can only have one face. Returning false for now.
                    return false;
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
                    if (candidate == previous_intersected_edge)
                        continue;

                    if (intersected_edges.Contains(candidate))
                        continue;

                    GridLineSegment candidateEdgeSeg = this.ToGridLineSegment(candidate);
                    if (constrained_seg.Intersects(candidateEdgeSeg, EndpointsOnRingDoNotIntersect: false, Intersection: out IShape2D intersection))
                    {
                        intersected_edges.Add(candidate);

                        //Todo: Handle endpoint intersection case
                        if(intersection as IPoint2D != null && candidateEdgeSeg.IsEndpoint((IPoint2D)intersection))
                        {
                            int iIntersectedVert = candidateEdgeSeg.A == (IPoint2D)intersection ? candidate.A : candidate.B;
                            //FindIntersectingEdges(testFace, constrained_edge, constrained_seg, candidate, ref intersected_edges);

                            throw new CorrespondingEdgeIntersectsVertexException(iIntersectedVert, "Constrained edge passes directly through vertex");
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

}
