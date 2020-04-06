using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Collections;
using System.Diagnostics;

namespace Geometry
{
    public enum Concavity
    {
        CONCAVE = -1,
        PARALLEL = 0,
        CONVEX = 1
    }
    /// <summary>
    /// Records the index of a vertex in a polygon
    /// </summary>
    [Serializable()]
    public struct PointIndex : IComparable<PointIndex>, IEquatable<PointIndex>
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

        private readonly int NumUniqueInRing; //The total number of verticies in the ring iVertex indexes into

        /// <summary>
        /// True if the vertex is part of an inner polygon
        /// </summary>
        public bool IsInner
        {
            get
            {
                return iInnerPoly.HasValue;
            }
        }

        public PointIndex(int poly, int iV, int ringLength)
        {
            iPoly = poly;
            iInnerPoly = new int?();
            this.iVertex = iV;
            this.NumUniqueInRing = ringLength;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PointIndex(int poly, int iV, IReadOnlyList<GridPolygon> Polygons)
        {
            iPoly = poly;
            iInnerPoly = new int?();
            this.iVertex = iV;
            this.NumUniqueInRing = Polygons[poly].ExteriorRing.Length - 1;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing);
        }

        public PointIndex(int poly, int? innerPoly, int iV, int ringLength)
        {
            iPoly = poly;
            iInnerPoly = innerPoly;
            this.iVertex = iV;
            this.NumUniqueInRing = ringLength;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(iVertex <= NumUniqueInRing);
        }

        public PointIndex(int poly, int? innerPoly, int iV, IReadOnlyList<GridPolygon> Polygons)
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

            if (object.ReferenceEquals(obj, null) || GetType() != obj.GetType())
            {
                return false;
            }

            PointIndex other = (PointIndex)obj;
            return Equals(other);
        }

        public bool Equals(PointIndex other)
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

        public static bool operator ==(PointIndex A, PointIndex B)
        {
            bool ANull = object.ReferenceEquals(A, null);
            bool BNull = object.ReferenceEquals(B, null);

            if (ANull && BNull)
                return true;
            else if (ANull ^ BNull)
                return false;

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

        public static bool operator !=(PointIndex A, PointIndex B)
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

        public int CompareTo(PointIndex other)
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
        internal GridPolygon Polygon(GridPolygon poly)
        {
            if (this.IsInner)
            {
                return poly.InteriorPolygons[this.iInnerPoly.Value];
            }
            else
            {
                return poly;
            }

            return Polygon(new GridPolygon[] { poly });
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
        public bool AreAdjacent(PointIndex other)
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

            return (other.IsLastIndexInRing() && this.iVertex == 0) ||
                   (other.iVertex == 0 && this.IsLastIndexInRing());
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
        /// Return the next index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PointIndex Next
        {
            get
            {
                return new PointIndex(this.iPoly, this.iInnerPoly, this.NextVertexInRing(), this.NumUniqueInRing);
            }
        }

        /// <summary>
        /// Return the previous index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PointIndex Previous
        {
            get
            {
                return new PointIndex(this.iPoly, this.iInnerPoly, this.PreviousVertexInRing(), this.NumUniqueInRing);
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

        public bool AreOnSameRing(PointIndex B)
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
        public static bool IsBorderLine(PointIndex A, PointIndex B, GridPolygon poly)
        {
            //TODO: Add unit test
            System.Diagnostics.Debug.Assert(A.iPoly == B.iPoly, "LineIsOnBorder should only called for indicies into the same polygon");

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
        }

        public override string ToString()
        {
            if (IsInner)
                return string.Format("P:{0} I:{1} iVert:{2}", this.iPoly, this.iInnerPoly, this.iVertex);
            else
                return string.Format("P:{0} iVert:{1}", this.iPoly, this.iVertex);
        }

        public static PointIndex[] SortByRing(PointIndex[] verts)
        {
            Array.Sort(verts);
            List<PointIndex> listIndex = new List<PointIndex>(verts.Length);

            foreach (var poly in verts.GroupBy(v => v.iPoly))
            {
                foreach (var ring in poly.GroupBy(v => v.iInnerPoly))
                {
                    PointIndex[] ringArray = ring.ToArray();
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
        public PointIndex Reindex(int iPoly)
        {
            return new PointIndex(iPoly, this.iInnerPoly, this.iVertex, this.NumUniqueInRing);
        }

        /// <summary>
        /// Return a copy of this PointIndex that refers to the inner polygon index as an exterior polygon coordinate
        /// </summary>
        /// <param name="iPoly">Passing -1 will use the innerPolygon's index as the new iPoly value.  Useful for referencing into arrays of interior polygons from a parent polygon.</param>
        /// <returns></returns>
        public PointIndex ReindexToOuter(int iPoly=0)
        {
            if(this.IsInner == false)
            {
                throw new ArgumentException("Trying to ReindexToOuter using a non-interior polygon's PointIndex");
            }

            if(iPoly == -1)
            {
                iPoly = this.iInnerPoly.Value;
            }

            return new PointIndex(iPoly, this.iVertex, this.NumUniqueInRing);
        }

        /// <summary>
        /// Return a copy of this PointIndex that refers to the inner polygon index as an exterior polygon coordinate
        /// </summary>
        /// <param name="iPoly">Passing -1 will use the innerPolygon's index as the new iPoly value.  Useful for referencing into arrays of interior polygons from a parent polygon.</param>
        /// <returns></returns>
        public PointIndex ReindexToInner(int iInner, int iPoly=0)
        {
            if (this.IsInner == true)
            {
                throw new ArgumentException("Trying to ReindexToInner using an interior polygon's PointIndex");
            }

            return new PointIndex(iPoly, iInner, this.iVertex, this.NumUniqueInRing);
        }
    }

    public class PolygonVertexEnum : IEnumerator<PointIndex>, IEnumerator, IEnumerable<PointIndex>, IEnumerable
    {
        PointIndex? curIndex; 
        readonly GridPolygon polygon;

        /// <summary>
        /// If set indicies returned by this enumerator will use this value for the iPoly field of the polygon index
        /// </summary>
        public int? PolyIndex = new int?();

        public PolygonVertexEnum(GridPolygon poly)
        {
            this.polygon = poly;
            curIndex = new Geometry.PointIndex?();
        }

        public PolygonVertexEnum(GridPolygon poly, int ForceiPoly)
        {
            this.polygon = poly;
            curIndex = new Geometry.PointIndex?();
            PolyIndex = ForceiPoly;
        }

        public PointIndex Current
        {
            get
            {
                if (!curIndex.HasValue)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (!curIndex.HasValue)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        public void Dispose() { }

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if (!curIndex.HasValue)
            {
                if (polygon == null)
                    return false;

                if (polygon.ExteriorRing.Length == 0)
                    return false;

                curIndex = new PointIndex(PolyIndex.HasValue ? PolyIndex.Value : 0, 0, polygon.ExteriorRing.Length - 1);
                return true;
            }

            PointIndex? next = NextIndex(polygon, curIndex.Value);

            curIndex = next;
            return curIndex.HasValue;
        }

        private PointIndex? NextIndex(GridPolygon poly, PointIndex current)
        {
            int iNextVert = current.iVertex + 1;
            GridVector2[] ring = current.GetRing(poly);

            if (iNextVert < ring.Length - 1) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating
                return new PointIndex(current.iPoly, current.iInnerPoly, iNextVert, ring.Length - 1);
            }

            if (iNextVert == ring.Length - 1) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating and hit the last vertex in a closed loop that equals the first vertex
                if (ring[0] != ring[iNextVert])
                    return new PointIndex(current.iPoly, current.iInnerPoly, iNextVert, ring.Length - 1);
            }

            //OK, handle case where we are out of indicies on the current ring            
            {
                //Find the next ring
                if (current.IsInner)
                {
                    if (current.iInnerPoly.Value + 1 < poly.InteriorRings.Count)
                    {
                        int iNextInner = current.iInnerPoly.Value + 1;
                        //Move to the next inner polygon
                        return new PointIndex(current.iPoly, iNextInner, 0, poly.InteriorRings.ElementAt(iNextInner).Length - 1);
                    }
                    else
                    {
                        //No more polygons, move to the next polygon, handled below
                    }
                }
                else
                {
                    if (poly.HasInteriorRings)
                    {
                        return new PointIndex(current.iPoly, 0, 0, poly.InteriorRings.First().Length - 1); //Go to the first vertex of the first inner polygon
                    }
                }

                //OK, we need to move on and could not move to an inner ring.  Normally this is where we go to the next polygon but since this enumerator only covers a single polygon we are done.
                return new PointIndex?();
            }
        }

        public void Reset()
        {
            curIndex = new PointIndex?();
        }

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator<PointIndex>)this;
        }

        IEnumerator<PointIndex> IEnumerable<PointIndex>.GetEnumerator()
        {
            return (IEnumerator<PointIndex>)this;
        }
    }



    /// <summary>
    /// Enumerate all verticies for a collection of polygons
    /// </summary>
    public class PolySetVertexEnum : IEnumerator<PointIndex>, IEnumerator, IEnumerable<PointIndex>, IEnumerable
    {
        PointIndex? curIndex;

        IReadOnlyList<GridPolygon> polygons;

        /// <summary>
        /// The index to use for the first polygon in the list, defaults to zero
        /// </summary>
        private int StartingPolyIndex;
         

        public PolySetVertexEnum(IReadOnlyList<GridPolygon> polys, int iStartingPolyIndex = 0)
        {
            this.polygons = polys;
            curIndex = new Geometry.PointIndex?();
            StartingPolyIndex = iStartingPolyIndex;
        }

        public PointIndex Current
        {
            get
            {
                if(!curIndex.HasValue)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        object IEnumerator.Current
        {
            get
            {
                if (!curIndex.HasValue)
                {
                    throw new IndexOutOfRangeException("Current Index is undefined");
                }

                return curIndex.Value;
            }
        }

        public void Dispose() { }

        /// <summary>
        /// Go to the next index, if the shape is closed we do not return the closed index twice. 
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            if(!curIndex.HasValue)
            {
                if (polygons.Count == 0)
                    return false;

                if (polygons[0].ExteriorRing.Length == 0)
                    return false;

                curIndex = new PointIndex(StartingPolyIndex, 0, polygons[0].ExteriorRing.Length-1);
                return true;
            }

            PointIndex? next = NextIndex(polygons, curIndex.Value);

            curIndex = next;
            return curIndex.HasValue;
        }
         
        private PointIndex? NextIndex(IReadOnlyList<GridPolygon> polygons, PointIndex current)
        {
            int iPoly = current.iPoly - StartingPolyIndex;
            GridPolygon poly = polygons[iPoly];

            int iNextVert = current.iVertex + 1;
            GridVector2[] ring = current.GetRing(poly);

            if(iNextVert < ring.Length-1) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating
                return new PointIndex(current.iPoly, current.iInnerPoly, iNextVert, ring.Length-1);
            }
            
            if(iNextVert == ring.Length - 1) //-1 because we do not want to report a duplicate vertex for a closed ring
            {
                //Move along the ring we are iterating and hit the last vertex in a closed loop that equals the first vertex
                if (ring[0] != ring[iNextVert])
                    return new PointIndex(current.iPoly, current.iInnerPoly, iNextVert, ring.Length-1);
            }
            
            //OK, handle case where we are out of indicies on the current ring            
            { 
                //Find the next ring
                if(current.IsInner)
                {   
                    if(current.iInnerPoly.Value + 1 < poly.InteriorRings.Count)
                    {
                        int iNextInner = current.iInnerPoly.Value + 1; 
                        //Move to the next inner polygon
                        return new PointIndex(current.iPoly, iNextInner, 0, poly.InteriorRings.ElementAt(iNextInner).Length - 1);
                    }
                    else
                    {
                        //No more polygons, move to the next polygon, handled below
                    }
                }
                else
                {
                    if(poly.HasInteriorRings)
                    {
                        return new PointIndex(current.iPoly, 0, 0, poly.InteriorRings.First().Length - 1); //Go to the first vertex of the first inner polygon
                    }
                }

                //OK, we need to move on and could not move to an inner ring.  Go to the next polygon

                int iNextPoly = iPoly + 1;
                if (iNextPoly >= polygons.Count)
                {
                    return new PointIndex?();
                }
                else
                {
                    return new Geometry.PointIndex(current.iPoly + 1, 0, polygons[iNextPoly].ExteriorRing.Length-1);
                }
            }
        }        

        public void Reset()
        {
            curIndex = new PointIndex?();
        }

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator<PointIndex>)this;
        }

        IEnumerator<PointIndex> IEnumerable<PointIndex>.GetEnumerator()
        {
            return (IEnumerator<PointIndex>)this;
        }
    }

    /// <summary>
    /// This is a helper class.  Originally used as values to insert into RTree's to speed up GridPolygon class
    /// </summary>
    internal struct PolygonSegment
    {
        public PointIndex Index { get; private set; }
        
    }

    /// <summary>
    /// A polygon with interior rings representing holes
    /// Rings are described by points.  The first and last point should match
    /// Uses Counter-Clockwise winding order
    /// </summary>
    [Serializable()]
    public class GridPolygon : ICloneable, IPolygon2D
    {
        double _ExteriorRingArea;
        GridVector2[] _ExteriorRing;
         
        public GridVector2[] ExteriorRing
        {
            get { return _ExteriorRing; }
            set
            {
                _ExteriorRingArea = value.PolygonArea();
                if (_ExteriorRingArea < 0) //Negative area indicates Clockwise orientation, we use counter-clockwise
                {
                    _ExteriorRingArea = -_ExteriorRingArea;
                    _ExteriorRing = value.Reverse().ToArray();
                }
                else
                {
                    _ExteriorRing = value;
                }

                _Centroid = null;
                _BoundingRect = _ExteriorRing.BoundingBox();
                _ExteriorSegments = CreateLineSegments(_ExteriorRing);
                //                _ExteriorSegmentRTree = null;
                _SegmentRTree = null;
            }
        }

        GridRectangle _BoundingRect; 
        GridLineSegment[] _ExteriorSegments;

        [NonSerialized]
        RTree.RTree<PointIndex> _SegmentRTree = null;

        internal RTree.RTree<PointIndex> SegmentRTree
        {
            get
            {
                if (_SegmentRTree == null)
                {
                    _SegmentRTree = CreatePointIndexSegmentBoundingBoxRTree(this);
                }

                return _SegmentRTree;
            }
        }

        /// <summary>
        /// Read only please
        /// </summary>
        public GridLineSegment[] ExteriorSegments
        {
            get
            {
                return _ExteriorSegments;
            }
        }

        /// <summary>
        /// Test if a line segment is one of the polygons exterior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorSegment(GridLineSegment segment)
        {
            if(_ExteriorSegments.Length < 20)
            {
                return _ExteriorSegments.Contains(segment);
            }
            else
            {
                //return ExteriorSegmentRTree.Intersects(segment.BoundingBox.ToRTreeRect(0)).Contains(segment);  //No need to check in further detail because they should be identical GridLineSegments
                return SegmentRTree.Intersects(segment.BoundingBox.ToRTreeRect(0)).Where(i => i.IsInner == false).Select(p => p.Segment(this)).Contains(segment);  //No need to check in further detail because they should be identical GridLineSegments
            }
        }

        /// <summary>
        /// Test if a line segment is one of the polygons exterior or interior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorOrInteriorSegment(GridLineSegment segment)
        {
            return SegmentRTree.Intersects(segment.BoundingBox.ToRTreeRect(0)).Where(p => p.Segment(this) == segment).Any();  //No need to check in further detail because they should be identical GridLineSegments
        }

        GridVector2? _Centroid;
        public GridVector2 Centroid
        {
            get
            {
                if(!_Centroid.HasValue)
                {
                    _Centroid = CalculateCentroid(ExteriorRing);
                }

                return _Centroid.Value;
            }
        }

        List<GridPolygon> _InteriorPolygons = new List<GridPolygon>();

        /// <summary>
        /// Read only please
        /// </summary>
        public IReadOnlyList<GridPolygon> InteriorPolygons
        {
            get
            {
                return _InteriorPolygons.AsReadOnly();
            }
        }

        /// <summary>
        /// Read only please
        /// </summary>
        public IList<GridVector2[]> InteriorRings
        {
            get
            {
                return _InteriorPolygons.Select(p => p._ExteriorRing).ToList();
            }
        }

        /// <summary>
        /// Return a list of all exterior and interior line segments
        /// </summary>
        public List<GridLineSegment> AllSegments
        {
            get
            {
                List<GridLineSegment> listLines = this.ExteriorSegments.ToList();

                listLines.AddRange(this.InteriorPolygons.SelectMany(inner => inner.AllSegments));

                return listLines;
            }
        }

        public bool HasInteriorRings
        {
            get
            {
                return _InteriorPolygons.Count > 0;
            }
        }

        public GridPolygon(IEnumerable<IPoint2D> exteriorRing) : this (exteriorRing.Select(p => p.Convert()).ToArray())
        {}

        public GridPolygon(IEnumerable<GridVector2> exteriorRing) : this(exteriorRing.ToArray())
        {}

        public GridPolygon(GridVector2[] exteriorRing)
        {
            if(!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            //The only duplicate point should be the first and the last.  If not throw an exception
            var nonDuplicatedPoints = exteriorRing.RemoveDuplicates();
            if(nonDuplicatedPoints.Length != exteriorRing.Length -1)
            {                 
                throw new ArgumentException("Duplicate point found in exterior ring");
            }

            if(exteriorRing.AreClockwise())
            {
                exteriorRing = exteriorRing.Reverse().ToArray();
            }

            ExteriorRing = exteriorRing;
        }


        public GridPolygon(IEnumerable<IPoint2D> exteriorRing, IEnumerable<IPoint2D[]> interiorRings) 
            : this(exteriorRing.Select(p => p.Convert()).ToArray(), 
                   interiorRings.Select(inner_ring => inner_ring.Select(p => p.Convert() ).ToArray()).ToArray())
        { 
        }

        public GridPolygon(IEnumerable<GridVector2> exteriorRing, IEnumerable<ICollection<GridVector2>> interiorRings)
        {
            ExteriorRing = exteriorRing.ToArray();

            if (!ExteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }
              
            foreach(ICollection<GridVector2> interiorRing in interiorRings)
            {
                AddInteriorRing(interiorRing);
            }
        }

        public GridPolygon(GridVector2[] exteriorRing, IEnumerable<GridVector2[]> interiorRings)
        {
            if (!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            ExteriorRing = exteriorRing;

            foreach (GridVector2[] interiorRing in interiorRings)
            {
                AddInteriorRing(interiorRing);
            }
        }

        public double Area
        {
            get
            {
                double area = _ExteriorRingArea;
                double inner_area = _InteriorPolygons.Sum(ip => ip.Area);
                area -= inner_area;
                return area;
            }
        }

        public double Perimeter
        {
            get
            {
                return ExteriorRing.PerimeterLength();
            }
        }

        public GridRectangle BoundingBox
        {
            get
            {
                return _BoundingRect;
            }
        }

        public ShapeType2D ShapeType
        {
            get
            {
                return ShapeType2D.POLYGON;
            }
        }

        ICollection<IPoint2D> IPolygon2D.ExteriorRing
        {
            get
            {
                return this.ExteriorRing.Select(p => p as IPoint2D).ToArray();
            }
        }

        ICollection<IPoint2D[]> IPolygon2D.InteriorRings
        {
            get
            {
                return this.InteriorRings.Select(ir => ir.Select(p => p as IPoint2D).ToArray()).ToArray(); 
            }
        }

        /// <summary>
        /// All unique verticies.  This is calculated for every use
        /// </summary>
        public GridVector2[] AllVerticies
        {
            get
            {
                return ExteriorRing.Union(InteriorRings.SelectMany(i => i)).Distinct().ToArray();
            }
        }

        /// <summary>
        /// Total verticies, including the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalVerticies
        {
            get
            { 
                return ExteriorRing.Length + InteriorRings.Sum(ir => ir.Length);
            }
        }

        /// <summary>
        /// Total verticies, minus the duplicate verticies at the end of each ring
        /// </summary>
        public int TotalUniqueVerticies
        {
            get
            {
                return (ExteriorRing.Length  - 1) + InteriorRings.Sum(ir => ir.Length - 1);
            }
        }

        IPoint2D IPolygon2D.Centroid
        {
            get
            {
                return this.Centroid;
            }
        }

        public void AddInteriorRing(IEnumerable<GridVector2> interiorRing)
        {
            GridPolygon innerPoly = new Geometry.GridPolygon(interiorRing);

            //TODO: Make sure the inner poly does not  intersect the outer ring or any existing inner ring
            AddInteriorRing(innerPoly);
        }

        public void AddInteriorRing(GridPolygon innerPoly)
        {
            //TODO: Make sure the inner poly does not intersect the outer ring or any existing inner ring

            if (this._InteriorPolygons.Any(p => p.Intersects(innerPoly)))
                throw new ArgumentException("Cannot add interior polygon that intersects and existing interior polygon");

            if (this.ExteriorSegments.Any(line => line.Intersects(innerPoly)))
                throw new ArgumentException("Cannot add interior polygon that intersects a polygon's exterior boundary");

            int iInner = _InteriorPolygons.Count;
            this._InteriorPolygons.Add(innerPoly);

            //Add the new inner polygon to our RTree if it is built
            if (this._SegmentRTree != null)
            {
                PolygonVertexEnum enumerator = new PolygonVertexEnum(innerPoly);

                IEnumerable<PointIndex> newIndicies = enumerator.Select(p => new PointIndex(p.iPoly, iInner, p.iVertex, innerPoly.ExteriorRing.Length - 1));

                foreach (PointIndex index in newIndicies)
                {
                    this._SegmentRTree.Add(index.Segment(this).BoundingBox.ToRTreeRect(0), index);
                }
            }

            //this._ExteriorSegmentRTree = null; //Reset our RTree
        }

        public void RemoveInteriorRing(int iInner)
        {
            this._InteriorPolygons.RemoveAt(iInner);

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        public void ReplaceInteriorRing(int iInner, GridPolygon replacement)
        {
            this._InteriorPolygons.RemoveAt(iInner);

            if (this._InteriorPolygons.Any(p => p.Intersects(replacement)))
                throw new ArgumentException("Cannot add interior polygon that intersects and existing interior polygon");

            if(this.ExteriorSegments.Any(line => line.Intersects(replacement)))
                throw new ArgumentException("Cannot add interior polygon that intersects a polygon's exterior boundary");

            this._InteriorPolygons.Insert(iInner, replacement);

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        /// <summary>
        /// Remove the interior polygon that contains the hole position
        /// </summary>
        /// <param name="holePosition"></param>
        public bool TryRemoveInteriorRing(GridVector2 holePosition)
        {
            for (int iPoly = 0; iPoly < _InteriorPolygons.Count; iPoly++)
            {
                if (_InteriorPolygons[iPoly].Contains(holePosition))
                {
                    _InteriorPolygons.RemoveAt(iPoly);
                    this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
                    return true;
                }
            }

            return false;
        }
        
        /// <summary>
        /// Adds a vertex to the polygon on the segment nearest to the point, including interior polygons.
        /// If the point is already a vertex no action is taken
        /// </summary>
        /// <param name="NewControlPointPosition"></param>
        public void AddVertex(GridVector2 NewControlPointPosition)
        {
            //Find the line segment the NewControlPoint intersects
            PointIndex nearestSegment = this.NearestSegment(NewControlPointPosition, out double segment_distance);
            AddVertex(NewControlPointPosition, nearestSegment);
        }

        /// <summary>
        /// Adds a vertex to the polygon after the specified point index
        /// If the point is already a vertex no action is taken
        /// </summary>
        /// <param name="NewControlPointPosition"></param>
        public void AddVertex(GridVector2 NewControlPointPosition, PointIndex nearestSegment)
        {
            if (nearestSegment.IsInner)
            {
                this.InteriorPolygons[nearestSegment.iInnerPoly.Value].AddVertex(NewControlPointPosition, nearestSegment.ReindexToOuter(0));
            }
            else
            {
                //Ensure the new point is not on either endpoint of the segment we are inserting between
                if (nearestSegment.Point(this) == NewControlPointPosition)
                    return;

                if (nearestSegment.Next.Point(this) == NewControlPointPosition)
                    return;

                var original_verts = this.ExteriorRing;
                GridLineSegment[] updatedSegments = this.ExteriorSegments.Insert(NewControlPointPosition, nearestSegment.iVertex);
                this.ExteriorRing = updatedSegments.Verticies();

                /*if (this.IsValid() == false)
                {
                    this.ExteriorRing = original_verts;
                    throw new ArgumentException("Adding vertex resulted in an invalid state.");
                }*/
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        /// <summary>
        /// Removes the vertex closest to the passed point
        /// </summary>
        /// <param name="RemovedControlPointPosition"></param>
        public void RemoveVertex(GridVector2 RemovedControlPointPosition)
        { 
            double MinDistance = this.NearestVertex(RemovedControlPointPosition, out PointIndex index);

            RemoveVertex(index);
        }

        public void RemoveVertex(PointIndex iVertex)
        {
            GridPolygon poly = iVertex.Polygon(this);
            poly.RemoveVertex(iVertex.iVertex);

            if(iVertex.IsInner)
            {
                this.InteriorRings[iVertex.iInnerPoly.Value] = poly.ExteriorRing;
                this._InteriorPolygons[iVertex.iInnerPoly.Value] = poly;
            }
            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }

        /// <summary>
        /// Removes the vertex from the exterior ring of a polgon only
        /// </summary>
        /// <param name="iVertex"></param>
        public void RemoveVertex(int iVertex)
        { 
            //We must have at least 3 points to create a polygon
            if (ExteriorSegments.Length <= 3)
            {
                throw new ArgumentException("Cannot remove vertex.  Polygon's must have three verticies.");
            }

            //Find the line segment the NewControlPoint intersects
            GridLineSegment[] updatedLineSegments = ExteriorSegments.Remove(iVertex);

            GridVector2[] original_verts = this.ExteriorRing;

            this.ExteriorRing = updatedLineSegments.Verticies();

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain

            if(this.IsValid() == false)
            {
                this.ExteriorRing = original_verts;
                throw new ArgumentException("Removing vertex resulted in an invalid state.");
            }
        }

        public bool IsValid()
        {
            if (this.ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED))
                return false;

            GridPolygon externalPolyOnly = new GridPolygon(this.ExteriorRing);

            //Check that the interior polygons are inside the exterior ring
            foreach(GridPolygon inner in this.InteriorPolygons)
            {
                if (inner.ExteriorRing.Any(v => externalPolyOnly.Contains(v) == false))
                    return false;

                if(GridPolygon.SegmentsIntersect(externalPolyOnly, inner))
                    return false;

                if (inner.IsValid() == false)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool IsVertex(GridVector2 point)
        {
            if(!this.BoundingBox.Contains(point))
            {
                return false;
            }

            if (this.ExteriorRing.Contains(point))
                return true;
            
            foreach(GridPolygon inner in this.InteriorPolygons)
            {
                if(!inner.BoundingBox.Contains(point))
                {
                    continue;
                }

                if (inner.IsVertex(point))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point">The PointIndex of the point if it is a vertex</param>
        /// <returns></returns>
        public bool TryGetIndex(GridVector2 point, out PointIndex index)
        {
            
            if (!this.BoundingBox.Contains(point))
            {
                index = new PointIndex();
                return false;
            }

            int iVert = this.ExteriorRing.IndexOf(point);
            if (iVert >= 0)
            {
                index = new PointIndex(0, iVert, this.ExteriorRing.Length-1);
                return true;
            }

            for (int iInner = 0; iInner < InteriorPolygons.Count; iInner++)
            {
                GridPolygon inner = InteriorPolygons[iInner];
                if (!inner.BoundingBox.Contains(point))
                {
                    continue;
                }

                if (inner.TryGetIndex(point, out index))
                {
                    index = index.ReindexToInner(iInner, 0);
                    return true;
                }
            }

            index = new PointIndex();
            return false;
        }

        /// <summary>
        /// Return true if the point is one of the polygon verticies
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public List<PointIndex> TryGetIndicies(ICollection<GridVector2> points)
        {
            List<PointIndex> found = new List<PointIndex>(points.Count);
            var candidates = points.Where(p => BoundingBox.Contains(p));
            List<GridVector2> notExterior = new List<GridVector2>(points.Count);

            foreach (GridVector2 point in points)
            {
                int iVert = this.ExteriorRing.IndexOf(point);
                if (iVert >= 0)
                {
                    found.Add(new PointIndex(0, iVert, this.ExteriorRing.Length - 1));
                    continue;
                }
                else
                {
                    for(int iInner = 0; iInner < InteriorPolygons.Count; iInner++)
                    {
                        if (InteriorPolygons[iInner].Contains(point) == false)
                            continue;

                        if(this.InteriorPolygons[iInner].TryGetIndex(point, out PointIndex innerIndex))
                        {
                            found.Add(innerIndex.ReindexToInner(iInner, 0));
                            break;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Returns true if the vertex on the exterior ring is concave
        /// </summary>
        /// <param name="iVert"></param>
        /// <returns></returns>
        public Concavity IsVertexConcave(int iVert, out double Angle)
        {
            int A = iVert - 1 < 0 ? ExteriorRing.Length - 2 : iVert - 1;
            int Origin = iVert;
            int B = iVert + 1 >= ExteriorRing.Length ? 1 : iVert + 1;

            Angle = GridVector2.AbsArcAngle(ExteriorRing[A], ExteriorRing[Origin], ExteriorRing[B], Clockwise: true);

            if (Angle == 0)
                return Concavity.PARALLEL;
            else if(Angle > Math.PI)
            {
                return Concavity.CONCAVE;
            }
            else
            {
                return Concavity.CONVEX;
            }
        }

        /// <summary>
        /// Returns true if the vertex on the exterior ring is concave
        /// </summary>
        /// <param name="iVert"></param>
        /// <returns></returns>
        public Concavity[] VertexConcavity(out double[] Angles)
        {
            Concavity[] results = new Concavity[ExteriorRing.Length];
            Angles = new double[ExteriorRing.Length];

            for(int i = 0; i < ExteriorRing.Length -1; i++)
            {
                results[i] = IsVertexConcave(i, out Angles[i]);
                //Trace.WriteLine(string.Format("{0}: {1} {2}", i, results[i], Angles[i]));
            }

            results[ExteriorRing.Length - 1] = results[0];
            Angles[ExteriorRing.Length - 1] = Angles[0];

            return results;
        }

        /// <summary>
        /// Returns true if the vertex on the exterior ring is concave
        /// </summary>
        /// <param name="iVert"></param>
        /// <returns></returns>
        public bool IsConvex()
        {
            return this.VertexConcavity(out double[] angles).All(c => c != Concavity.CONCAVE);
        }

        /// <summary>
        /// Returns the nearest segment to the point and the PointIndex of the line, use the Next function to obtain the vertex after the line
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="WorldPosition"></param>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public PointIndex NearestSegment(GridVector2 WorldPosition, out double nearestPolyDistance)
        {
            PointIndex nearestVertex = new PointIndex();
            nearestPolyDistance = double.MaxValue;
            double distance;

            for (int iRing = 0; iRing < this.InteriorPolygons.Count; iRing++)
            {
                GridPolygon innerPoly = this.InteriorPolygons[iRing];
                
                PointIndex foundVertex = innerPoly.NearestExternalSegment(WorldPosition, out distance);
                if (distance < nearestPolyDistance)
                {
                    nearestVertex = new PointIndex(0, iRing, foundVertex.iVertex, innerPoly.ExteriorRing.Length - 1);
                    nearestPolyDistance = distance;
                }
            }
             
            PointIndex iSegmentIndex = this.NearestExternalSegment(WorldPosition, out distance);
            if (distance < nearestPolyDistance)
            {
                nearestVertex = iSegmentIndex;
                nearestPolyDistance = distance;
            }

            return nearestVertex;
        }


        /// <summary>
        /// Returns the index and distance to the nearest line segment in an array, brute force.
        /// In the case where the segments are a poly-line and p is an endpoint, the segment with segment.A == p is returned.
        /// </summary>
        /// <param name="segments"></param>
        /// <param name="p"></param>
        /// <param name="MinDistance"></param>
        /// <returns></returns>
        public PointIndex NearestExternalSegment(GridVector2 p, out double MinDistance)
        {
            //Find the line segment the NewControlPoint intersects
            int iNearestSegment = ExteriorRing.TakeWhile(v => v != p).Count();
            if (iNearestSegment < ExteriorRing.Length)
            {
                MinDistance = 0;
                return new PointIndex(0, iNearestSegment, ExteriorRing.Length - 1);
            }
             
            double[] distancesToNewPoint = this.ExteriorSegments.Select(l => l.DistanceToPoint(p)).ToArray();
            double minDistance = distancesToNewPoint.Min();

            iNearestSegment = distancesToNewPoint.TakeWhile(d => d != minDistance).Count();
            MinDistance = minDistance;
            return new PointIndex(0, iNearestSegment, ExteriorRing.Length - 1);
        }


        public bool Contains(IPoint2D point_param)
        {
            return ContainsExt(point_param) != OverlapType.NONE;
        }

        public OverlapType ContainsExt(IPoint2D point_param)
        {
            if (!_BoundingRect.Contains(point_param))
                return OverlapType.NONE;

            GridVector2 p = new GridVector2(point_param.X, point_param.Y);

            //Create a line we know must pass outside the polygon
            //There is an edge case where the test line passes through a polygon vertex, so make sure the test line does not cross any verticies
            //GridVector2 targetPoint = new GridLineSegment(this.ExteriorRing[0], this.ExteriorRing[1]).Bisect();
            //GridVector2 targetPoint = new GridLineSegment(p.X, p.Y + this.ExteriorRing[0], this.ExteriorRing[1]).Bisect();

            //GridLine test_ray = new GridLine(point_param, targetPoint - point_param);

            //GridLineSegment test_line = test_ray.ToLine(Math.Max(BoundingBox.Width, BoundingBox.Height) * 2);

            
            List<GridLineSegment> segmentsToTest;

            if(_ExteriorSegments.Length > 32)// || HasInteriorRings)
            {
                segmentsToTest = _ExteriorSegments.ToList();

                ///This doesn't work because rTree returns the points in arbitrary order, and the line list must be passed to IsPointInsidePolygon in the order they appear on the ring.
                /*
                GridVector2 line_endpoint_translation = new GridVector2(BoundingBox.Width * 1.5, 0);
                GridLineSegment test_line_seg = new Geometry.GridLineSegment(p - line_endpoint_translation, p + line_endpoint_translation);
                var intersectingSegments = this.GetIntersectingSegments(test_line_seg.BoundingBox);
                segmentsToTest = this.AllSegments.Where(s => intersectingSegments.Contains(s)).ToList();
                */
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            //Make a horizontal line
            GridLine test_line = new GridLine(p, GridVector2.UnitX);

            //Test all of the line segments for both interior and exterior polygons
            //return IsPointInsidePolygonByWindingTest(segmentsToTest, test_line); 
            OverlapType result = IsPointInsidePolygonByWindingTest(segmentsToTest, test_line);
            if(result == OverlapType.CONTAINED)
            {
                foreach(GridPolygon inner in this.InteriorPolygons)
                {
                    OverlapType inner_result = inner.ContainsExt(p);
                    if (inner_result != OverlapType.NONE)
                        return OverlapType.NONE; //The point is in the inner polygon, therefore not part of this polygon
                }
            }

            return result;
        }

        /*
        static Random random = new Random();
        public bool ContainsWithPolyRayTest(IPoint2D point_param)
        {
            if (!_BoundingRect.Contains(point_param))
                return false;

            GridVector2 p = new GridVector2(point_param.X, point_param.Y);
            GridLineSegment? test_line = new GridLineSegment?();
            GridLine test_ray;
            //Create a line we know must pass outside the polygon
            //There is an edge case where the test line passes through a polygon vertex, so make sure the test line does not cross any verticies
            double test_line_length = Math.Max(BoundingBox.Width, BoundingBox.Height) * 2;
            GridVector2[] AllVerticies = this.AllVerticies;

            if (AllVerticies.Any(v => v == point_param))
                return true; 

            while (test_line.HasValue == false)
            {
                foreach (GridLineSegment s in this.ExteriorSegments)
                {
                    
                    GridVector2 targetPoint = s.PointAlongLine(random.NextDouble());
                    if (targetPoint == point_param)
                        continue;

                    test_ray = new GridLine(point_param, targetPoint - point_param);

                    test_line = test_ray.ToLine(test_line_length);
                    if (AllVerticies.Any(v => test_line.Value.DistanceToPoint(v) <= Global.Epsilon))
                    {
                        test_line = null; 
                        continue; //Too close to a vertex.  Try another target
                    }

                    break;
                }
            }
            
            
            //GridLineSegment test_line = new Geometry.GridLineSegment(p, new GridVector2(p.X + (BoundingBox.Width*2), p.Y));

            List<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(test_line.Value);
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            //Test all of the line segments for both interior and exterior polygons
            return IsPointInsidePolygonByRayTest(segmentsToTest, test_line.Value);
        }*/

        public bool Contains(GridLineSegment line)
        {
            //Ensure both endpoints are inside and a point in the center.
            //Test the center because if the line crosses a concave region with both endpoints exactly on the exterior ring we'd not have any intersections but the poly would not contain the line.
            if (!(this.Contains(line.A) && this.Contains(line.B) && this.Contains(line.PointAlongLine(0.5))))
                return false;

            List<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(line);
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList();
            }

            bool intersects = line.Intersects(segmentsToTest, true); //It is OK for endpoints to be on the exterior ring.
            if(intersects)
            {
                //The line intersects some of the polygon segments, but was it just the endpoint?
                return false; //Line is not entirely inside the polygon
            }

            foreach(GridPolygon innerPoly in this.InteriorPolygons)
            {
                if (innerPoly.Intersects(line) || innerPoly.Contains(line))
                    return false;
            }

            return true;
        }


        /// <summary>
        /// Return true if the polygon completely contains the circle
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Contains(GridCircle other)
        {
            GridRectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return false;

            //We cannot contain the other shape if the overlapping bounding box is not identical
            if (overlap.Value != other.BoundingBox)
                return false;

            //We must contain the center of the circle
            if (!this.Contains(other.Center))
            {
                return false;
            }

            //If our borders intersect we do not entirely contain the circle
            if (this.Intersects(other))
            {
                return false;
            }

            //If we have an interior hole inside the circle we don't entirely contain the circle.
            if(this.InteriorRings.Any(ir => other.Contains(ir[0])))
            {
                return false;
            }

            //Check case of line segment passing through a convex polygon or an interior polygon
            return true;
        }

        /// <summary>
        /// Return true if the polygon is completely inside the other
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public bool Contains(GridPolygon other)
        {
            GridRectangle? overlap = BoundingBox.Intersection(other.BoundingBox);
            if (!overlap.HasValue)
                return false;

            //We cannot contain the other shape if the overlapping bounding box is not identical
            if (overlap.Value != other.BoundingBox)
                return false;
            
            //Check case of interior polygon intersection
            if(!other.ExteriorRing.All(p => this.Contains(p)))
            {
                return false;
            }

            //Check case of line segment passing through a convex polygon or an interior polygon
            return !GridPolygon.SegmentsIntersect(this, other);
        }

        public bool InteriorPolygonContains(GridVector2 p)
        {
            GridPolygon intersectedPoly;
            return InteriorPolygonContains(p, out intersectedPoly);
        }

        public bool InteriorPolygonContains(GridVector2 p, out GridPolygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Contains(p))
                return false;

            //Check that our point is not inside an interior hole
            foreach (GridPolygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Contains(p))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        public bool InteriorPolygonIntersects(GridLineSegment line)
        { 
            return InteriorPolygonIntersects(line, out GridPolygon intersectedPoly);
        }

        public bool InteriorPolygonIntersects(GridLineSegment line, out GridPolygon interiorPolygon)
        {
            interiorPolygon = null;
            if (!_BoundingRect.Intersects(line.BoundingBox))
                return false;

            //Check that our point is not inside an interior hole
            foreach (GridPolygon innerPoly in _InteriorPolygons)
            {
                if (innerPoly.Intersects(line))
                {
                    interiorPolygon = innerPoly;
                    return true;
                }
            }

            return false;
        }

        public GridCircle InscribedCircle()
        {
            GridVector2 center = this.Centroid;
            double Radius = ExteriorRing.Select(p => GridVector2.Distance(center, p)).Min();
            return new GridCircle(center, Radius);
        }

        private static OverlapType IsPointInsidePolygonByWindingTest(List<GridLineSegment> polygonSegments, GridLine test_line)
        {
            GridVector2 test_point = test_line.Origin;

            if (polygonSegments.Any(ps => ps.IsEndpoint(test_line.Origin)))
                return OverlapType.TOUCHING;

#if DEBUG
            var OriginalSegments = polygonSegments.ToList(); //Create a copy so we can examine the debugger
#endif

            var IsLeft = polygonSegments.Select((s,i) => new { A = test_line.IsLeft(s.A), B = test_line.IsLeft(s.B), S = s, IsPLeftOfSeg=new int?()}).ToList();

            //OK, now we need to condense any instance where IsLeft.A or IsLeft.B == 0.  That is, the segment does not cross the line, mearly touches it. 
            //If we have opposite IsLeftValues we create a new edge that entirely crosses the line.  Otherwise we ignore the edge, which is the case where the segment touches the test_line but does not cross.
            for(int i = 0; i < IsLeft.Count; i++)
            {
                var seg = IsLeft[i];
                if (seg.A != seg.B || (seg.A == 0 && seg.B == 0))
                {
                    //Check the case of the point exactly on the line
                    if (seg.S.DistanceToPoint(test_point) < Global.Epsilon)
                        return OverlapType.TOUCHING;
                }

                if (seg.A == seg.B) //Remove all segments that are on the same side of the line or parallel to the line.  This leaves only segments that cross or touch the line
                {
                    //We can remove this segment entirely as it is perfectly parallel to our test line
                    polygonSegments.RemoveAt(i); 
                    IsLeft.RemoveAt(i);
                    i = i - 1;
                    continue;
                }
            }

            if (IsLeft.Count == 0)
                return OverlapType.NONE;

            //Find all segments that touch the line.  Remove the endpoints that touch the line and create a virtual segment that runs between the endpoints that did not touch the line.  This prevents double-counting windings.
            //InfiniteSequentialIndexSet SegEnumerator = new InfiniteSequentialIndexSet(0, IsLeft.Count, 0);
            for (int i = 0; i < IsLeft.Count; i++)
            {
                int iNext = i + 1 >= IsLeft.Count ? 0 : i + 1; //The index of the next entry in the list
                var seg = IsLeft[i];
                if (seg.A != 0 && seg.B != 0)
                {
                    //Check the case of the point exactly on the line
                    if (seg.S.DistanceToPoint(test_point) < Global.Epsilon)
                        return OverlapType.TOUCHING;

                    continue;   //Segment does not end on the line, continue;
                }
                  
                if(seg.B == 0) //Seg.A == 0 will be caught by a later iteration
                {
                    var nextSeg = IsLeft[iNext];
                    int nextSegIsLeft = nextSeg.A != 0 ? nextSeg.A : nextSeg.B; //Figure out which part of the next line is not on the test line.  Create a new virtual line or delete
                    GridVector2 nextSegEndpoint = nextSeg.A != 0 ? nextSeg.S.A : nextSeg.S.B;

                    Debug.Assert(nextSeg.S.OppositeEndpoint(nextSegEndpoint).Y == seg.S.B.Y, "We expect the lines to be input in the order they appear in the ring.  Lines sharing endpoints must be adjacent.");
                    
                    if (nextSegIsLeft == seg.A) //We touch the line and retreat.  We can remove both entries 
                    { 
                        polygonSegments.RemoveAt(Math.Max(i, iNext));
                        polygonSegments.RemoveAt(Math.Min(i, iNext));
                         
                        IsLeft.RemoveAt(Math.Max(i, iNext));
                        IsLeft.RemoveAt(Math.Min(i, iNext));

                        i -= i < iNext ? 1 : 2; //Adjust for wraparound case
                    }
                    else  //We touch the line and then cross over it.  We can remove both entries and add a new one
                    {
                        GridLineSegment virtualPolySegment = new GridLineSegment(seg.S.A, nextSegEndpoint);
                        polygonSegments.RemoveAt(i);
                        polygonSegments.Insert(i, virtualPolySegment);
                        polygonSegments.RemoveAt(iNext);

                        var newEntry = new { A = seg.A, B = nextSegIsLeft, S = virtualPolySegment, IsPLeftOfSeg= new int?(seg.S.IsLeft(test_point))}; //Record whether the lines were left of the test_point in case the new line moves to the other side of the point.
                        IsLeft.RemoveAt(i);
                        IsLeft.Insert(i, newEntry);
                        IsLeft.RemoveAt(iNext);

                        //i = i; //Adjust to check the next record 
                    }
                }
            }

            var cross_or_parallel_segments = polygonSegments; //polygonSegments.Where((s, i) => (IsLeft[i].A != IsLeft[i].B) || (IsLeft[i].A == 0 || IsLeft[i].B == 0)).ToArray(); //Find all segments that span the testline or are parallel

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            //if (cross_or_parallel_segments.Any(ps => ps.IsEndpoint(test_line.A)))
            //    return OverlapType.TOUCHING;

            int wind_count = 0; 
            for(int i = 0; i < cross_or_parallel_segments.Count; i++)
            {
                var SegData = IsLeft[i];
                GridLineSegment polySeg = SegData.S;
                int IsAboveToBelow;
                int pIsLeft;

                IsAboveToBelow = SegData.S.A.Y.CompareTo(SegData.S.B.Y);

                if (SegData.IsPLeftOfSeg.HasValue == false)
                {
                    pIsLeft = polySeg.IsLeft(test_point);
                }
                else
                {
                    pIsLeft = SegData.IsPLeftOfSeg.Value;
                }
                
                /*if(IsAboveToBelow == 0) //Case of parallel line
                {
                    if(polySeg.BoundingBox.Left <= test_point.X && polySeg.BoundingBox.Right >= test_point.X)
                    {
                        return OverlapType.TOUCHING; //Test point is within the line segment, return true   
                                    //We aren't using epsilon here, perhaps we should?
                    }
                    continue;
                }
                else*/ if(IsAboveToBelow > 0)
                {
                    if (pIsLeft >= 0)
                        wind_count += 1;
                }
                else //IsAbove < 0
                {
                    if (pIsLeft <= 0)
                        wind_count -= 1;
                }
            }

            return wind_count != 0 ? OverlapType.CONTAINED : OverlapType.NONE;
        }

        private static bool IsPointInsidePolygonByRayTest(ICollection<GridLineSegment> polygonSegments, GridLineSegment test_line)
        {
            //In cases where our test line passes exactly through a vertex on the other polygon we double count the line.  
            //This code removes duplicate intersection points to prevent duplicates

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            if (polygonSegments.Any(ps => ps.SharedEndPoint(test_line)))
                return true;

            List<GridVector2> intersections;
            IEnumerable<GridLineSegment> IntersectedSegments;

            if (polygonSegments.Count > 128)
            {
                System.Collections.Concurrent.ConcurrentBag<GridVector2> intersectionsBag = new System.Collections.Concurrent.ConcurrentBag<Geometry.GridVector2>();

                IntersectedSegments = polygonSegments.Where(line =>
                {
                    GridVector2 Intersection;
                    bool intersected = line.Intersects(test_line, out Intersection);
                    if (intersected)
                    {
                        intersectionsBag.Add(Intersection);
                    }

                    return intersected;
                }).AsParallel().ToList(); //Need ToList here to ensure the query executes fully

                intersections = new List<GridVector2>(intersectionsBag);
            }
            else
            {
                intersections = new List<GridVector2>(polygonSegments.Count);

                IntersectedSegments = polygonSegments.Where(line =>
                {
                    GridVector2 Intersection;
                    bool intersected = line.Intersects(test_line, out Intersection);
                    if (intersected)
                    {
                        intersections.Add(Intersection);
                    }

                    return intersected;
                }).ToList(); //Need ToList here to ensure the query executes fully
            }

            //Ensure the line doesn't pass through on a line endpoint
            //SortedSet<GridVector2> intersectionPoints = new SortedSet<GridVector2>();
            GridVector2[] UniqueIntersections = intersections.Distinct().ToArray();
            
            if (UniqueIntersections.Any(p => test_line.IsEndpoint(p)))
                return true; //If the point is exactly on the line then we can often have two intersections as the line leaves the polygon which results in a false negative.
                             //This test short-circuits that problem
                           
            //If the intersection point is exactly through a polygon vertex then two segments will be returned but we should count only one.
            if(UniqueIntersections.Length != intersections.Count)
            {
                throw new NotImplementedException("This is an edge case where the line passes through a vertex of the polygon.");

                //The fix is to create a new testline that does not pass through any verticies
            }
            
            //Inside the polygon if we intersect line segments of the border an odd number of times
            return UniqueIntersections.Length % 2 == 1; 
        }


        private GridLineSegment[] CreateLineSegments(GridVector2[] ring_points)
        {
            GridLineSegment[] lines = new GridLineSegment[ring_points.Length-1];

            for (int iPoint = 0; iPoint < ring_points.Length-1; iPoint++)
            {
                GridLineSegment line = new Geometry.GridLineSegment(ring_points[iPoint], ring_points[iPoint + 1]);
                lines[iPoint] = line;
            }

            return lines;
        }

        private static RTree.RTree<GridLineSegment> CreateSegmentBoundingBoxRTree(GridLineSegment[] segments)
        {
            RTree.RTree<GridLineSegment> R = new RTree.RTree<GridLineSegment>();

            foreach(GridLineSegment l in segments)
            {
                R.Add(l.BoundingBox.ToRTreeRect(0), l);
            }
              
            return R;
        }

        private static RTree.RTree<PointIndex> CreatePointIndexSegmentBoundingBoxRTree(GridPolygon poly)
        {
            RTree.RTree<PointIndex> R = new RTree.RTree<PointIndex>();

            PolygonVertexEnum enumerator = new PolygonVertexEnum(poly);
            foreach(PointIndex p in enumerator)
            {
                GridLineSegment s = p.Segment(poly);
                R.Add(s.BoundingBox.ToRTreeRect(0), p);
            }

            return R;
        }

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public List<GridLineSegment> GetIntersectingSegments(GridLineSegment line)
        {
            GridRectangle bbox = line.BoundingBox;
            if (!this.BoundingBox.Intersects(bbox))
            {
                return new List<Geometry.GridLineSegment>(0);
            }

            return SegmentRTree.Intersects(bbox.ToRTreeRect(0)).Select(p => p.Segment(this)).Where(segment => line.Intersects(segment, false)).ToList();
        }

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public List<GridLineSegment> GetIntersectingSegments(GridRectangle bbox)
        {
            if(!this.BoundingBox.Intersects(bbox))
            {
                return new List<Geometry.GridLineSegment>(0);
            }

            return SegmentRTree.Intersects(bbox.ToRTreeRect(0)).Select(p => p.Segment(this)).Where(segment => bbox.Intersects(segment)).ToList();
        }

        public GridPolygon Rotate(double angle, GridVector2? origin = null)
        {
            if(!origin.HasValue)
            {
                origin = this.Centroid;
            } 
            
            GridVector2[] RotatedRing = this.ExteriorRing.Rotate(angle, origin.Value);

            GridPolygon poly = new GridPolygon(RotatedRing);

            foreach(GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon rotated_inner = innerRing.Rotate(angle, origin);
                poly.AddInteriorRing(rotated_inner);
            }

            return poly;
        }

        public GridPolygon Scale(double scalar, GridVector2? origin = null)
        {
            return this.Scale(new GridVector2(scalar, scalar), origin);
        }

        public GridPolygon Scale(GridVector2 scalar, GridVector2? origin = null)
        {
            if (!origin.HasValue)
            {
                origin = this.Centroid;
            }

            GridVector2[] ScaledRing = this.ExteriorRing.Scale(scalar, origin.Value);

            GridPolygon poly = new GridPolygon(ScaledRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon scaled_inner = innerRing.Scale(scalar, origin);
                poly.AddInteriorRing(scaled_inner);
            }

            return poly;
        }

        public GridPolygon Translate(GridVector2 offset)
        {
            GridVector2[] TranslatedRing = this.ExteriorRing.Translate(offset);

            GridPolygon poly = new GridPolygon(TranslatedRing);

            foreach (GridPolygon innerRing in this._InteriorPolygons)
            {
                GridPolygon translated_inner = innerRing.Translate(offset);
                poly.AddInteriorRing(translated_inner);
            }

            return poly;
        }

        public GridPolygon Smooth(uint NumInterpolationPoints)
        {
            return GridPolygon.Smooth(this, NumInterpolationPoints);
        }

        public static GridVector2 CalculateCentroid(GridVector2[] ExteriorRing, bool ValidateRing = true)
        {
            double accumulator_X = 0;
            double accumulator_Y = 0;

            //To prevent precision errors we subtract the average value and add it again
            ExteriorRing = ExteriorRing.EnsureClosedRing().ToArray();
            GridVector2 Average = ExteriorRing.Average();
            GridVector2[] translated_Points = ExteriorRing.Translate(-Average);

            for (int i = 0; i < translated_Points.Length - 1; i++)
            {
                GridVector2 p0 = translated_Points[i];
                GridVector2 p1 = translated_Points[i + 1];
                double SharedTerm = ((p0.X * p1.Y) - (p1.X * p0.Y));
                accumulator_X += (p0.X + p1.X) * SharedTerm;
                accumulator_Y += (p0.Y + p1.Y) * SharedTerm;
            }

            double ExteriorArea = translated_Points.PolygonArea();
            double scalar = ExteriorArea * 6;

            return new GridVector2((accumulator_X / scalar) + Average.X, (accumulator_Y / scalar) + Average.Y);
        }

        public static GridPolygon Smooth(GridPolygon poly, uint NumInterpolationPoints)
        {
            GridVector2[] smoothedCurve = poly.ExteriorRing.CalculateCurvePoints(NumInterpolationPoints, true);

            //GridVector2[] simplifiedCurve = smoothedCurve.DouglasPeuckerReduction(.5, poly.ExteriorRing).EnsureClosedRing().ToArray();

            GridPolygon smoothed_poly = new GridPolygon(smoothedCurve);

            foreach(GridPolygon inner_poly in poly.InteriorPolygons)
            {
                GridPolygon smoother_inner_poly = GridPolygon.Smooth(inner_poly, NumInterpolationPoints);
                smoothed_poly.AddInteriorRing(smoother_inner_poly);
            }

            return smoothed_poly;
        }

        public double Distance(GridVector2 p)
        {
            return this.ExteriorSegments.Min(line => line.DistanceToPoint(p));
        }

        public double Distance(GridVector2 p, out GridLineSegment nearestLine)
        {
            double minDistance = double.MaxValue;
            nearestLine = ExteriorSegments.First();

            for (int i = 0; i < ExteriorSegments.Length; i++)
            {
                double dist = ExteriorSegments[i].DistanceToPoint(p);
                if(dist < minDistance)
                {
                    nearestLine = ExteriorSegments[i];
                    minDistance = dist;
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Brute force search for distance
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Distance(GridLineSegment p)
        {
            double minDistanceA = Distance(p.A);
            double minDistanceB = Distance(p.B);
            double minDistanceLine = ExteriorRing.Min(es => p.DistanceToPoint(es));

            return new double[] { minDistanceA, minDistanceB, minDistanceLine }.Min();
        }

        /// <summary>
        /// Brute force search for distance
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public double Distance(GridPolygon other)
        {
            if (this.Intersects(other))
                return 0; 

            double minDistanceToOtherLineSegment = this.ExteriorRing.Min(p => other.Distance(p));
            double minDistanceToThisLineSegment = other.ExteriorRing.Min(p => this.Distance(p));

            return Math.Min(minDistanceToOtherLineSegment, minDistanceToThisLineSegment);
        }

        /// <summary>
        /// Given a point inside the polygon return the normalized distance.
        /// Create a line passing through the centroid and the point. 
        /// Locate the nearest intersecting line segment in the exterior ring.
        /// Measure the distance
        /// </summary>
        /// <param name="p"></param>
        public double DistanceFromCenterNormalized(GridVector2 p)
        {
            GridLine line = new Geometry.GridLine(Centroid, p - Centroid);

            List<GridVector2> Intersections = new List<Geometry.GridVector2>(ExteriorRing.Length);
            for (int i = 0; i < _ExteriorSegments.Length; i++)
            {
                GridVector2 intersection;
                if (line.Intersects(this._ExteriorSegments[i], out intersection))
                {
                    double CenterDist = GridVector2.Distance(Centroid, intersection);
                    double PointDist = GridVector2.Distance(p, intersection);
                    //Since the line is infinite we need to ignore cases where the intersection is between the point and the line, or on the other side of the center point
                    //  I------P---------I-----I------C-------I
                    //  In the example above slicing through a concave poly we'd want the first Intersection (I) to determine the distance from center normalized
                    
                }
            }

            throw new NotImplementedException();
        }

        public int[] VerticiesOnConvexHull()
        {
            int[] indicies; 
            GridVector2[] convex_hull_verts = this.ExteriorRing.ConvexHull(out indicies);

            return indicies;
        }

        public object Clone()
        {
            GridPolygon clone = new Geometry.GridPolygon(this.ExteriorRing.Clone() as GridVector2[]);
            foreach(GridPolygon innerPoly in this.InteriorPolygons)
            {
                GridPolygon innerClone = innerPoly.Clone() as GridPolygon;
                clone.AddInteriorRing(innerClone);
            }

            return clone;
        }

        public override string ToString()
        {
            if(this.HasInteriorRings)
            {
                return string.Format("Poly with {0} verts, {1} interior rings", this.TotalUniqueVerticies, this.InteriorRings.Count);
            }
            else
            {
                return string.Format("Poly with {0} verts", this.TotalUniqueVerticies);
            }
        }

        public bool Intersects(IShape2D shape)
        {
            return ShapeExtensions.PolygonIntersects(this, shape);
        }


        public bool Intersects(ICircle2D c)
        {
            GridCircle circle = c.Convert();
            return this.Intersects(circle);
        }

        public bool Intersects(GridCircle circle)
        {
            return PolygonIntersectionExtensions.Intersects(this, circle);
        }


        public bool Intersects(ILineSegment2D l)
        {
            GridLineSegment line = l.Convert();
            return this.Intersects(line);
        }

        public bool Intersects(GridLineSegment line)
        {
            return PolygonIntersectionExtensions.Intersects(this, line);
        }

        public bool Intersects(ITriangle2D t)
        {
            GridTriangle tri = t.Convert();
            return this.Intersects(tri);
        }

        public bool Intersects(GridTriangle tri)
        {
            return PolygonIntersectionExtensions.Intersects(this, tri);
        }

        public bool Intersects(IPolygon2D p)
        {
            GridPolygon poly = p.Convert();
            return this.Intersects(poly);
        }

        /// <summary>
        /// Return true if the polygon contains or intersects the other polygon
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Intersects(GridPolygon other)
        {
            GridRectangle? Intersection = this.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case of the other polygon entirely inside
            if (other.ExteriorRing.Any(p => this.Contains(p)))
                return true;

            return SegmentsIntersect(this, other);
        }

        /// <summary>
        /// Return true if segments of the polygons intersect.  Returns false if the other triangle is entirely contained by poly
        /// </summary>
        /// <param name="poly"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool SegmentsIntersect(GridPolygon poly, GridPolygon other)
        {
            GridRectangle? Intersection = poly.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case for a line segment passing entirely through the polygon.
            GridRectangle overlap = Intersection.Value;

            List<GridLineSegment> CandidateSegments = poly.GetIntersectingSegments(overlap);

            foreach (GridLineSegment candidate in CandidateSegments)
            {
                List<GridLineSegment> OtherSegments = other.GetIntersectingSegments(candidate);

                if (OtherSegments.Count > 0)
                    return true; 
            }

            return false;
        }

        bool IShape2D.Contains(IPoint2D p)
        {
            return this.Contains(p.Convert());
        }
         
        IShape2D IShape2D.Translate(IPoint2D offset)
        {
            GridVector2 v = offset.Convert();
            return this.Translate(v);
        }

        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of our segments
        /// </summary>
        /// <param name="other"></param>
        /// <returns>All intersection points, including pre-existing and added</returns>
        public List<GridVector2> AddPointsAtIntersections(GridPolygon other)
        {
            List<GridVector2> found_or_added_intersections = new List<GridVector2>();
            GridRectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return found_or_added_intersections;

            List<GridVector2> newRing = new List<Geometry.GridVector2>();

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);

                newRing.Add(ExteriorRing[i]);

                GridVector2[] IntersectionPoints;
                //Since we want the out parameter just get a quick list of candidates with the ls.bounding box in instead of running the full intersection test twice.
                List<GridLineSegment> candidates = ls.Intersections(other.GetIntersectingSegments(ls.BoundingBox), out IntersectionPoints); 

                //Remove any duplicates of the existing endpoints 
                for (int iInter = 0; iInter < IntersectionPoints.Length; iInter++)
                {
                    GridVector2 p = IntersectionPoints[iInter];

                    //Nudge the corresponding point just a bit so we don't have two sets of colinear points which tends to expose 
                    //floating point rounding errors in the geometry algorithms.  
                    //TODO: Find the intersection of a curve using the exterior rings
                    /*double minDist = Math.Min(ls.Length, candidates[iInter].Length);
                    double fudgeDistance = minDist * 0.05;
                    GridVector2 fudgeFactor = new GridVector2(fudgeDistance, fudgeDistance);

                    GridVector2 fudged_p = p + fudgeFactor;
                    */
                    //System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                    if (!newRing.Contains(p))
                    {
                        double other_vertex_distance = other.NearestVertex(p, out PointIndex other_vertex_index);

                        //If we intersect close enough to another vertex on the other polygon, just add that point to ourselves.
                        if (other_vertex_distance == 0)
                        {
                            //Vertex exists in the other polygon at exact position
                            newRing.Add(p);
                            found_or_added_intersections.Add(p);
                        }
                        else if (other_vertex_distance < Global.Epsilon)
                        {
                            //Use the position of the existing vertex in the other polygon for our own position
                            newRing.Add(other_vertex_index.Point(other));
                            found_or_added_intersections.Add(other_vertex_index.Point(other));
                        }
                        else
                        {
                            //Intersection point is not a  vertex on either polygon
                            newRing.Add(p);
                            other.AddVertex(p);
                            found_or_added_intersections.Add(p);
                        }
                        

                        //Trace.WriteLine(string.Format("Add Corresponding Point {0}", p));
                    }
                    else
                    {
                        int existingIndex = newRing.IndexOf(p);
                        //Ensure the intersection point occurs in the other polygon
                        double other_vertex_distance = other.NearestVertex(p, out PointIndex other_vertex_index);

                        //We need the point to be exact, so adjust our point accordingly
                        if (other_vertex_distance == 0)
                        {
                            //No action needed.  Vertex exists in the other polygon at exact position and in this polygon at exact position.
                            //We still report the intersection though
                            found_or_added_intersections.Add(other_vertex_index.Point(other));
                        }
                        else if (other_vertex_distance < Global.Epsilon)
                        {
                            //Use the position of the existing vertex in the other polygon for our own position
                            newRing[existingIndex] = other_vertex_index.Point(other);
                            found_or_added_intersections.Add(other_vertex_index.Point(other));
                        }
                        else
                        {
                            //We have the vertex, but not the other polygon.  Add the vertex to the other polygon
                            other.AddVertex(p);
                            found_or_added_intersections.Add(p);
                        }

                    }
                }
            }

            newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);
            
            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = newRing.ToArray();

            foreach (GridPolygon otherInnerPolygon in other.InteriorPolygons)
            {
                found_or_added_intersections.AddRange(this.AddPointsAtIntersections(otherInnerPolygon));
            }

            foreach (GridPolygon innerPolygon in this._InteriorPolygons)
            {
                found_or_added_intersections.AddRange(innerPolygon.AddPointsAtIntersections(other));
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain

            return found_or_added_intersections;
        }

        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of our segments
        /// </summary>
        /// <param name="other"></param>
        public void AddPointsAtIntersections(GridLineSegment other)
        {
            GridRectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return;

            List<GridVector2> newRing = new List<Geometry.GridVector2>();

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);

                newRing.Add(ExteriorRing[i]);

                IShape2D intersection;
                
                var intersects = ls.Intersects(other, true, out intersection); //Don't check the endpoints of the segment because we are already adding them

                if(intersects)
                {
                    //The intersection could be a line, which we can't really add an infinite number of points for... we could add internal endpoints, but for now we add point intersections only.
                    IPoint2D point = intersection as IPoint2D;
                    if(point != null)
                    {
                        GridVector2 p = new GridVector2(point.X, point.Y);
                        System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                        newRing.Add(p);
                    }
                }
            }

            newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);

            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = newRing.ToArray();

            foreach (GridPolygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }


        /// <summary>
        /// Add a vertex to our rings everywhere the other polygon intersects one of the passed segments
        /// </summary>
        /// <param name="other"></param>
        public void AddPointsAtIntersections(GridLineSegment[] other)
        {
            //Only check the lines that could intersect our polygon
            other = other.Where(o => this.BoundingBox.Intersects(o.BoundingBox)).ToArray();

            List<GridVector2> newRing = new List<Geometry.GridVector2>();

            for (int i = 0; i < ExteriorRing.Length - 1; i++)
            {
                GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);

                //Don't add the point if it is too close
                if(newRing.Count == 0 || GridVector2.DistanceSquared(newRing.Last(), ExteriorRing[i]) > Global.EpsilonSquared)
                    newRing.Add(ExteriorRing[i]);

                GridVector2[] IntersectionPoints;
                List<GridLineSegment> candidates = ls.Intersections(other, out IntersectionPoints);

                //Remove any duplicates of the existing endpoints 
                foreach (GridVector2 p in IntersectionPoints)
                {
                    System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                    //Don't add the point if it is too close
                    if (newRing.Count == 0 || GridVector2.DistanceSquared(newRing.Last(), p) > Global.EpsilonSquared)
                        newRing.Add(p);
                }
            }

            if (newRing.Count == 0 || GridVector2.DistanceSquared(newRing.Last(), ExteriorRing[ExteriorRing.Length - 1]) > Global.EpsilonSquared)
                newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);

            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = newRing.ToArray();

            foreach (GridPolygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            }

            this._SegmentRTree = null; //Reset our RTree since yanking a polygon and changing the indicies are a pain
        }


        public Dictionary<GridVector2, PointIndex> CreatePointToPolyMap()
        {
            var map = CreatePointToPolyMap(new GridPolygon[] { this });
            Dictionary<GridVector2, PointIndex> flatMap = new Dictionary<Geometry.GridVector2, Geometry.PointIndex>(); //The map without the possibility of multiple verticies at the same position

            foreach (GridVector2 p in map.Keys)
            {
                flatMap.Add(p, map[p].First());
            }

            return flatMap;
        }

        /// <summary>
        /// Creates a lookup table for verticies to a polygon index.  Polygons may not share verticies.
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static Dictionary<GridVector2, PointIndex> CreatePointToPolyMap2D(GridPolygon[] Polygons)
        {
            Dictionary<GridVector2, PointIndex> pointToPoly = new Dictionary<GridVector2, PointIndex>();
            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;

                //Subtract one from ring length to prevent duplicate point key insertion since they are closed rings
                for (int iVertex = 0; iVertex < poly.ExteriorRing.Length-1; iVertex++)
                {
                    GridVector2 p = poly.ExteriorRing[iVertex];
                    PointIndex value = new PointIndex(iPoly, iVertex, Polygons);

                    if (pointToPoly.ContainsKey(p))
                    {
                        throw new ArgumentException(string.Format("Duplicate vertex {0}", p));
                    }

                    pointToPoly.Add(p, value);
                }

                
                for (int iInnerPoly = 0; iInnerPoly < poly.InteriorPolygons.Count; iInnerPoly++)
                {
                    GridPolygon innerPolygon = poly.InteriorPolygons.ElementAt(iInnerPoly);

                    //Subtract one from ring length to prevent duplicate point key insertion since they are closed rings
                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length-1; iVertex++)
                    {
                        GridVector2 p = innerPolygon.ExteriorRing[iVertex];

                        PointIndex value = new PointIndex(iPoly, iInnerPoly, iVertex, Polygons);
                        if (pointToPoly.ContainsKey(p))
                        {
                            throw new ArgumentException(string.Format("Duplicate inner polygon vertex {0}", p));
                        }

                        List<PointIndex> indexList = new List<Geometry.PointIndex>();
                        indexList.Add(value);
                        pointToPoly.Add(p, value);
                    }
                }
            }

            return pointToPoly;
        }

        /// <summary>
        /// Creates a lookup table for verticies to a polygon index.  Polygons may share verticies.
        /// </summary>
        /// <param name="Polygons">An array of N polygons.</param>
        /// <param name="iPoly">An array of N indicies.  If not null PointIndex values will use the corresponding entry in this array for the
        /// Polygon index instead of the position in the passed Polygons array.  This is useful when generating a map for a subset of a larger 
        /// collection of polygons. </param>
        /// <returns></returns>
        public static Dictionary<GridVector2, List<PointIndex>> CreatePointToPolyMap(GridPolygon[] Polygons, IReadOnlyList<int> PolygonIndicies = null)
        {
            Dictionary<GridVector2, List<PointIndex>> pointToPoly = new Dictionary<GridVector2, List<PointIndex>>();
            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                int iPolygon = iPoly; //Used to adjust polygon index if PolygonIndicies is remapping those values
                if(PolygonIndicies != null)
                { 
                    iPolygon = PolygonIndicies[iPoly];
                }

                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;
                for(int iVertex = 0; iVertex < poly.ExteriorRing.Length - 1; iVertex++)
                {
                    GridVector2 p = poly.ExteriorRing[iVertex];
                    PointIndex value = new PointIndex(iPolygon, iVertex, Polygons);

                    if (pointToPoly.ContainsKey(p))
                    {
                        pointToPoly[p].Add(value);
                        continue;
                    }
                     
                    List<PointIndex> indexList = new List<Geometry.PointIndex>();
                    indexList.Add(value);
                    pointToPoly.Add(p, indexList);
                }

                for (int iInnerPoly = 0; iInnerPoly < poly.InteriorPolygons.Count; iInnerPoly++)
                {
                    GridPolygon innerPolygon = poly.InteriorPolygons.ElementAt(iInnerPoly);

                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length - 1; iVertex++)
                    {
                        GridVector2 p = innerPolygon.ExteriorRing[iVertex];

                        PointIndex value = new PointIndex(iPolygon, iInnerPoly, iVertex, Polygons);
                        if (pointToPoly.ContainsKey(p))
                        {
                            pointToPoly[p].Add(value);
                            continue;
                        }
                        
                        List<PointIndex> indexList = new List<Geometry.PointIndex>();
                        indexList.Add(value);
                        pointToPoly.Add(p, indexList);
                    }
                }
            }

            return pointToPoly;
        }

        public static GridPolygon WalkPolygonCut(GridPolygon input, RotationDirection direction, IList<GridVector2> cutLine)
        {
            return WalkPolygonCut(input, direction, cutLine, out PointIndex FirstIntersection, out PointIndex LastIntersection, out List<GridVector2> intersecting_cutline_verts);
        }


        /// <summary>
        /// Given a polyline, find two locations where it intersects the polygon and walk the polygon in either clockwise/counter-clockwise direction from the first intersection of the cutline to the second, add the cutline to close the ring, and return the resulting polygon.
        /// </summary>
        /// <param name="start_index"></param>
        /// <param name="input">The polygon to cut/extend</param>
        /// <param name="direction">The direction we will walk to connect the starting and ending cut points</param>
        /// <param name="cutLine">The line cutting the polygon.  It should intersect the same polygonal ring in two locations without intersecting any others</param>
        /// <param name="FirstIntersect">The polygon vertex before the intersected segment, use intersect_index.next to get the endpoint of the intersected segment of the polygon</param>
        /// <returns></returns>
        public static GridPolygon WalkPolygonCut(GridPolygon input, RotationDirection direction, IList<GridVector2> cutLine, out PointIndex FirstIntersection, out PointIndex LastIntersection, out List<GridVector2> intersecting_cutline_verts)
        {
            
            //Find a possible intersection point for the retrace
            GridLineSegment[] cutLines = cutLine.ToLineSegments();
            intersecting_cutline_verts = new List<GridVector2>(); //Every vert in the path that crosses the two polygon
            List<PointIndex> IntersectingPointIndicies = new List<PointIndex>();
            bool FirstCutIntersectionFound = false;

            //Add the intersection points to the polygon
            GridPolygon output = input.Clone() as GridPolygon;
            output.AddPointsAtIntersections(cutLines);

            //Identify where the cut crosses the polygon rings 
            for(int iVert = 0; iVert < cutLine.Count-1; iVert++)
            {
                GridLineSegment segment = new GridLineSegment(cutLine[iVert], cutLine[iVert+1]);
                
                var intersections = output.IntersectingSegments(segment);

                if (FirstCutIntersectionFound)
                {
                    if ( intersections.Count == 0)
                    {
                        intersecting_cutline_verts.Add(segment.B);
                    }
                    else
                    {
                    }
                    
                }
                else if(intersections.Count == 1)
                {
                    FirstCutIntersectionFound = true;
                    intersecting_cutline_verts.Add(segment.B);
                }
                else if(intersections.Count > 1)
                {
                    //We'll exit, but since we found two intersections at once none of the path is inside the polygon
                }

                IntersectingPointIndicies.AddRange(intersections.Values);

                if (IntersectingPointIndicies.Count >= 2)
                {
                    //intersecting_cutline_verts.Add(cutLine[iVert + 1]);
                    break;
                } 
            }

            if (IntersectingPointIndicies.Count == 0)
            {
                throw new ArgumentException("cutLine must intersect a polygon ring");
            }
            else if(IntersectingPointIndicies.Count == 1)
            {
                FirstIntersection = IntersectingPointIndicies[0];
                throw new ArgumentException("cutline must intersect a polygon ring a second time.");
            }
             
            //Identify the first vertex of the segment of the polygon that intersects the cut line
            FirstIntersection = IntersectingPointIndicies[IntersectingPointIndicies.Count - 2];
            LastIntersection = IntersectingPointIndicies[IntersectingPointIndicies.Count - 1];

            if (false == FirstIntersection.AreOnSameRing(LastIntersection))
            {
                throw new ArgumentException("Cut line must cross segments on the same ring of the polygon");
            }

            //Drop the first cut intersection because it will be on the wrong side of the polygon border
            //intersecting_cutline_verts.RemoveAt(0);
             
            return WalkPolygonCut(FirstIntersection,
                                  LastIntersection,
                                  output,
                                  direction,
                                  intersecting_cutline_verts);
        }


        /// <summary>
        /// Given a polyline that crosses the same ring of the polygon at two points on the same ring, returns the polygon that results from walking the polygon either clockwise-or-counter clockwise around the cut line. 
        /// This can be used to cut a polygon into arbitrary parts.
        /// </summary>
        /// <param name="start_index">The vertex of the polygon the cut begins at</param>
        /// <param name="intersect_index">The vertex of the polygon the cut ends at</param>
        /// <param name="originPolygon">Polygon we are cutting</param>
        /// <param name="direction">Build the polygon with a clockwise or counterclockwise rotation order from the start_index</param>
        /// <param name="cutLine">The verticies of the cutline.  Must be entirely inside or outside the polygon and not intersect any rings</param>
        /// <returns></returns>
        public static GridPolygon WalkPolygonCut(PointIndex start_index, PointIndex end_index, GridPolygon originPolygon, RotationDirection direction, IList<GridVector2> cutLine)
        {
            if (false == end_index.AreOnSameRing(start_index))
            {
                throw new ArgumentException("Cut must run between the same ring of the polygon without intersecting other rings");
            }

            //Walk the ring using Next to find perimeter on one side, the walk using prev to find perimeter on the other
            List<GridVector2> walkedPoints = new List<GridVector2>();
            PointIndex current = start_index;

            //Add the points from the polygon
            do
            {
                Debug.Assert(walkedPoints.Contains(current.Point(originPolygon)) == false);
                walkedPoints.Add(current.Point(originPolygon));
                if (direction == RotationDirection.COUNTERCLOCKWISE)
                    current = current.Next;
                else
                    current = current.Previous;

            }
            while (current != end_index);

            walkedPoints.Add(end_index.Point(originPolygon));

            //Add the intersection point of where we crossed the boundary 
            //List<GridVector2> SimplifiedPath = CurveSimplificationExtensions.DouglasPeuckerReduction(cutLine, Global.PenSimplifyThreshold);
            //Since we start walking the polygon from the first intersection point we always add the cutline in reverse order to return to the cirst intersection point.
            List<GridVector2> SimplifiedPath = cutLine.Reverse().ToList();

            //The intersection point marks where we enter the polygon.  The first point in the path is not added because it indicates where the line exited the cut region. 
            //Add the PenInput.Path 

            //Temp for debugging ///////////////
            for (int iCut = 0; iCut < SimplifiedPath.Count; iCut++)
            {
                Debug.Assert(walkedPoints.Contains(SimplifiedPath[iCut]) == false);
                if (GridVector2.DistanceSquared(SimplifiedPath[iCut], walkedPoints.Last()) <= Geometry.Global.EpsilonSquared)
                {
                    int i = 5; //Temp for debugging
                    continue;
                }

                walkedPoints.Add(SimplifiedPath[iCut]);
            }
            /////////////////////////////////////
            ///
            //walkedPoints.AddRange(cutLine);
#if DEBUG
            //Ensure we do not have duplicates in our list
            GridVector2[] walkedPoints_noduplicates = walkedPoints.RemoveDuplicates();
            Debug.Assert(walkedPoints_noduplicates.Length == walkedPoints.Count);
#endif

            //Close the ring
            walkedPoints.Add(start_index.Point(originPolygon));

            /*
            Debug.Assert(walkedPoints.ToArray().AreClockwise() == (direction == RotationDirection.CLOCKWISE));
            
            if(direction == RotationDirection.CLOCKWISE)
            {
                walkedPoints.Reverse();
            }
             */
            GridPolygon output = new GridPolygon(walkedPoints.EnsureClosedRing());
            
            //Add any interior polygons contained within our cut
            for (int iRing=0; iRing < originPolygon.InteriorRings.Count; iRing++)
            {
                //We should be safe quickly testing a single point of each interior polygon because we test that the cut intersects the same ring only
                if (output.Contains(originPolygon.InteriorRings[iRing].First()))
                    output.AddInteriorRing(originPolygon.InteriorPolygons[iRing]);
            }

            if(output.IsValid() == false)
            {
                throw new ArgumentException("Invalid polygon created by cut. (Does the cutting line have loops?)");
            }
            return output;
        }

        /*
        /// <summary>
        /// Add a polygon vertex, added by splitting the nearest ring segment into two parts.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="point"></param>
        /// <param name="updated_poly"></param>
        /// <param name="iVerex"></param>
        /// <returns></returns>
        public static PointIndex AddPointToPolygon(GridPolygon original_polygon, GridVector2 point, out GridPolygon updated_poly)
        {
            original_polygon.NearestSegment(point, out updated_poly);

            //Find the Origin of the path's intersection point and add it too the Exterior Points
            List<GridLineSegment> ExteriorSegments = updated_poly.ExteriorSegments.ToList();
            int iInsertionPoint = ExteriorSegments.NearestSegment(point, out double MinDistance);
            GridLineSegment A_To_B = ExteriorSegments[iInsertionPoint];

            //Find out which verticies the endpoints are
            original_polygon.NearestVertex(A_To_B.A, out PointIndex AIndex);
            original_polygon.NearestVertex(A_To_B.B, out PointIndex BIndex);

            ExteriorSegments.RemoveAt(iInsertionPoint);
            GridLineSegment A_To_Origin = new GridLineSegment(A_To_B.A, point);
            GridLineSegment Origin_To_B = new GridLineSegment(point, A_To_B.B);
            ExteriorSegments.InsertRange(iInsertionPoint, new GridLineSegment[] { A_To_Origin, Origin_To_B });

            GridPolygon poly_with_origin = new GridPolygon(ExteriorSegments.Select(l => l.A).ToArray().EnsureClosedRing());

            if (AIndex.IsInner == false)
            {
                updated_poly = poly_with_origin;
            }
            else
            {
                updated_poly = (GridPolygon)original_polygon.Clone();
                updated_poly.ReplaceInteriorRing(AIndex.iInnerPoly.Value, poly_with_origin);
            }

            updated_poly.NearestVertex(point, out PointIndex origin_index);
            return origin_index;
        }
        */
    }
}
