using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{ 
    /// <summary>
    /// Records the index of a vertex in a polygon
    /// </summary>
    [Serializable()]
    public readonly struct PolygonIndex : IComparable<PolygonIndex>, IEquatable<PolygonIndex>
    {
        /// <summary>
        /// The index of the polygon 
        /// </summary>
        public readonly int iPoly;

        /// <summary>
        /// The index of the inner polygon, or no value if part of the external border
        /// </summary>
        public readonly int? iInnerPoly;

        /// <summary>
        /// The index of the vertex
        /// </summary>
        public readonly int iVertex;

        public readonly int NumUniqueInRing; //The total number of verticies in the ring iVertex indexes into

        /// <summary>
        /// True if the vertex is part of an inner polygon
        /// </summary>
        public bool IsInner => iInnerPoly.HasValue;

        public PolygonIndex(int poly, int iV, int ringLength)
        {
            iPoly = poly;
            iInnerPoly = new int?();
            this.iVertex = iV;
            this.NumUniqueInRing = ringLength;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolygonIndex(int poly, int iV, IReadOnlyList<GridPolygon> Polygons)
        {
            iPoly = poly;
            iInnerPoly = new int?();
            this.iVertex = iV;
            this.NumUniqueInRing = Polygons[poly].ExteriorRing.Length - 1;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing);
        }

        public PolygonIndex(int poly, int? innerPoly, int iV, int ringLength)
        {
            iPoly = poly;
            iInnerPoly = innerPoly;
            this.iVertex = iV;
            this.NumUniqueInRing = ringLength;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing);
        }

        public PolygonIndex(int poly, int? innerPoly, int iV, IReadOnlyList<GridPolygon> Polygons)
        {
            iPoly = poly;
            iInnerPoly = innerPoly;
            this.iVertex = iV;
            this.NumUniqueInRing = 0; //Temp assignment so we can call GetRing
            this.NumUniqueInRing = this.GetRing(Polygons).Length - 1;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing);
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
            if (obj is PolygonIndex other)
                return Equals(other);

            return false;
        }

        public bool Equals(PolygonIndex other)
        {
            if (other.iPoly != this.iPoly)
            {
                return false;
            }

            if (other.iVertex != this.iVertex)
            {
                return false;
            }

            if (other.iInnerPoly != this.iInnerPoly)
            {
                return false;
            }

            if (other.NumUniqueInRing != this.NumUniqueInRing)
                return false;

            return true;
        }

        public static bool operator ==(PolygonIndex A, PolygonIndex B)
        {  
            if (A.iPoly != B.iPoly)
            {
                return false;
            }

            if (A.iVertex != B.iVertex)
            {
                return false;
            }

            if (A.iInnerPoly != B.iInnerPoly)
            {
                return false;
            }

            if (A.NumUniqueInRing != B.NumUniqueInRing)
                return false;

            return true;
        }

        public static bool operator !=(PolygonIndex A, PolygonIndex B)
        {
            return !(A == B);
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            if (IsInner)
            {
                return this.iVertex + (this.iPoly << 16) + (this.iInnerPoly.Value << 10);
            }
            else
            {
                return this.iVertex + (this.iPoly << 16);
            }
        }

        public int CompareTo(PolygonIndex other)
        {
            if (this.iPoly != other.iPoly)
                return this.iPoly.CompareTo(other.iPoly);

            if (this.iInnerPoly != other.iInnerPoly)
            {
                if (this.iInnerPoly.HasValue && other.iInnerPoly.HasValue)
                {
                    return this.iInnerPoly.Value.CompareTo(other.iInnerPoly.Value);
                }

                return this.iInnerPoly.HasValue ? 1 : -1;
            }

            return this.iVertex.CompareTo(other.iVertex);
        }

        public bool IsFirstIndexInRing()
        {
            return this.iVertex == 0 || iVertex == this.NumUniqueInRing; //The latter case should not happen
        }

        public bool IsLastIndexInRing()
        {
            return this.iVertex == this.NumUniqueInRing - 1;
        }

        /// <summary>
        /// Return the specified point, ignoring the iPoly attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public GridVector2 Point(GridPolygon Polygon)
        {
            if (IsInner)
            {
                return Polygon.InteriorPolygons[this.iInnerPoly.Value].ExteriorRing[iVertex];
            }
            else
            {
                return Polygon.ExteriorRing[iVertex];
            }
        }

        public GridVector2 Point(IReadOnlyList<GridPolygon> Polygons)
        {
            if (IsInner)
            {
                return Polygons[iPoly].InteriorPolygons[this.iInnerPoly.Value].ExteriorRing[iVertex];
            }
            else
            {
                return Polygons[iPoly].ExteriorRing[iVertex];
            }
        }

        public GridVector2 Point(IReadOnlyDictionary<int, GridPolygon> Polygons)
        {
            if (IsInner)
            {
                return Polygons[iPoly].InteriorPolygons[this.iInnerPoly.Value].ExteriorRing[iVertex];
            }
            else
            {
                return Polygons[iPoly].ExteriorRing[iVertex];
            }
        }

        /// <summary>
        /// Return the specified point, ignoring the iPoly attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public void SetPoint(GridPolygon Polygon, GridVector2 value)
        {
            Polygon[this] = value;
        }

        public void SetPoint(IReadOnlyList<GridPolygon> Polygons, GridVector2 value)
        {
            Polygons[iPoly][this] = value;
        }

        public void SetPoint(IReadOnlyDictionary<int, GridPolygon> Polygons, GridVector2 value)
        {
            Polygons[iPoly][this] = value;
        }


        /// <summary>
        /// Return the segment, using this point index and the next index in the ring
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public GridLineSegment Segment(GridPolygon Polygon)
        {
            return new GridLineSegment(Point(Polygon), Next.Point(Polygon));
        }

        public GridLineSegment Segment(IReadOnlyList<GridPolygon> Polygons)
        {
            return new GridLineSegment(Point(Polygons), Next.Point(Polygons));
        }

        public GridLineSegment Segment(IReadOnlyDictionary<int, GridPolygon> Polygons)
        {
            return new GridLineSegment(Point(Polygons), Next.Point(Polygons));
        }

        /// <summary>
        /// Returns the polygon the index refers to
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public GridPolygon Polygon(GridPolygon poly)
        {
            return this.IsInner ? poly.InteriorPolygons[iInnerPoly.Value] : poly;
        }

        /// <summary>
        /// Returns the polygon the index refers to
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public GridPolygon Polygon(IReadOnlyList<GridPolygon> polygons)
        {
            GridPolygon poly = polygons[this.iPoly];
            return Polygon(poly);
        }

        /// <summary>
        /// Returns the polygon the index refers to
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public GridPolygon Polygon(IReadOnlyDictionary<int, GridPolygon> polygons)
        {
            GridPolygon poly = polygons[this.iPoly];
            return Polygon(poly);
        }

        /// <summary>
        /// Return true if the index is adjacent to the other index
        /// </summary>
        /// <param name="other"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public bool AreAdjacent(PolygonIndex other)
        {
            if (this.iPoly != other.iPoly)
                return false;

            if (this.iInnerPoly != other.iInnerPoly)
                return false;

            if (this.iVertex == other.iVertex)
                return false;

            if (Math.Abs(this.iVertex - other.iVertex) == 1)
            {
                return true;
            }

            return (other.IsLastIndexInRing() && this.IsFirstIndexInRing()) ||
                   (other.IsFirstIndexInRing() && this.IsLastIndexInRing());
        }

        /// <summary>
        /// Returns the verticies before and after this index
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        private GridVector2[] ConnectedVerticies(GridVector2[] ring)
        {
            int iPrevious = PreviousVertexInRing();
            int iNext = NextVertexInRing();

            //Should I reverse the order for interior polygons?
            return new GridVector2[] { ring[iPrevious], ring[iNext] };
        }

        /// <summary>
        /// Returns the verticies before and after this index
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        public GridVector2[] ConnectedVerticies(IReadOnlyList<GridPolygon> polygons)
        {
            return ConnectedVerticies(GetRing(polygons));
        }

        /// <summary>
        /// Returns the verticies before and after this index
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        public GridVector2[] ConnectedVerticies(IReadOnlyDictionary<int, GridPolygon> polygons)
        {
            return ConnectedVerticies(GetRing(polygons));
        }

        public GridLineSegment[] ConnectedSegments(GridVector2[] ring)
        {
            int iPrevious = PreviousVertexInRing();
            int iNext = PreviousVertexInRing();

            //Should I reverse the order for interior polygons?
            return new GridLineSegment[] {
                new GridLineSegment(ring[iPrevious], ring[iVertex]),
                new GridLineSegment(ring[iVertex], ring[iNext]) };
        }

        public GridLineSegment[] ConnectedSegments(IReadOnlyList<GridPolygon> polygons)
        {
            GridVector2[] ring = GetRing(polygons);
            return ConnectedSegments(ring);
        }

        public GridLineSegment[] ConnectedSegments(IReadOnlyDictionary<int, GridPolygon> polygons)
        {
            GridVector2[] ring = GetRing(polygons);
            return ConnectedSegments(ring);
        }

        /// <summary>
        /// Returns the index of the beginning of the current ring
        /// </summary>
        public PolygonIndex FirstInRing
        {
            get
            {
                return new PolygonIndex(this.iPoly, this.iInnerPoly, 0, this.NumUniqueInRing);
            }
        }

        /// <summary>
        /// Returns the index of the end of the current ring
        /// </summary>
        public PolygonIndex LastInRing
        {
            get
            {
                return new PolygonIndex(this.iPoly, this.iInnerPoly, this.NumUniqueInRing - 1, this.NumUniqueInRing);
            }
        }

        /// <summary>
        /// Return the next index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolygonIndex Next
        {
            get
            {
                return new PolygonIndex(this.iPoly, this.iInnerPoly, this.NextVertexInRing(), this.NumUniqueInRing);
            }
        }

        /// <summary>
        /// Return the previous index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolygonIndex Previous
        {
            get
            {
                return new PolygonIndex(this.iPoly, this.iInnerPoly, this.PreviousVertexInRing(), this.NumUniqueInRing);
            }
        }

        private int NextVertexInRing()
        {
            int iNext = iVertex + 1;
            if (iNext >= this.NumUniqueInRing)
            {
                return 0;
            }

            return iNext;
        }

        private int PreviousVertexInRing()
        {
            int iPrevious = iVertex - 1;
            if (iPrevious < 0)
            {
                return this.NumUniqueInRing - 1;
            }

            return iPrevious;
        }

        internal GridVector2[] GetRing(IReadOnlyList<GridPolygon> Polygons)
        {
            return this.GetRing(Polygons[this.iPoly]);
        }

        internal GridVector2[] GetRing(IReadOnlyDictionary<int, GridPolygon> Polygons)
        {
            return this.GetRing(Polygons[this.iPoly]);
        }

        internal GridVector2[] GetRing(GridPolygon polygon)
        {
            if (this.IsInner)
            {
                return polygon.InteriorPolygons[this.iInnerPoly.Value].ExteriorRing;
            }

            return polygon.ExteriorRing;
        }

        public bool AreOnSameRing(PolygonIndex B)
        {
            if (this.iPoly != B.iPoly)
                return false;

            if (this.IsInner != B.IsInner)
                return false;

            if (this.IsInner && B.IsInner)
            {
                return this.iInnerPoly.Value == B.iInnerPoly.Value;
            }

            return true;
        }

        /// <summary>
        /// Return True if the vertices A and B are a line on the internal or external border of the polygon
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="poly"></param>
        /// <returns></returns>
        public static bool IsBorderLine(PolygonIndex A, PolygonIndex B, GridPolygon poly)
        {
            return A.AreAdjacent(B);

            /*
            //TODO: Add unit test
            System.Diagnostics.Debug.Assert(A.iPoly == B.iPoly, "LineIsOnBorder should only called for indicies into the same polygon");
            if (A.iPoly != B.iPoly)
                throw new ArgumentException("LineIsOnBorder should only called for indicies into the same polygon");

            //Points must be both inside or outside border.
            if (A.IsInner ^ B.IsInner)
            {
                return false;
            }

            if (A.IsInner)
            {
                //Check that the indicies are to the same interior polygon
                if (A.iInnerPoly.Value != B.iInnerPoly.Value)
                {
                    return false;
                }
            }

            //Simple case of adjacent vertex indicies
            int diff = Math.Abs(A.iVertex - B.iVertex);
            if (diff == 1)
                return true;

            //Handle case of the vertex index that wraps around the closed ring 
            // Example: A box with four verticies
            // 0 -- 1 -- 2 -- 3 -- 4 : 0 == 4
            // 3 is adjacent to both 0, 1 and 3, but the diff value to 0 would be 3.

            int RingLength;

            //External Border case
            if (!A.IsInner)
            {
                RingLength = poly.ExteriorRing.Length;
            }
            else
            {
                RingLength = poly.InteriorRings.ElementAt(A.iInnerPoly.Value).Length;
            }

            //Must have points at the wraparound point to be adjacent
            if (A.iVertex > 0 && A.iVertex < RingLength - 2)
                return false;
            if (B.iVertex > 0 && B.iVertex < RingLength - 2)
                return false;

            return diff == RingLength - 2;
            */
        }

        public override string ToString()
        {
            if (IsInner)
                return string.Format("P:{0} I:{1} iVert:{2} of {3}", this.iPoly, this.iInnerPoly, this.iVertex, this.NumUniqueInRing);
            else
                return string.Format("P:{0} iVert:{1} of {2}", this.iPoly, this.iVertex, this.NumUniqueInRing);
        }

        public static PolygonIndex[] SortByRing(PolygonIndex[] verts)
        {
            Array.Sort(verts);
            List<PolygonIndex> listIndex = new List<PolygonIndex>(verts.Length);

            foreach (var poly in verts.GroupBy(v => v.iPoly))
            {
                foreach (var ring in poly.GroupBy(v => v.iInnerPoly))
                {
                    PolygonIndex[] ringArray = ring.ToArray();
                    Array.Sort(ringArray);

                    //If this is not the complete ring make sure our sort is not breaking the ring at the wraparound point
                    if (ringArray.Length < ringArray[0].NumUniqueInRing)
                    {
                        if (ringArray.First().AreAdjacent(ringArray.Last()))
                        {
                            //Walk the items until we find the first index that is not adjacent. 
                            //Then add the indicies after that point.
                            int iStart = 0;
                            for (int iVert = 1; iVert < ringArray.Length; iVert++)
                            {
                                if (!ringArray[iVert].AreAdjacent(ringArray[iVert - 1]))
                                {
                                    iStart = iVert;
                                    break;
                                }
                            }

                            listIndex.AddRange(ringArray.Skip(iStart));
                            listIndex.AddRange(ringArray.Take(iStart));
                        }
                        else
                        {
                            listIndex.AddRange(ringArray);
                        }

                    }
                    else
                    {
                        listIndex.AddRange(ringArray);
                    }
                }
            }

            return listIndex.ToArray();
        }

        /// <summary>
        /// Return a copy of this PointIndex with iPoly value changed to point at a different polygon index
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex Reindex(int iPoly)
        {
            return new PolygonIndex(iPoly, this.iInnerPoly, this.iVertex, this.NumUniqueInRing);
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex ReindexToSize(int numUniqueInRing)
        {
            return new PolygonIndex(this.iPoly, this.iInnerPoly, this.iVertex, numUniqueInRing);
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex ReindexToSize(GridPolygon poly)
        {
            return this.ReindexToSize(this.Polygon(poly).ExteriorRing.Length - 1);
        }

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex ReindexToSize(IReadOnlyList<GridPolygon> Polygons)
        {
            //return this.ReindexToSize(this.Polygon(Polygons).ExteriorRing.Length - 1);
            return new PolygonIndex(this.iPoly, this.iInnerPoly, this.iVertex, this.Polygon(Polygons).ExteriorRing.Length - 1); 
        }

        /// <summary>
        /// Return a copy of this PointIndex that refers to the inner polygon index as an exterior polygon coordinate
        /// </summary>
        /// <param name="iPoly">Passing -1 will use the innerPolygon's index as the new iPoly value.  Useful for referencing into arrays of interior polygons from a parent polygon.</param>
        /// <returns></returns>
        public PolygonIndex ReindexToOuter(int iPoly = 0)
        {
            if (this.IsInner == false)
            {
                throw new ArgumentException("Trying to ReindexToOuter using a non-interior polygon's PointIndex");
            }

            if (iPoly == -1)
            {
                iPoly = this.iInnerPoly.Value;
            }

            return new PolygonIndex(iPoly, this.iVertex, this.NumUniqueInRing);
        }

        /// <summary>
        /// Return a copy of this PointIndex that refers to the inner polygon index as an exterior polygon coordinate
        /// </summary>
        /// <param name="iPoly">Passing -1 will use the innerPolygon's index as the new iPoly value.  Useful for referencing into arrays of interior polygons from a parent polygon.</param>
        /// <returns></returns>
        public PolygonIndex ReindexToInner(int iInner, int iPoly = 0)
        {
            if (this.IsInner == true)
            {
                throw new ArgumentException("Trying to ReindexToInner using an interior polygon's PointIndex");
            }

            return new PolygonIndex(iPoly, iInner, this.iVertex, this.NumUniqueInRing);
        }
    }
}
