using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Geometry
{
    [Serializable()]
    public struct PolylineIndex : IComparable<PolylineIndex>, IEquatable<PolylineIndex>
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

        public readonly bool Closed; 

        private static int CalculateNumUniqueVerticies(GridPolyline line)
        {
            return CalculateNumUniqueVerticies(line.Points.Count, line.Closed);
        }

        private static int CalculateNumUniqueVerticies(int lineLength, bool closed)
        {
            return lineLength - (closed ? 1 : 0);
        }

        public PolylineIndex(int iV, int lineLength, bool closed = false)
        {
            iLine = 0; //Not used in this constructor
            this.iVertex = iV;
            this.NumUnique = CalculateNumUniqueVerticies(lineLength, closed);
            Closed = closed;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolylineIndex(int line, int iV, int lineLength, bool closed = false)
        {
            iLine = line; 
            this.iVertex = iV;
            this.NumUnique = CalculateNumUniqueVerticies(lineLength, closed);
            Closed = closed;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolylineIndex(int line, int iV, IReadOnlyList<GridPolyline> Lines)
        {
            iLine = line;
            this.iVertex = iV;
            this.NumUnique = CalculateNumUniqueVerticies(Lines[iLine]);
            Closed = Lines[iLine].Closed;
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
            bool ANull = object.ReferenceEquals(A, null);
            bool BNull = object.ReferenceEquals(B, null);

            if (ANull && BNull)
                return true;
            else if (ANull ^ BNull)
                return false;

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

        public bool IsFirstIndex()
        {
            return this.iVertex == 0 || (Closed && iVertex == this.NumUnique);
        }

        public bool IsLastIndex()
        {
            return this.iVertex == this.NumUnique - 1;
        }

        public int? NextVertex
        {
            get
            {
                int iNext = iVertex + 1;
                if (iNext >= this.NumUniqueInRing)
                {
                    return this.Closed ? 0 : new int?();
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
                    return this.Closed ? this.NumUnique - 1 : new int?();
                }

                return iPrevious;
            }
        }

        /// <summary>
        /// Return the next index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolylineIndex Next
        {
            get
            {
                return new PolylineIndex(this.iPoly, this.NextVertex, this.NumUniqueInRing);
            }
        }

        /// <summary>
        /// Return the previous index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolylineIndex Previous
        {
            get
            {
                return new PolylineIndex(this.iPoly, this.PreviousVertex, this.NumUniqueInRing);
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
        /// Return the segment, using this point index and the next index in the ring
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public GridLineSegment Segment(GridPolyline polyline)
        {
            return new GridLineSegment(polyline[this],polyline[this.Next]);
        }

        public GridLineSegment Segment(IReadOnlyList<GridPolyline> polylines)
        {
            return new GridLineSegment(Point(Polygons), Next.Point(Polygons));
        }

        public GridLineSegment Segment(IReadOnlyDictionary<int, GridPolyline> polylines)
        {
            return new GridLineSegment(Point(Polygons), Next.Point(Polygons));
        }

        /// <summary>
        /// Return a copy of this PointIndex with iPoly value changed to point at a different polygon index
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex Reindex(int iLine)
        {
            return new PolygonIndex(iLine, this.iVertex, this.NumUniqueInRing);
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
            return ReindexToSize(CalculateNumUniqueVerticies(line));
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(IReadOnlyList<GridPolyline> lines)
        {
            return new PolylineIndex(this.iLine, this.iVertex, CalculateNumUniqueVerticies(lines[iLine]));
        }

        public override string ToString()
        { 
            return string.Format("L:{0} iVert:{1} of {2}", this.iLine, this.iVertex, this.NumUnique);
        }

    }
}
