using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using static Geometry.IShapeIndex;

namespace Geometry
{
    /// <summary>
    /// Records the index of a vertex in a polygon
    /// </summary>
    [Serializable()]
    public readonly struct PolygonIndex : IComparable<PolygonIndex>, IEquatable<PolygonIndex>, ICloneable, IShapeIndex
    {
        /// <summary>
        /// The index of the polygon 
        /// </summary>
        public readonly int ShapeIndex;

        /// <summary>
        /// The index of the inner polygon, or no value if part of the external border
        /// </summary>
        public readonly int? InnerShapeIndex;

        /// <summary>
        /// The index of the vertex
        /// </summary>
        public readonly int VertexIndex;

        public readonly int NumUniqueInRing; //The total number of verticies in the ring VertexIndex indexes into

        int? IShapeIndex.InnerShapeIndex => InnerShapeIndex;
        int IShapeIndex.ShapeIndex => ShapeIndex;
        int IShapeIndex.VertexIndex => VertexIndex;
        int IShapeIndex.NumUnique => NumUniqueInRing;
        IShapeIndex IShapeIndex.Next => Next;
        IShapeIndex IShapeIndex.Previous => Previous;
        IShapeIndex IShapeIndex.FirstVertexInShape => FirstInRing;
        IShapeIndex IShapeIndex.LastVertexInShape => LastInRing;

        IShapeIndex IShapeIndex.Reindex(int shapeIndex) => Reindex(shapeIndex);

        ShapeType2D IShapeIndex.ShapeType => ShapeType2D.Polygon;

        /// <summary>
        /// True if the vertex is part of an inner polygon
        /// </summary>
        public bool IsInner => InnerShapeIndex.HasValue;

        public PolygonIndex(int poly, int iV, int ringLength)
        {
            ShapeIndex = poly;
            InnerShapeIndex = new int?();
            this.VertexIndex = iV;
            this.NumUniqueInRing = ringLength;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex <= NumUniqueInRing); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolygonIndex(int poly, int iV, IReadOnlyList<Polygon> Polygons)
        {
            ShapeIndex = poly;
            InnerShapeIndex = new int?();
            this.VertexIndex = iV;
            this.NumUniqueInRing = Polygons[poly].ExteriorRing.Length - 1;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex <= NumUniqueInRing);
        }

        public PolygonIndex(int poly, int? innerPoly, int iV, int ringLength)
        {
            ShapeIndex = poly;
            InnerShapeIndex = innerPoly;
            this.VertexIndex = iV;
            this.NumUniqueInRing = ringLength;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex <= NumUniqueInRing);
        }

        public PolygonIndex(int poly, int? innerPoly, int iV, IReadOnlyList<Polygon> Polygons)
        {
            ShapeIndex = poly;
            InnerShapeIndex = innerPoly;
            this.VertexIndex = iV;
            this.NumUniqueInRing = 0; //Temp assignment so we can call GetRing
            this.NumUniqueInRing = this.GetRing(Polygons).Length - 1;
            Debug.Assert(NumUniqueInRing > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex <= NumUniqueInRing);
        }




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

            if (obj is IShapeIndex shapeIndex)
                return Equals(shapeIndex);

            return false;
        }

        // override object.Equals
        public bool Equals(IShapeIndex other)
        {
            if (other.ShapeType != ShapeType2D.Polygon)
                return false;

            if (other.ShapeIndex != this.ShapeIndex)
            {
                return false;
            }

            if (other.VertexIndex != this.VertexIndex)
            {
                return false;
            }

            if (other.InnerShapeIndex != this.InnerShapeIndex)
            {
                return false;
            }

            if (other.NumUnique != this.NumUniqueInRing)
                return false;

            return true;
        }

        public bool Equals(PolygonIndex other)
        {
            if (other.ShapeIndex != this.ShapeIndex)
            {
                return false;
            }

            if (other.VertexIndex != this.VertexIndex)
            {
                return false;
            }

            if (other.InnerShapeIndex != this.InnerShapeIndex)
            {
                return false;
            }

            if (other.NumUniqueInRing != this.NumUniqueInRing)
                return false;

            return true;
        }

        public static bool operator ==(PolygonIndex A, PolygonIndex B)
        {
            if (A.ShapeIndex != B.ShapeIndex)
            {
                return false;
            }

            if (A.VertexIndex != B.VertexIndex)
            {
                return false;
            }

            if (A.InnerShapeIndex != B.InnerShapeIndex)
            {
                return false;
            }

            if (A.NumUniqueInRing != B.NumUniqueInRing)
                return false;

            return true;
        }

        public static bool operator !=(PolygonIndex A, PolygonIndex B) => !(A == B);

        public static bool operator ==(PolygonIndex A, IShapeIndex B)
        {
            if (B.ShapeType != ShapeType2D.Polygon)
                return false;

            if (A.ShapeIndex != B.ShapeIndex)
            {
                return false;
            }

            if (A.VertexIndex != B.VertexIndex)
            {
                return false;
            }

            if (A.InnerShapeIndex != B.InnerShapeIndex)
            {
                return false;
            }

            if (A.NumUniqueInRing != B.NumUnique)
                return false;

            return true;
        }

        public static bool operator !=(PolygonIndex A, IShapeIndex B) => !(A == B);

        // override object.GetHashCode
        public override int GetHashCode()
        {
            if (IsInner)
            {
                return this.VertexIndex + (this.ShapeIndex << 16) + (this.InnerShapeIndex.Value << 10);
            }
            else
            {
                return this.VertexIndex + (this.ShapeIndex << 16);
            }
        }

        public int CompareTo(IShapeIndex other)
        {
            if (other.ShapeType != ShapeType2D.Polygon)
                return other.ShapeType.CompareTo(ShapeType2D.Polygon);

            if (this.ShapeIndex != other.ShapeIndex)
                return this.ShapeIndex.CompareTo(other.ShapeIndex);

            if (this.InnerShapeIndex != other.InnerShapeIndex)
            {
                if (this.InnerShapeIndex.HasValue && other.InnerShapeIndex.HasValue)
                {
                    return this.InnerShapeIndex.Value.CompareTo(other.InnerShapeIndex.Value);
                }

                return this.InnerShapeIndex.HasValue ? 1 : -1;
            }

            return this.VertexIndex.CompareTo(other.VertexIndex);
        }

        public int CompareTo(PolygonIndex other)
        {
            if (this.ShapeIndex != other.ShapeIndex)
                return this.ShapeIndex.CompareTo(other.ShapeIndex);

            if (this.InnerShapeIndex != other.InnerShapeIndex)
            {
                if (this.InnerShapeIndex.HasValue && other.InnerShapeIndex.HasValue)
                {
                    return this.InnerShapeIndex.Value.CompareTo(other.InnerShapeIndex.Value);
                }

                return this.InnerShapeIndex.HasValue ? 1 : -1;
            }

            return this.VertexIndex.CompareTo(other.VertexIndex);
        }

        public bool IsFirstIndexInRing() => this.VertexIndex == 0 || VertexIndex == this.NumUniqueInRing; //The latter case should not happen

        public bool IsLastIndexInRing() => this.VertexIndex == this.NumUniqueInRing - 1;

        /// <summary>
        /// Return the specified point, ignoring the ShapeIndex attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public Vector2 Point(in Polygon Polygon)
        {
            if (IsInner)
            {
                return Polygon.InteriorPolygons[this.InnerShapeIndex.Value].RingStorage[VertexIndex];
            }
            else
            {
                return Polygon.RingStorage[VertexIndex];
            }
        }

        /// <summary>
        /// Return the specified point, ignoring the ShapeIndex attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public Vector2 Point(in IShape2D shape)
        {
            if (shape is Polygon poly)
            {
                return Point(poly);
            }

            throw new ArgumentException("Shape must be a Polygon to use this method");
        }

        /// <summary>
        /// Return the point corresponding to this index
        /// </summary>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public Vector2 Point(in IReadOnlyList<Polygon> Polygons)
        {
            if (IsInner)
            {
                return Polygons[ShapeIndex].InteriorPolygons[this.InnerShapeIndex.Value].RingStorage[VertexIndex];
            }
            else
            {
                return Polygons[ShapeIndex].RingStorage[VertexIndex];
            }
        }

        public Vector2 Point(in IReadOnlyDictionary<int, Polygon> Polygons)
        {
            if (IsInner)
            {
                return Polygons[ShapeIndex].InteriorPolygons[this.InnerShapeIndex.Value].RingStorage[VertexIndex];
            }
            else
            {
                return Polygons[ShapeIndex].RingStorage[VertexIndex];
            }
        }

        public Vector2 Point(in IReadOnlyList<IShape2D> shapes)
        {
            if (shapes[ShapeIndex] is Polygon poly)
            {
                return Point(poly);
            }

            throw new ArgumentException("Shape must be a Polygon to use this method");
        }

        public Vector2 Point(in IReadOnlyDictionary<int, IShape2D> shapes)
        {
            if (shapes.TryGetValue(ShapeIndex, out var shape))
            {
                if (shape is Polygon poly)
                {
                    return Point(poly);
                }
            }
            else { throw new ArgumentException("Index of shape not in dictionary"); }

            throw new ArgumentException("Shape must be a Polygon to use this method");
        }

        /// <summary>
        /// Return the specified point, ignoring the ShapeIndex attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public void SetPoint(Polygon Polygon, Vector2 value) => Polygon[this] = value;

        public void SetPoint(IReadOnlyList<Polygon> Polygons, Vector2 value) => Polygons[ShapeIndex][this] = value;

        public void SetPoint(IReadOnlyDictionary<int, Polygon> Polygons, Vector2 value) => Polygons[ShapeIndex][this] = value;


        /// <summary>
        /// Return the segment, using this point index and the next index in the ring
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public LineSegment Segment(Polygon Polygon) => new LineSegment(Point(Polygon), Next.Point(Polygon));

        public LineSegment Segment(IReadOnlyList<Polygon> Polygons) => new LineSegment(Point(Polygons), Next.Point(Polygons));

        public LineSegment Segment(IReadOnlyDictionary<int, Polygon> Polygons) => new LineSegment(Point(Polygons), Next.Point(Polygons));

        /// <summary>
        /// Returns the polygon the index refers to
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public Polygon Polygon(Polygon poly) => this.IsInner ? poly.InteriorPolygons[InnerShapeIndex.Value] : poly;

        /// <summary>
        /// Returns the polygon the index refers to
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public Polygon Polygon(IReadOnlyList<Polygon> polygons)
        {
            Polygon poly = polygons[this.ShapeIndex];
            return Polygon(poly);
        }

        /// <summary>
        /// Returns the polygon the index refers to
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public Polygon Polygon(IReadOnlyDictionary<int, Polygon> polygons)
        {
            Polygon poly = polygons[this.ShapeIndex];
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
            if (this.ShapeIndex != other.ShapeIndex)
                return false;

            if (this.InnerShapeIndex != other.InnerShapeIndex)
                return false;

            if (this.VertexIndex == other.VertexIndex)
                return false;

            if (Math.Abs(this.VertexIndex - other.VertexIndex) == 1)
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
        private Vector2[] ConnectedVertices(Vector2[] ring)
        {
            int iPrevious = PreviousVertexInRing();
            int iNext = NextVertexInRing();

            //Should I reverse the order for interior polygons?
            return [ring[iPrevious], ring[iNext]];
        }

        /// <summary>
        /// Returns the verticies before and after this index
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        public Vector2[] ConnectedVertices(IReadOnlyList<Polygon> polygons) => ConnectedVertices(GetRing(polygons));

        /// <summary>
        /// Returns the verticies before and after this index
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        public Vector2[] ConnectedVertices(IReadOnlyList<IShape2D> polygons) => ConnectedVertices(GetRing(polygons));

        /// <summary>
        /// Returns the verticies before and after this index
        /// </summary>
        /// <param name="polygons"></param>
        /// <returns></returns>
        public Vector2[] ConnectedVertices(IReadOnlyDictionary<int, Polygon> polygons) => ConnectedVertices(GetRing(polygons));

        public LineSegment[] ConnectedSegments(Vector2[] ring)
        {
            int iPrevious = PreviousVertexInRing();
            int iNext = PreviousVertexInRing();

            //Should I reverse the order for interior polygons?
            return [
                new(ring[iPrevious], ring[VertexIndex]),
                new(ring[VertexIndex], ring[iNext]) ];
        }

        public LineSegment[] ConnectedSegments(IReadOnlyList<Polygon> polygons)
        {
            Vector2[] ring = GetRing(polygons);
            return ConnectedSegments(ring);
        }

        public LineSegment[] ConnectedSegments(IReadOnlyDictionary<int, Polygon> polygons)
        {
            Vector2[] ring = GetRing(polygons);
            return ConnectedSegments(ring);
        }

        /// <summary>
        /// Get the normal of the vertex at this index, do not weight according to the relative length of the connected segments
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public Vector2 GetOrientation(in Polygon poly)
        {
            Vector2[] adjacent = this.ConnectedVertices(GetRing(poly));
            LineSegment line = new(adjacent[0], adjacent[1]);
            return line.Normal;
        }

        /// <summary>
        /// Get the normal of the vertex at this index, do not weight according to the relative length of the connected segments
        /// </summary>
        /// <param name="poly"></param>
        /// <returns></returns>
        public Vector2 GetOrientation(in IReadOnlyList<IShape2D> Shapes)
        {
            Vector2[] adjacent = this.ConnectedVertices(GetRing(Shapes));
            LineSegment line = new(adjacent[0], adjacent[1]);
            return line.Normal;
        }

        /// <summary>
        /// Returns the index of the beginning of the current ring
        /// </summary>
        public PolygonIndex FirstInRing => new(this.ShapeIndex, this.InnerShapeIndex, 0, this.NumUniqueInRing);

        /// <summary>
        /// Returns the index of the end of the current ring
        /// </summary>
        public PolygonIndex LastInRing => new(this.ShapeIndex, this.InnerShapeIndex, this.NumUniqueInRing - 1, this.NumUniqueInRing);

        /// <summary>
        /// Return the next index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolygonIndex Next => new(this.ShapeIndex, this.InnerShapeIndex, this.NextVertexInRing(), this.NumUniqueInRing);

        /// <summary>
        /// Return the previous index after this one, staying within the same ring
        /// </summary>
        /// <returns></returns>
        public PolygonIndex Previous => new(this.ShapeIndex, this.InnerShapeIndex, this.PreviousVertexInRing(), this.NumUniqueInRing);

        private int NextVertexInRing()
        {
            int iNext = VertexIndex + 1;
            if (iNext >= this.NumUniqueInRing)
            {
                return 0;
            }

            return iNext;
        }

        private int PreviousVertexInRing()
        {
            int iPrevious = VertexIndex - 1;
            if (iPrevious < 0)
            {
                return this.NumUniqueInRing - 1;
            }

            return iPrevious;
        }

        internal Vector2[] GetRing(IReadOnlyList<IShape2D> Shapes)
        {
            if (Shapes[ShapeIndex] is Polygon poly)
                return this.GetRing(poly);

            throw new ArgumentException("Shape must be a grid polygon.");
        }

        internal Vector2[] GetRing(IReadOnlyList<Polygon> Polygons) => this.GetRing(Polygons[this.ShapeIndex]);

        internal Vector2[] GetRing(IReadOnlyDictionary<int, Polygon> Polygons) => this.GetRing(Polygons[this.ShapeIndex]);

        internal Vector2[] GetRing(Polygon polygon)
        {
            if (this.IsInner)
            {
                return polygon.InteriorPolygons[this.InnerShapeIndex.Value].RingStorage;
            }

            return polygon.RingStorage;
        }

        public bool AreOnSameRing(PolygonIndex B)
        {
            if (this.ShapeIndex != B.ShapeIndex)
                return false;

            if (this.IsInner != B.IsInner)
                return false;

            if (this.IsInner && B.IsInner)
            {
                return this.InnerShapeIndex.Value == B.InnerShapeIndex.Value;
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
        public static bool IsBorderLine(PolygonIndex A, PolygonIndex B, Polygon poly) => A.AreAdjacent(B);/*
            //TODO: Add unit test
            System.Diagnostics.Debug.Assert(A.ShapeIndex == B.ShapeIndex, "LineIsOnBorder should only called for indicies into the same polygon");
            if (A.ShapeIndex != B.ShapeIndex)
                throw new ArgumentException("LineIsOnBorder should only called for indicies into the same polygon");

            //Points must be both inside or outside border.
            if (A.IsInner ^ B.IsInner)
            {
                return false;
            }

            if (A.IsInner)
            {
                //Check that the indicies are to the same interior polygon
                if (A.InnerShapeIndex.Value != B.InnerShapeIndex.Value)
                {
                    return false;
                }
            }

            //Simple case of adjacent vertex indicies
            int diff = Math.Abs(A.VertexIndex - B.VertexIndex);
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
                RingLength = poly.InteriorRings.ElementAt(A.InnerShapeIndex.Value).Length;
            }

            //Must have points at the wraparound point to be adjacent
            if (A.VertexIndex > 0 && A.VertexIndex < RingLength - 2)
                return false;
            if (B.VertexIndex > 0 && B.VertexIndex < RingLength - 2)
                return false;

            return diff == RingLength - 2;
            */

        public override string ToString()
        {
            if (IsInner)
                return $"P:{this.ShapeIndex} I:{this.InnerShapeIndex} iVert:{this.VertexIndex} of {this.NumUniqueInRing}";
            else
                return $"P:{this.ShapeIndex} iVert:{this.VertexIndex} of {this.NumUniqueInRing}";
        }

        public static PolygonIndex[] SortByRing(PolygonIndex[] verts)
        {
            Array.Sort(verts);
            List<PolygonIndex> listIndex = new(verts.Length);

            foreach (var poly in verts.GroupBy(v => v.ShapeIndex))
            {
                foreach (var ring in poly.GroupBy(v => v.InnerShapeIndex))
                {
                    PolygonIndex[] ringArray = [.. ring];
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

            return [.. listIndex];
        }

        /// <summary>
        /// Return a copy of this PointIndex with ShapeIndex value changed to point at a different polygon index
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex Reindex(int shapeIndex) => new PolygonIndex(shapeIndex, this.InnerShapeIndex, this.VertexIndex, this.NumUniqueInRing);

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex ReindexToSize(int numUniqueInRing) => new PolygonIndex(this.ShapeIndex, this.InnerShapeIndex, this.VertexIndex, numUniqueInRing);

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex ReindexToSize(Polygon poly) => this.ReindexToSize(this.Polygon(poly).ExteriorRing.Length - 1);

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolygonIndex ReindexToSize(IReadOnlyList<Polygon> Polygons) =>
            //return this.ReindexToSize(this.Polygon(Polygons).ExteriorRing.Length - 1);
            new PolygonIndex(this.ShapeIndex, this.InnerShapeIndex, this.VertexIndex, this.Polygon(Polygons).ExteriorRing.Length - 1);

        /// <summary>
        /// Return a copy of this PointIndex that refers to the inner polygon index as an exterior polygon coordinate
        /// </summary>
        /// <param name="ShapeIndex">Passing -1 will use the innerPolygon's index as the new ShapeIndex value.  Useful for referencing into arrays of interior polygons from a parent polygon.</param>
        /// <returns></returns>
        public PolygonIndex ReindexToOuter(int ShapeIndex = 0)
        {
            if (this.IsInner == false)
            {
                throw new ArgumentException("Trying to ReindexToOuter using a non-interior polygon's PointIndex");
            }

            if (ShapeIndex == -1)
            {
                ShapeIndex = this.InnerShapeIndex.Value;
            }

            return new PolygonIndex(ShapeIndex, this.VertexIndex, this.NumUniqueInRing);
        }

        /// <summary>
        /// Return a copy of this PointIndex that refers to the inner polygon index as an exterior polygon coordinate
        /// </summary>
        /// <param name="ShapeIndex">Passing -1 will use the innerPolygon's index as the new ShapeIndex value.  Useful for referencing into arrays of interior polygons from a parent polygon.</param>
        /// <returns></returns>
        public PolygonIndex ReindexToInner(int iInner, int ShapeIndex = 0)
        {
            if (this.IsInner == true)
            {
                throw new ArgumentException("Trying to ReindexToInner using an interior polygon's PointIndex");
            }

            return new PolygonIndex(ShapeIndex, iInner, this.VertexIndex, this.NumUniqueInRing);
        }

        public object Clone() => new PolygonIndex(ShapeIndex, InnerShapeIndex, VertexIndex, NumUniqueInRing);
    }
}
