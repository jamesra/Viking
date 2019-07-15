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
    /// <summary>
    /// Records the index of a vertex in a polygon
    /// </summary>
    [Serializable()]
    public struct PointIndex : IComparable<PointIndex>
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
            this.NumUniqueInRing = Polygons[poly].ExteriorRing.Length-1;
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
            this.NumUniqueInRing = this.GetRing(Polygons).Length-1;
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

            if(other.iPoly != this.iPoly)
            {
                return false;
            }

            if(other.iVertex != this.iVertex)
            {
                return false; 
            } 

            if(other.iInnerPoly != this.iInnerPoly)
            {
                return false;
            }

            if (other.NumUniqueInRing != this.NumUniqueInRing)
                return false;

            return true; 
        }

        public static bool  operator ==(PointIndex A, PointIndex B)
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
            return this.iVertex;
        }

        public int CompareTo(PointIndex other)
        {
            if (this.iPoly != other.iPoly)
                return this.iPoly.CompareTo(other.iPoly);

            if (this.iInnerPoly != other.iInnerPoly)
            {
                if(this.iInnerPoly.HasValue && other.iInnerPoly.HasValue)
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
            if(IsInner)
            {
                return Polygons[iPoly].InteriorPolygons[this.iInnerPoly.Value].ExteriorRing[iVertex];
            }
            else
            {
                return Polygons[iPoly].ExteriorRing[iVertex];
            }
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

            if(this.iVertex == other.iVertex)
                return false;

            if(Math.Abs(this.iVertex - other.iVertex) == 1)
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
        public GridVector2[] ConnectedVerticies(IReadOnlyList<GridPolygon> polygons)
        {
            GridVector2[] ring = GetRing(polygons);

            int iPrevious = PreviousVertexInRing();
            int iNext = NextVertexInRing();

            //Should I reverse the order for interior polygons?
            return new GridVector2[] { ring[iPrevious], ring[iNext] };
        }

        public GridLineSegment[] ConnectedSegments(IReadOnlyList<GridPolygon> polygons)
        {
            GridVector2[] ring = GetRing(polygons);

            int iPrevious = PreviousVertexInRing();
            int iNext = PreviousVertexInRing();

            //Should I reverse the order for interior polygons?
            return new GridLineSegment[] {
                new GridLineSegment(ring[iPrevious], ring[iVertex]),
                new GridLineSegment(ring[iVertex], ring[iNext]) };
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
            if(iNext >= this.NumUniqueInRing)
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

        internal GridVector2[] GetRing(GridPolygon polygon)
        { 
            if(this.IsInner)
            {
                return polygon.InteriorPolygons[this.iInnerPoly.Value].ExteriorRing;
            }

            return polygon.ExteriorRing;
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

            if(A.IsInner)
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
            if(IsInner)
                return string.Format("P:{0} I:{1} iVert:{2}", this.iPoly, this.iInnerPoly, this.iVertex);
            else
                return string.Format("P:{0} iVert:{1}", this.iPoly, this.iVertex);
        }

        public static PointIndex[] SortByRing(PointIndex[] verts)
        {
            Array.Sort(verts);
            List<PointIndex> listIndex = new List<PointIndex>(verts.Length);

            foreach(var poly in verts.GroupBy(v => v.iPoly))
            {
                foreach(var ring in poly.GroupBy(v => v.iInnerPoly))
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
    /// A polygon with interior rings representing holes
    /// Rings are described by points.  The first and last point should match
    /// Uses Counter-Clockwise winding order
    /// </summary>
    [Serializable()]
    public class GridPolygon : IShape2D, ICloneable, IPolygon2D
    {
        double _Area;
        GridVector2[] _ExteriorRing;
         
        public GridVector2[] ExteriorRing
        {
            get { return _ExteriorRing; }
            set
            {
                _Area = value.PolygonArea();
                if (_Area < 0) //Negative area indicates Clockwise orientation, we use counter-clockwise
                {
                    _Area = -_Area;
                    _ExteriorRing = value.Reverse().ToArray();
                }
                else
                {
                    _ExteriorRing = value;
                }

                _Centroid = null;
                _BoundingRect = _ExteriorRing.BoundingBox();
                _ExteriorSegments = CreateLineSegments(_ExteriorRing);
                _ExteriorSegmentRTree = null;
            }
        }

        GridRectangle _BoundingRect; 
        GridLineSegment[] _ExteriorSegments;

        RTree.RTree<GridLineSegment> _ExteriorSegmentRTree = null;

        internal RTree.RTree<GridLineSegment> ExteriorSegmentRTree
        {
            get
            {
                if(_ExteriorSegmentRTree == null)
                {
                    _ExteriorSegmentRTree = CreateSegmentBoundingBoxRTree(_ExteriorSegments);
                }

                return _ExteriorSegmentRTree;
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
                return ExteriorSegmentRTree.Intersects(segment.BoundingBox.ToRTreeRect(0)).Contains(segment);
            }
        }

        /// <summary>
        /// Test if a line segment is one of the polygons exterior or interior segments
        /// </summary>
        /// <param name="segment"></param>
        /// <returns></returns>
        public bool IsExteriorOrInteriorSegment(GridLineSegment segment)
        {
            if (IsExteriorSegment(segment))
                return true;

            return this.InteriorPolygons.Any(p => p.IsExteriorSegment(segment));
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
        public ICollection<GridVector2[]> InteriorRings
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

        public GridPolygon(ICollection<IPoint2D> exteriorRing) : this (exteriorRing.Select(p => p.Convert()).ToArray())
        {}

        public GridPolygon(ICollection<GridVector2> exteriorRing) : this(exteriorRing.ToArray())
        {}

        public GridPolygon(GridVector2[] exteriorRing)
        {
            if(!exteriorRing.IsValidClosedRing())
            {
                throw new ArgumentException("Exterior polygon ring must be valid");
            }

            if(exteriorRing.AreClockwise())
            {
                exteriorRing = exteriorRing.Reverse().ToArray();
            }

            ExteriorRing = exteriorRing;
        }


        public GridPolygon(ICollection<IPoint2D> exteriorRing, ICollection<IPoint2D[]> interiorRings) 
            : this(exteriorRing.Select(p => p.Convert()).ToArray(), 
                   interiorRings.Select(inner_ring => inner_ring.Select(p => p.Convert() ).ToArray()).ToArray())
        { 
        }

        public GridPolygon(GridVector2[] exteriorRing, ICollection<GridVector2[]> interiorRings)
        {
            ExteriorRing = exteriorRing;

            foreach(GridVector2[] interiorRing in interiorRings)
            {
                AddInteriorRing(interiorRing);
            }
        }

        public double Area
        {
            get
            {
                double area = _Area;
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
        /// Total verticies, minus the duplicate verticies at the end of each ring
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
                return TotalVerticies - (1 + InteriorRings.Count);
            }
        }

        IPoint2D IPolygon2D.Centroid
        {
            get
            {
                return this.Centroid;
            }
        }

        public void AddInteriorRing(GridVector2[] interiorRing)
        {
            GridPolygon innerPoly = new Geometry.GridPolygon(interiorRing);

            //TODO: Make sure the inner poly does not intersect the outer ring or any existing inner ring
            AddInteriorRing(innerPoly);
        }

        public void AddInteriorRing(GridPolygon innerPoly)
        {
            //TODO: Make sure the inner poly does not intersect the outer ring or any existing inner ring
            
            this._InteriorPolygons.Add(innerPoly);
        }

        /// <summary>
        /// Remove the interior polygon that contains the hole position
        /// </summary>
        /// <param name="holePosition"></param>
        public bool TryRemoveInteriorRing(GridVector2 holePosition)
        {
            for(int iPoly = 0; iPoly < _InteriorPolygons.Count; iPoly++)
            {
                if(_InteriorPolygons[iPoly].Contains(holePosition))
                {
                    _InteriorPolygons.RemoveAt(iPoly);
                    return true;
                }
            }

            return false;
        }

        public void AddVertex(GridVector2 NewControlPointPosition)
        {
            //Find the line segment the NewControlPoint intersects
            double MinDistance;
            int iNearestSegment = this.ExteriorSegments.NearestSegment(NewControlPointPosition, out MinDistance);
            GridLineSegment[] updatedSegments = this.ExteriorSegments.Insert(NewControlPointPosition, iNearestSegment);

            this.ExteriorRing = updatedSegments.Verticies(); 
        }

        public void RemoveVertex(GridVector2 RemovedControlPointPosition)
        {
            double MinDistance;
            GridVector2[] OriginalControlPoints = this.ExteriorRing;
            int iNearestPoint = OriginalControlPoints.NearestPoint(RemovedControlPointPosition, out MinDistance);

            RemoveVertex(iNearestPoint);
        }

        public void RemoveVertex(int iVertex)
        {
            //We must have at least 3 points to create a polygon
            if(ExteriorSegments.Length <= 3)
            {
                throw new ArgumentException("Cannot remove vertex.  Polygon's must have three verticies.");
            }

            //Find the line segment the NewControlPoint intersects
            GridLineSegment[] updatedLineSegments = ExteriorSegments.Remove(iVertex);
            
            this.ExteriorRing = updatedLineSegments.Verticies();
        }

        public bool Contains(IPoint2D point_param)
        {
            if (!_BoundingRect.Contains(point_param))
                return false;

            GridVector2 p = new GridVector2(point_param.X, point_param.Y);
            //Create a line we know must pass outside the polygon
            GridLineSegment test_line = new Geometry.GridLineSegment(p, new GridVector2(p.X + (BoundingBox.Width*2), p.Y));

            List<GridLineSegment> segmentsToTest;
            
            if(_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(test_line.BoundingBox);
            }
            else
            {
                segmentsToTest = _ExteriorSegments.ToList(); 
            }

            //Test all of the line segments for both interior and exterior polygons
            return IsPointInsidePolygon(segmentsToTest, test_line); 
        }

        public bool Contains(GridLineSegment line)
        {
            //Ensure both endpoints are inside and a point in the center.
            //Test the center because if the line crosses a concave region with both endpoints exactly on the exterior ring we'd not have any intersections but the poly would not contain the line.
            if (!(this.Contains(line.A) && this.Contains(line.B) && this.Contains(line.PointAlongLine(0.5))))
                return false;

            List<GridLineSegment> segmentsToTest;

            if (_ExteriorSegments.Length > 32 || HasInteriorRings)
            {
                segmentsToTest = this.GetIntersectingSegments(line.BoundingBox);
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
                if (innerPoly.Contains(line))
                    return false;
            }

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

        

        public GridCircle InscribedCircle()
        {
            GridVector2 center = this.Centroid;
            double Radius = ExteriorRing.Select(p => GridVector2.Distance(center, p)).Min();
            return new GridCircle(center, Radius);
        }

        private static bool IsPointInsidePolygon(ICollection<GridLineSegment> polygonSegments, GridLineSegment test_line)
        {
            //In cases where our test line passes exactly through a vertex on the other polygon we double count the line.  
            //This code removes duplicate intersection points to prevent duplicates

            //If we share endpoints then we are always inside the polygon.  Handles case where we ask if a polygon vertex is inside the polygon
            if (polygonSegments.Any(ps => ps.SharedEndPoint(test_line)))
                return true;

            System.Collections.Concurrent.ConcurrentBag<GridVector2> intersections = new System.Collections.Concurrent.ConcurrentBag<Geometry.GridVector2>();

            IEnumerable<GridLineSegment> IntersectedSegments = polygonSegments.Where(line =>
            {
                GridVector2 Intersection;
                bool intersected = line.Intersects(test_line, out Intersection);
                if (intersected)
                { 
                    intersections.Add(Intersection);
                }

                return intersected;
            }).AsParallel().ToList(); //Need ToList here to ensure the query executes fully
            
            //Ensure the line doesn't pass through on a line endpoint
            SortedSet<GridVector2> intersectionPoints = new SortedSet<GridVector2>();
            intersectionPoints.UnionWith(intersections);

            if (intersectionPoints.Any(p => test_line.IsEndpoint(p)))
                return true; //If the point is exactly on the line then we can often have two intersections as the line leaves the polygon which results in a false negative.
                             //This test short-circuits that problem
            
            //Inside the polygon if we intersect line segments of the border an odd number of times
            return intersectionPoints.Count % 2 == 1; 
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

        /// <summary>
        /// Return all segments, both interior and exterior, that fall within the bounding rectangle
        /// </summary>
        /// <param name="bbox"></param>
        /// <returns></returns>
        public List<GridLineSegment> GetIntersectingSegments(GridRectangle bbox)
        {
            if(!this.BoundingBox.Intersects(bbox))
            {
                return new List<Geometry.GridLineSegment>();
            }

            List<GridLineSegment> intersectingSegments = ExteriorSegmentRTree.Intersects(bbox.ToRTreeRect(0));

            if (!intersectingSegments.Any())
                return new List<Geometry.GridLineSegment>();
            
            foreach(GridPolygon innerPoly in InteriorPolygons)
            {
                intersectingSegments.AddRange(innerPoly.GetIntersectingSegments(bbox));
            }

            return intersectingSegments;
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
        private static bool SegmentsIntersect(GridPolygon poly, GridPolygon other)
        {
            GridRectangle? Intersection = poly.BoundingBox.Intersection(other.BoundingBox);
            if (!Intersection.HasValue)
                return false;

            //Check the case for a line segment passing entirely through the polygon.
            GridRectangle overlap = Intersection.Value;

            List<GridLineSegment> CandidateSegments = poly.GetIntersectingSegments(overlap);

            foreach (GridLineSegment candidate in CandidateSegments)
            {
                List<GridLineSegment> OtherSegments = other.GetIntersectingSegments(candidate.BoundingBox);

                if (candidate.Intersects(OtherSegments))
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
        public void AddPointsAtIntersections(GridPolygon other)
        {
            GridRectangle? overlap = this.BoundingBox.Intersection(other.BoundingBox);

            //No work to do if there is no overlap
            if (!overlap.HasValue)
                return; 

            List<GridVector2> newRing = new List<Geometry.GridVector2>();

            for(int i = 0; i < ExteriorRing.Length-1; i++)
            {
                GridLineSegment ls = new GridLineSegment(ExteriorRing[i], ExteriorRing[i + 1]);

                newRing.Add(ExteriorRing[i]);

                GridVector2[] IntersectionPoints; 
                List<GridLineSegment> candidates = ls.Intersections(other.GetIntersectingSegments(ls.BoundingBox), out IntersectionPoints);
                 
                //Remove any duplicates of the existing endpoints 
                foreach(GridVector2 p in IntersectionPoints)
                {
                    System.Diagnostics.Debug.Assert(!newRing.Contains(p));
                    newRing.Add(p);
                } 
            }

            newRing.Add(ExteriorRing[ExteriorRing.Length - 1]);
            
            //Ensure we are not accidentally adding duplicate points, other than to close the ring
            System.Diagnostics.Debug.Assert(newRing.Count == newRing.Distinct().Count() + 1);

            this.ExteriorRing = newRing.ToArray();

            foreach (GridPolygon otherInnerPolygon in other.InteriorPolygons)
            {
                this.AddPointsAtIntersections(otherInnerPolygon);
            }

            foreach (GridPolygon innerPolygon in this._InteriorPolygons)
            {
                innerPolygon.AddPointsAtIntersections(other);
            } 
        }
         

        /// <summary>
        /// 
        /// </summary>
        /// <param name="poly">Polygon to compare against</param>
        /// <param name="intersections">The coordinates of the intersection</param>
        /// <param name="intersection_indicies">The index before the intersection point</param>
        /*public void Intersect(GridPolygon poly, out GridVector2[] intersections, out PointIndex intersection_indicies)
        {
            
        }
        */

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
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public static Dictionary<GridVector2, List<PointIndex>> CreatePointToPolyMap(GridPolygon[] Polygons)
        {
            Dictionary<GridVector2, List<PointIndex>> pointToPoly = new Dictionary<GridVector2, List<PointIndex>>();
            for (int iPoly = 0; iPoly < Polygons.Length; iPoly++)
            {
                GridPolygon poly = Polygons[iPoly];
                GridVector2[] polyPoints = poly.ExteriorRing;
                for(int iVertex = 0; iVertex < poly.ExteriorRing.Length; iVertex++)
                {
                    GridVector2 p = poly.ExteriorRing[iVertex];
                    PointIndex value = new PointIndex(iPoly, iVertex, Polygons);

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

                    for (int iVertex = 0; iVertex < innerPolygon.ExteriorRing.Length; iVertex++)
                    {
                        GridVector2 p = innerPolygon.ExteriorRing[iVertex];

                        PointIndex value = new PointIndex(iPoly, iInnerPoly, iVertex, Polygons);
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
    }
}
