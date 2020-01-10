using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            Vertex2D<T> newVertex = new Vertex2D<T>(Position, Data, this.EdgeComparer);
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

    public class TriangulationMesh<VERTEX> : Mesh2D<VERTEX>
        where VERTEX : IVertex2D, IVertexSortEdgeByAngle
    {
        internal IComparer<IEdgeKey> edgeAngleComparer;
         
        internal SortedSet<long> _XSorted;
        internal SortedSet<long> _YSorted;

        private long[] _XSortedArrayCache = null;
        public long[] XSorted
        {
            get
            {
                if(_XSortedArrayCache == null)
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

        public TriangulationMesh()
        {
            edgeAngleComparer = new MeshEdgeAngleComparer<VERTEX>(this, GridVector2.UnitY);
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
            return iNew;
        } 

        public void EdgeToVertAngle(IEdge e, IVertex2D p)
        {
            GridLine line = ToGridLine(e);
        }
    }

}
