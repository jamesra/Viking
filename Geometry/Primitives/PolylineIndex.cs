using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Geometry
{
    [Serializable()]
    public readonly struct PolylineIndex : IComparable<PolylineIndex>, IEquatable<PolylineIndex>
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

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (object.ReferenceEquals(obj, null) || GetType() != obj.GetType())
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
             
            if (A.NumUnique != B.NumUnique)
                return false;

            return true;
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

        public int CompareTo(PolylineIndex other)
        {
            if (this.iLine != other.iLine)
                return this.iLine.CompareTo(other.iLine);
              
            return this.iVertex.CompareTo(other.iVertex);
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

        public bool IsFirstIndex
        {
            get
            {
                return this.iVertex == 0;
            }
        }

        public bool IsLastIndex 
        {
            get
            {
                return this.iVertex == this.NumUnique - 1;
            }
        }

        public int? NextVertex
        {
            get
            {
                int iNext = iVertex + 1;
                if (iNext >= this.NumUnique)
                {
                    return new int?();
                }

                return iNext;
            }
        }

        public int? PreviousVertex
        {
            get
            {
                int iPrevious = iVertex - 1;
                if (iPrevious < 0)
                {
                    return new int?();
                }

                return iPrevious;
            }
        }

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
                    return new PolylineIndex(this.iLine, this.NextVertex.Value, this.NumUnique);
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
                    return new PolylineIndex(this.iLine, this.PreviousVertex.Value, this.NumUnique);
                else
                    return default;
            }
        }

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
            return string.Format("L:{0} iVert:{1} of {2}", this.iLine, this.iVertex, this.NumUnique);
        }

    }
}
