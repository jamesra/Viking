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
    public class MeshEdgeAngleComparerFixedIndex : IComparer<IEdgeKey>
    {
        IMesh<IVertex2D> Mesh; 
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

        public MeshEdgeAngleComparerFixedIndex(IMesh2D<IVertex2D> mesh, int origin_vertex, GridLine origin_line_vector, bool clockwise = false) :
            this(mesh, origin_vertex,  origin_line_vector.Direction, clockwise)
        { 
        }

        public MeshEdgeAngleComparerFixedIndex(IMesh2D<IVertex2D> mesh, int origin_vertex, GridVector2 origin_line_vector, bool clockwise = false) 
        {
            Mesh = mesh;
            OriginVertex = origin_vertex;

            GridVector2 Origin = mesh[origin_vertex].Position;
            OriginLine = new GridLine(Origin, origin_line_vector);

            ComparisonPoint = Origin + origin_line_vector;

            ClockwiseOrder = clockwise;
        }

        public int Compare(IEdgeKey A, IEdgeKey B)
        {
            int origin_vertex = A.A == B.A || A.A == B.B ? A.A : A.B; 
            int APoint = A.OppositeEnd(OriginVertex);
            int BPoint = B.OppositeEnd(OriginVertex);
            double angleA = GridVector2.ArcAngle(OriginLine.Origin, Mesh[APoint].Position, ComparisonPoint);
            double angleB = GridVector2.ArcAngle(OriginLine.Origin, Mesh[BPoint].Position, ComparisonPoint);

            //We are measuring the angle from the line in one direction, so don't allow negative angles
            angleA = angleA < 0 ? angleA + (Math.PI * 2.0) : angleA;
            angleB = angleB < 0 ? angleB + (Math.PI * 2.0) : angleB;

            return ClockwiseOrder ? angleA.CompareTo(angleB) : angleB.CompareTo(angleA);
        }
    }


    public class TriangulationVertex<T> : Vertex2D<T>
    {
        public TriangulationVertex(GridVector2 p, T data) : base(p, data)
        {
        }

        public TriangulationVertex(GridVector2 p) : base(p)
        {
        }
    }

    public class TriangulationMesh<VERTEX> : Mesh2D<VERTEX>
        where VERTEX : IVertex2D
    {
        private IComparer<IEdgeKey> edgeComparer;

        public TriangulationMesh()
        {
            edgeComparer = new MeshEdgeAngleComparer<VERTEX>(this, GridVector2.UnitY);
        }
    }

}
