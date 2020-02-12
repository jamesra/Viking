#define TRACEDDELAUNAY

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Geometry.Meshing
{
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

            InfiniteWrappedIndexSet TriangleIndexer = new InfiniteWrappedIndexSet(0, 3, 0);

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

            List<IEdge> IntersectedEdges = FindIntersectingEdges(constrained_edge);
            //Special case: If there is only a single edge we can do an edge flip and be done
            
            if (IntersectedEdges.Count == 0)
            {
                //Debug.Assert(IntersectedEdges.Count != 0, string.Format("Unexpected condition where adding constraint {0} intersected no edges but the constraint was not in the mesh already", constrained_edge)); //This should never be possible unless the edge is already in the triangulated mesh
                throw new ArgumentException(string.Format("Unexpected condition where adding constraint {0} intersected no edges but the constraint was not in the mesh already", constrained_edge));
                this.AddEdge(constrained_edge);
                return;
            }
            else if (IntersectedEdges.Count == 1)
            {
                IEdgeKey intersectedEdge = IntersectedEdges[0];

                var new_faces = TriangleFace.Flip(this[intersectedEdge], constrained_edge);
                 
                this.RemoveEdge(intersectedEdge);
                this.AddEdge(constrained_edge);

                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }

                System.Diagnostics.Debug.Assert(false == this.IsClockwise(new_faces.Item1));
                System.Diagnostics.Debug.Assert(false == this.IsClockwise(new_faces.Item2));

                this.AddFace(new_faces.Item1);
                this.AddFace(new_faces.Item2);

                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }

                return;
            }
            else
            {
                //Triangulate the polygon formed on either side of the constrained line we just added

                //**** Determine which verticies fall to the left and right of the new segment
                List<VERTEX> Left = new List<VERTEX>(); //Verticies left of the line, in order of appearance.  Each adjacent vertex is connected to its neighbors in the list via an edge
                List<VERTEX> Right = new List<VERTEX>();

                Left.Add(this[constrained_edge.A]);
                Right.Add(this[constrained_edge.A]);

                //Remove the edges (which will also remove the faces) from our mesh
                foreach (var edge in IntersectedEdges)
                {
                    //Figure out which endpoint is on which side of the line and populated the left/right list appropriately
                    VERTEX A = this[edge.A];
                    VERTEX B = this[edge.B];

#if TRACEDDELAUNAY
                    Trace.WriteLine(string.Format("Remove edge {0}", edge));
#endif
                    
                    if (ConstrainedEdge.IsLeft(A.Position) > 0)
                    { 
                        if (Left.Last().Index != A.Index)
                        {
                            if (Left.Contains(A) )
                                throw new ArgumentException();

                            Debug.Assert(Left.Contains(A) == false);
                            Left.Add(A);
                        }
                        if (Right.Last().Index != B.Index)
                        {
                            if (Right.Contains(B) )
                                throw new ArgumentException();

                            Debug.Assert(Right.Contains(B) == false);
                            Right.Add(B);
                        }
                    }
                    else //TODO: Handle case of point exactly on the line...
                    {
                        if (Left.Last().Index != B.Index)
                        {
                            if (Left.Contains(B) )
                                throw new ArgumentException();

                            Debug.Assert(Left.Contains(B) == false);
                            Left.Add(B);
                        }
                        if (Right.Last().Index != A.Index)
                        {
                            if (Right.Contains(A) )
                                throw new ArgumentException();

                            Debug.Assert(Right.Contains(A) == false);
                            Right.Add(A);
                        }
                    }
                    
                    this.RemoveEdge(edge);
                    if (ReportProgress != null)
                    {
                        ReportProgress(this);
                    }
                }

                Left.Add(this[constrained_edge.B]);
                Right.Add(this[constrained_edge.B]);

                //Reverse the left side so it is counter-clockwise
                Left.Reverse();

                //Add our constrained edge to the mesh
                this.AddEdge(constrained_edge);

                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }

                TriangulatePolygon(Left, true, ReportProgress);
                TriangulatePolygon(Right, false, ReportProgress);
            }           
        }

        private void TriangulatePolygon(List<VERTEX> PolyVerts, bool LeftPoly, ProgressUpdate ReportProgress = null)
        {
            //For a reason I don't have time to investigate at the moment even though
            //the left and right polygons passed to this function are both in CCW order,
            //the left polygon makes clockwise faces and the right polygon makes CCW faces.
            //Rather than leave a test in the code for each face I'm passing a parameter 
            //indicating which side the polygon originated.  I'm sure the reason for the
            //face ordering will make sense if I thought about it.

#if TRACEDDELAUNAY
            if(this.IsClockwise(PolyVerts.Select(v => v.Index).Distinct()))
            {
                Trace.WriteLine("Clockwise input");
            }
                else
            {
                Trace.WriteLine("Counter-clockwise input");
            }
#endif

            if(PolyVerts.Count == 3)
            {
                int[] verts = PolyVerts.Select(v => v.Index).ToArray();
                TriangleFace f = this.IsClockwise(verts) ? new TriangleFace(verts.Reverse()) : new TriangleFace(verts);

                this.AddFace(f);
                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }
                Debug.Assert(this.IsClockwise(f) == false);
                return;
            }

            PolyVerts.Add(PolyVerts[0]); //Make the vertex list a closed ring
            GridPolygon poly = new GridPolygon(PolyVerts.Select(v => v.Position));

            List<Concavity> concavity = poly.VertexConcavity(out double[] AnglesOutput).ToList();
            List<double> angles = AnglesOutput.ToList();
            
            //Quickly find some simple cases where convex verticies are flanked by two convex verticies. Create a triangle there.
            for (int i = 0; i < poly.TotalUniqueVerticies - 1; i++)
            {
                if (concavity[i] == Concavity.CONCAVE)
                    continue;

                int A = i - 1 < 0 ? poly.TotalUniqueVerticies - 1 : i - 1;
                int C = i + 1 >= poly.TotalUniqueVerticies ? 0 : i + 1;

                if (concavity[A] == Concavity.CONCAVE && concavity[C] == Concavity.CONCAVE)
                {
                    
                    Edge e = new Edge(PolyVerts[A].Index, PolyVerts[C].Index);
                    AddEdge(e);
                    if (ReportProgress != null)
                    {
                        ReportProgress(this);
                    }
                    int[] verts = new int[] { PolyVerts[A].Index, PolyVerts[i].Index, PolyVerts[C].Index };

                    TriangleFace triFace;

                    if(LeftPoly)
                    {
                        triFace = new TriangleFace(verts);
                        //Trace.WriteLine("CW");
                    }
                    else
                    {
                        triFace = new TriangleFace(verts);
                        //Trace.WriteLine("CCW");
                    }

#if TRACEDDELAUNAY
                    Trace.WriteLine(string.Format("Add face {0}", triFace));
#endif

                    AddFace(triFace);
                    if (ReportProgress != null)
                    {
                        ReportProgress(this);
                    }
                    Debug.Assert(this.IsClockwise(triFace) == false);

                    PolyVerts.RemoveAt(i);
                    poly.RemoveVertex(i);

                    /*
                    try
                    {
                        poly.RemoveVertex(i);
                    }
                    catch(ArgumentException)
                    {

                        return;
                    } 
                    */

                    concavity.RemoveAt(i);
                    angles.RemoveAt(i);

                    double angle_out;
                    concavity[A] = poly.IsVertexConcave(A, out angle_out);
                    angles[A] = angle_out;
                    concavity[i] = poly.IsVertexConcave(i, out angle_out);
                    angles[i] = angle_out;

                    i = i - 2;
                    if (i < 0)
                        i = 0; 
                }
            }

#if TRACEDDELAUNAY
            Trace.WriteLine("Concave-Convex-Concave pattern triangulated");
#endif

            //Now score each vertex based on the angle a triangle would make
            List<double> scores = new List<double>(poly.TotalUniqueVerticies);
            double maxScore = double.MinValue;
            double minScore = double.MaxValue;
            for (int i = 0; i < poly.TotalUniqueVerticies; i++)
            {
                //Figure out the minimum angle of a triangle formed here.
                //We know convex verts will not have scores, so skip them
                if (concavity[i] == Concavity.CONCAVE)
                    scores.Add(double.NaN);
                else
                {
                    scores.Add(ScoreTriangle(poly, i));
                    maxScore = scores[i] > maxScore ? scores[i] : maxScore;
                    minScore = scores[i] < minScore ? scores[i] : minScore;
                }
            }
             
            while (poly.TotalUniqueVerticies > 3)
            {/*
                if (concavity.All(c => c == Concavity.CONVEX || c == Concavity.PARALLEL))
                {
                    //OK, we can triangulate the rest with a fan. 
                    TriangulateConcavePolygon(PolyVerts);
                }
                */

                int iBest = scores.IndexOf(minScore);
                int A = iBest - 1 < 0 ? poly.TotalUniqueVerticies - 1 : iBest - 1;
                int C = iBest + 1 >= poly.TotalUniqueVerticies ? 0 : iBest + 1;

                Edge e = new Edge(PolyVerts[A].Index, PolyVerts[C].Index);
                AddEdge(e);
                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }
                int[] verts = new int[] { e.A, PolyVerts[iBest].Index, e.B };
                TriangleFace triFace = null;

                if (this.IsClockwise(verts)) //LeftPoly)
                {
                    //Trace.WriteLine("CW");
                    triFace = new TriangleFace(verts.Reverse());
                }
                else
                {
                    //Trace.WriteLine("CCW");
                    triFace = new TriangleFace(verts);
                }


#if TRACEDDELAUNAY
                Trace.WriteLine(string.Format("Add face {0} with score {1}", triFace, scores[iBest]));
#endif

                AddFace(triFace);
                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }
                // Debug.Assert(this.IsClockwise(triFace) == false);
                //poly.RemoveVertex(iBest);

                /*
                try
                {
                    AddFace(triFace);
                }
                catch(InvalidOperationException)
                {
                    return;
                }
                */
                try
                {
                    poly.RemoveVertex(iBest);
                }
                catch (ArgumentException)
                {
                    Trace.WriteLine(string.Format("Exception removing vertex {0}", PolyVerts[iBest].Index));
                    return;
                } 

                scores.RemoveAt(iBest);


                //Since we have a loop of values with the first equal to the last, handle removing the duplicate if needed
                if (iBest == 0 && PolyVerts[0].Equals(PolyVerts.Last()))
                {
                    int iEnd = PolyVerts.Count - 1;
                    PolyVerts.RemoveAt(iEnd);
                    concavity.RemoveAt(iEnd);
                    angles.RemoveAt(iEnd);
                }

                PolyVerts.RemoveAt(iBest);
                concavity.RemoveAt(iBest);
                angles.RemoveAt(iBest);


                if (poly.TotalUniqueVerticies > 3)
                { 
                    if (iBest >= poly.TotalUniqueVerticies)
                        iBest = 0; 

                    scores[iBest] = ScoreTriangle(poly, iBest);

                    if(iBest == 0)
                    {
                        A = poly.TotalUniqueVerticies - 1;
                    }

                    scores[A] = ScoreTriangle(poly, A);
                     
                    maxScore = scores.Where(s => !double.IsNaN(s)).Max();
                    minScore = scores.Where(s => !double.IsNaN(s)).Min();
                } 
            }

            if (PolyVerts.Distinct().Count() == 3)
            {
                int[] verts = PolyVerts.Distinct().Select(v => v.Index).ToArray();
                TriangleFace f = this.IsClockwise(verts) ? new TriangleFace(verts.Reverse()) : new TriangleFace(verts);

                /*Adding edges for the faces was a workaround and probably covers up a bug*/

                foreach(IEdgeKey e in f.Edges)
                {
                    if(this.Contains(e) == false)
                    {
                        Debug.Assert(this.Contains(e), string.Format("Unexpected edge {0} found in face {1}", e, f));

                        throw new ArgumentException(string.Format("Unexpected edge {0} found in face {1}", e, f));
                        this.AddEdge(new Edge(e.A, e.B));
                        if (ReportProgress != null)
                        {
                            ReportProgress(this);
                        }
                    }
                }

                this.AddFace(f);
                if (ReportProgress != null)
                {
                    ReportProgress(this);
                }
                Debug.Assert(this.IsClockwise(f)==false);
                return;
            }
#if TRACEDDELAUNAY
            Trace.WriteLine("Done with triangulating polygon after adding constraint!");
#endif
        }

        private void TriangulateConcavePolygon(List<VERTEX> PolyVerts)
        {
            for(int i = 1; i < PolyVerts.Count-1; i++)
            { 
                int A = i - 1 < 0 ? PolyVerts.Count - 2 : i - 1;
                int C = i + 1 >= PolyVerts.Count ? 1 : i + 1;

                Edge e = new Edge(PolyVerts[A].Index, PolyVerts[C].Index);
                AddEdge(e);
                int[] verts = new int[] { e.A, PolyVerts[i].Index, e.B };
                TriangleFace triFace = null;

                if (this.IsClockwise(verts)) //LeftPoly)
                {
                    Trace.WriteLine("CW");
                    triFace = new TriangleFace(verts.Reverse());
                }
                else
                {
                    Trace.WriteLine("CCW");
                    triFace = new TriangleFace(verts);
                }

                Trace.WriteLine(string.Format("Add face {0}", triFace));

                AddFace(triFace);
                Debug.Assert(this.IsClockwise(triFace) == false);
            }
        }

        private double ScoreTriangle(GridTriangle tri)
        {
            if (tri.Angles.Any(a => a == 0))
            {
                return double.NaN;
            }

            //return tri.Angles.Min();
            return tri.Segments.Sum(s => s.Length);
        }

        private double ScoreTriangle(GridPolygon poly, int iVertex)
        {
            int A = iVertex - 1 < 0 ? poly.TotalUniqueVerticies - 1 : iVertex - 1;
            int C = iVertex + 1 >= poly.TotalUniqueVerticies ? 0 : iVertex + 1;

            GridLineSegment seg = new GridLineSegment(poly.ExteriorRing[A], poly.ExteriorRing[C]);
            if(poly.Contains(seg.PointAlongLine(0.5)) == false)
            {
                return double.NaN;
            }

            //Figure out the minimum angle of a triangle formed here.
            GridTriangle tri = new GridTriangle(poly.ExteriorRing[A],
                                                poly.ExteriorRing[iVertex],
                                                poly.ExteriorRing[C]);

            return ScoreTriangle(tri);
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
                            FindIntersectingEdges(testFace, constrained_edge, constrained_seg, candidate, ref intersected_edges);
                            throw new NotImplementedException("Constrained edge passes directly through vertex");
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
