using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Geometry
{
    [Serializable()]
    public readonly struct PolylineIndex : IComparable<PolylineIndex>, IEquatable<PolylineIndex>, ICloneable, IShapeIndex
    {
        /// <summary>
        /// The index of the polygon 
        /// </summary>
        public readonly int iLine;

        /// <summary>
        /// The index of the vertex
        /// </summary>
        public readonly int iVertex;

        public readonly int NumUnique; //The total number of verticies in the polyline

        public ShapeType2D ShapeType => ShapeType2D.POLYLINE;
        public int iShape => iLine;

        public int? iInnerShape => null;

        int IShapeIndex.NumUnique => NumUnique;

        public bool IsInner => false;

        public IShapeIndex FirstVertexInShape => new PolylineIndex(iLine, 0, NumUnique);

        public IShapeIndex LastVertexInShape => new PolylineIndex(iLine, NumUnique - 1, NumUnique);

        IShapeIndex IShapeIndex.Next => iVertex + 1 < NumUnique ? (IShapeIndex)(new PolylineIndex(iLine, iVertex + 1, NumUnique)) : null;

        IShapeIndex IShapeIndex.Previous => iVertex - 1 >= 0 ? (IShapeIndex)(new PolylineIndex(iLine, iVertex - 1, NumUnique)) : null;
        public int? NextVertex => iVertex + 1 < NumUnique ? (int?)(iVertex + 1) : null;

        public int? PreviousVertex => iVertex - 1 >= 0 ? (int?)(iVertex -1) : null;

        /// <summary>
        /// Return the next index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolylineIndex? Next
        {
            get
            {
                int? n = NextVertex;
                if (n.HasValue)
                    return new PolylineIndex(this.iLine, n.Value, this.NumUnique);
                else
                    return default;
            }
        }

        /// <summary>
        /// Return the previous index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolylineIndex? Previous
        {
            get
            {
                int? p = PreviousVertex;
                if (p.HasValue)
                    return new PolylineIndex(this.iLine, p.Value, this.NumUnique);
                else
                    return default;
            }
        }


        int IShapeIndex.iVertex => iVertex;

        public PolylineIndex(int iV, int lineLength)
        {
            iLine = 0; //Not used in this constructor
            this.iVertex = iV;
            this.NumUnique = lineLength;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolylineIndex(int line, int iV, int lineLength)
        {
            iLine = line;
            this.iVertex = iV;
            this.NumUnique = lineLength;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolylineIndex(int line, int iV, IReadOnlyList<GridPolyline> Lines)
        {
            iLine = line;
            this.iVertex = iV;
            this.NumUnique = Lines[iLine].PointCount;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public object Clone() => new PolylineIndex(this.iLine, this.iVertex, this.NumUnique);

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj is null || GetType() != obj.GetType())
            {
                return false;
            }

            PolylineIndex other = (PolylineIndex)obj;
            return Equals(other);
        }

        public bool Equals(PolylineIndex other)
        {
            if (other.iLine != this.iLine)
            {
                return false;
            }

            if (other.iVertex != this.iVertex)
            {
                return false;
            }

            if (other.NumUnique != this.NumUnique)
                return false;

            return true;
        }

        public bool Equals(IShapeIndex other)
        {
            if (other.ShapeType != this.ShapeType)
                return false;

            if (other.iShape != this.iShape)
            {
                return false;
            }

            if (other.iVertex != this.iVertex)
            {
                return false;
            }

            if (other.NumUnique != this.NumUnique)
                return false;

            return true;
        }

        public int CompareTo(IShapeIndex other)
        {
            if (other.ShapeType != ShapeType2D.POLYGON)
                return other.ShapeType.CompareTo(ShapeType2D.POLYGON);

            if (this.iLine != other.iShape)
                return this.iLine.CompareTo(other.iShape);

            return this.iVertex.CompareTo(other.iVertex);
        }

        public int CompareTo(PolylineIndex other)
        {
            if (this.iLine != other.iLine)
                return this.iLine.CompareTo(other.iLine);

            return this.iVertex.CompareTo(other.iVertex);
        }

        public static bool operator ==(PolylineIndex A, PolylineIndex B)
        {
            if (A.iLine != B.iLine)
            {
                return false;
            }

            if (A.iVertex != B.iVertex)
            {
                return false;
            }

            return A.NumUnique == B.NumUnique;
        }

        public static bool operator !=(PolylineIndex A, IShapeIndex B)
        {
            return !(A == B);
        }

        public static bool operator ==(PolylineIndex A, IShapeIndex B)
        {
            if (A.ShapeType != B.ShapeType)
                return false;

            if (A.iShape != B.iShape)
                return false;

            if (A.iVertex != B.iVertex)
                return false;

            return A.NumUnique == B.NumUnique;
        }

        public static bool operator !=(PolylineIndex A, PolylineIndex B)
        {
            return !(A == B);
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return this.iVertex + (this.iLine << 16);
        }


        /// <summary>
        /// Return true if the index is adjacent to the other index
        /// </summary>
        /// <param name="other"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public bool AreAdjacent(PolylineIndex other)
        {
            if (this.iLine != other.iLine)
                return false;

            if (this.iVertex == other.iVertex)
                return false;

            if (Math.Abs(this.iVertex - other.iVertex) == 1)
            {
                return true;
            }

            return false;
        }

        public bool IsFirstIndex => this.iVertex == 0;

        public bool IsLastIndex => this.iVertex == this.NumUnique - 1;

        /// <summary>
        /// Return the specified point, ignoring the iPoly attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public GridVector2 Point(GridPolyline line)
        {
            return new GridVector2(line.Points[iVertex]);
        }

        public GridVector2 Point(IReadOnlyList<GridPolyline> lines)
        {
            return new GridVector2(lines[iLine].Points[iVertex]);
        }

        /// <summary>
        /// Return a copy of this PointIndex with iPoly value changed to point at a different polygon index
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex Reindex(int iLine)
        {
            return new PolygonIndex(iLine, this.iVertex, this.NumUnique);
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(int numUnique)
        {
            return new PolylineIndex(this.iLine, this.iVertex, numUnique);
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(GridPolyline line)
        {
            return ReindexToSize(line.PointCount);
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(IReadOnlyList<GridPolyline> lines)
        {
            return new PolylineIndex(this.iLine, this.iVertex, lines[iLine].PointCount);
        }

        public override string ToString()
        {
            return $"L:{this.iLine} iVert:{this.iVertex} of {this.NumUnique}";
        }
    }
}
