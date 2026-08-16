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
        public readonly int ShapeIndex;

        /// <summary>
        /// The index of the vertex
        /// </summary>
        public readonly int VertexIndex;

        public readonly int NumUnique; //The total number of verticies in the polyline

        int IShapeIndex.ShapeIndex => ShapeIndex;

        public ShapeType2D ShapeType => ShapeType2D.Polyline;

        public int? InnerShapeIndex => null;

        int IShapeIndex.NumUnique => NumUnique;

        public bool IsInner => false;

        public IShapeIndex FirstVertexInShape => new PolylineIndex(ShapeIndex, 0, NumUnique);

        public IShapeIndex LastVertexInShape => new PolylineIndex(ShapeIndex, NumUnique - 1, NumUnique);

        IShapeIndex IShapeIndex.Next => VertexIndex + 1 < NumUnique ? (IShapeIndex)(new PolylineIndex(ShapeIndex, VertexIndex + 1, NumUnique)) : null;

        IShapeIndex IShapeIndex.Previous => VertexIndex - 1 >= 0 ? (IShapeIndex)(new PolylineIndex(ShapeIndex, VertexIndex - 1, NumUnique)) : null;
        public int? NextVertex => VertexIndex + 1 < NumUnique ? (int?)(VertexIndex + 1) : null;

        public int? PreviousVertex => VertexIndex - 1 >= 0 ? (int?)(VertexIndex - 1) : null;

        IShapeIndex IShapeIndex.Reindex(int shapeIndex) => Reindex(shapeIndex);

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
                    return new PolylineIndex(this.ShapeIndex, n.Value, this.NumUnique);
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
                    return new PolylineIndex(this.ShapeIndex, p.Value, this.NumUnique);
                else
                    return default;
            }
        }


        int IShapeIndex.VertexIndex => VertexIndex;

        public PolylineIndex(int iV, int lineLength)
        {
            ShapeIndex = 0; //Not used in this constructor
            this.VertexIndex = iV;
            this.NumUnique = lineLength;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolylineIndex(int line, int iV, int lineLength)
        {
            ShapeIndex = line;
            this.VertexIndex = iV;
            this.NumUnique = lineLength;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public PolylineIndex(int line, int iV, IReadOnlyList<Polyline> Lines)
        {
            ShapeIndex = line;
            this.VertexIndex = iV;
            this.NumUnique = Lines[ShapeIndex].PointCount;
            Debug.Assert(NumUnique > 0, "Must have at least 1 element in a ring");
            Debug.Assert(VertexIndex < NumUnique); //Can be equal if this is the index of the last point in the ring which is a duplicate
        }

        public object Clone() => new PolylineIndex(this.ShapeIndex, this.VertexIndex, this.NumUnique);

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
            if (other.ShapeIndex != this.ShapeIndex)
            {
                return false;
            }

            if (other.VertexIndex != this.VertexIndex)
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

            if (other.ShapeIndex != this.ShapeIndex)
            {
                return false;
            }

            if (other.VertexIndex != this.VertexIndex)
            {
                return false;
            }

            if (other.NumUnique != this.NumUnique)
                return false;

            return true;
        }

        public int CompareTo(IShapeIndex other)
        {
            if (other.ShapeType != ShapeType2D.Polygon)
                return other.ShapeType.CompareTo(ShapeType2D.Polygon);

            if (this.ShapeIndex != other.ShapeIndex)
                return this.ShapeIndex.CompareTo(other.ShapeIndex);

            return this.VertexIndex.CompareTo(other.VertexIndex);
        }

        public int CompareTo(PolylineIndex other)
        {
            if (this.ShapeIndex != other.ShapeIndex)
                return this.ShapeIndex.CompareTo(other.ShapeIndex);

            return this.VertexIndex.CompareTo(other.VertexIndex);
        }

        public static bool operator ==(PolylineIndex A, PolylineIndex B)
        {
            if (A.ShapeIndex != B.ShapeIndex)
            {
                return false;
            }

            if (A.VertexIndex != B.VertexIndex)
            {
                return false;
            }

            return A.NumUnique == B.NumUnique;
        }

        public static bool operator !=(PolylineIndex A, IShapeIndex B) => !(A == B);

        public static bool operator ==(PolylineIndex A, IShapeIndex B)
        {
            if (A.ShapeType != B.ShapeType)
                return false;

            if (A.ShapeIndex != B.ShapeIndex)
                return false;

            if (A.VertexIndex != B.VertexIndex)
                return false;

            return A.NumUnique == B.NumUnique;
        }

        public static bool operator !=(PolylineIndex A, PolylineIndex B) => !(A == B);

        // override object.GetHashCode
        public override int GetHashCode() => this.VertexIndex + (this.ShapeIndex << 16);


        /// <summary>
        /// Return true if the index is adjacent to the other index
        /// </summary>
        /// <param name="other"></param>
        /// <param name="Polygons"></param>
        /// <returns></returns>
        public bool AreAdjacent(PolylineIndex other)
        {
            if (this.ShapeIndex != other.ShapeIndex)
                return false;

            if (this.VertexIndex == other.VertexIndex)
                return false;

            if (Math.Abs(this.VertexIndex - other.VertexIndex) == 1)
            {
                return true;
            }

            return false;
        }

        public bool IsFirstIndex => this.VertexIndex == 0;

        public bool IsLastIndex => this.VertexIndex == this.NumUnique - 1;

        /// <summary>
        /// Return the specified point, ignoring the ShapeIndex attribute
        /// </summary>
        /// <param name="Polygon"></param>
        /// <returns></returns>
        public Vector2 Point(in Polyline line) => new Vector2(line.Points[VertexIndex]);

        public Vector2 Point(in IReadOnlyList<Polyline> lines) => new Vector2(lines[ShapeIndex].Points[VertexIndex]);


        public Vector2 Point(in IReadOnlyDictionary<int, Polyline> shapes)
        {
            if (shapes.TryGetValue(ShapeIndex, out var line))
                return line.Points[VertexIndex].ToVector2();

            throw new ArgumentException("Index of shape not in dictionary");
        }

        public Vector2 Point(in IShape2D shape)
        {
            if (shape is Polyline line)
            {
                return Point(line);
            }

            throw new ArgumentException("Shape must be a Polygon to use this method");
        }

        public Vector2 Point(in IReadOnlyList<IShape2D> shapes)
        {
            if (shapes[ShapeIndex] is Polyline line)
            {
                return Point(line);
            }

            throw new ArgumentException("Shape must be a Polygon to use this method");
        }

        public Vector2 Point(in IReadOnlyDictionary<int, IShape2D> shapes)
        {
            if (shapes.TryGetValue(ShapeIndex, out var shape))
            {
                if (shape is Polyline line)
                {
                    return Point(line).ToVector2();
                }
            }
            else { throw new ArgumentException("Index of shape not in dictionary"); }

            throw new ArgumentException("Shape must be a Polygon to use this method");
        }

        /// <summary>
        /// Return a copy of this PointIndex with ShapeIndex value changed to point at a different polygon index
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex Reindex(int shapeIndex) => new PolylineIndex(shapeIndex, this.VertexIndex, this.NumUnique);

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(int numUnique) => new PolylineIndex(this.ShapeIndex, this.VertexIndex, numUnique);

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(Polyline line) => ReindexToSize(line.PointCount);

        /// <summary>
        /// Return a copy of this PointIndex with a different size of ring
        /// This is used if the polygon we reference may have changed ring size but we know our index is still correct
        /// </summary>
        /// <param name="old"></param>
        /// <returns></returns>
        public PolylineIndex ReindexToSize(IReadOnlyList<Polyline> lines) => new PolylineIndex(this.ShapeIndex, this.VertexIndex, lines[ShapeIndex].PointCount);

        public override string ToString() => $"L:{this.ShapeIndex} iVert:{this.VertexIndex} of {this.NumUnique}";

        public Vector2 GetOrientation(in IReadOnlyList<IShape2D> Shapes)
        {
            if (Shapes[ShapeIndex] is Polyline line)
            {
                return GetOrientation(line);
            }

            throw new ArgumentException("Shape must be a Polyline to use this method");
        }

        public Vector2 GetOrientation(Polyline polyline)
        {
            var p1 = this.Point(polyline);
            var prev = this.Previous;
            var next = this.Next;

            if (prev.HasValue && next.HasValue)
            {
                LineSegment ALine = new(prev.Value.Point(polyline), p1);
                LineSegment BLine = new(p1, next.Value.Point(polyline));
                var normal = ALine.Normal + BLine.Normal;
                return Vector2.Normalize(normal);
            }
            else if (prev is null && next.HasValue)
            {
                LineSegment line = new(p1, next.Value.Point(polyline));
                return line.Normal;
            }
            else if (next is null && prev.HasValue)
            {
                LineSegment line = new(prev.Value.Point(polyline), p1);
                return line.Normal;
            }

            throw new ArgumentException("Only one point on polyline.  Unhandled case.");
        }
    }
}
