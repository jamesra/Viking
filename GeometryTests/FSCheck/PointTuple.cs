using Geometry;
using System;
using System.Collections.Generic;

namespace GeometryTests.FSCheck
{
    internal class PointTuple(Vector2 item1, int item2) : Tuple<Vector2, int>(item1, item2), IEquatable<PointTuple>
    {
        public Vector2 Point => this.Item1;
        public int Value => this.Item2;

        public bool Equals(PointTuple other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return other.Point.Equals(this.Point) && other.Value.Equals(this.Value);
        }

        public static implicit operator Vector2(PointTuple t) => t.Point;

        public override string ToString() => $"{Point} : {Value}";
    }

    internal class PointTupleComparer(Axis axis) : IComparer<PointTuple>
    {
        public Axis Axis = axis;

        private readonly IComparer<Vector2> Comparer = axis == Axis.Y ? new Vector2ComparerYX() : new Vector2ComparerXY();

        public int Compare(PointTuple x, PointTuple y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x is null)
                return -1;
            if (y is null)
                return 1;

            return Comparer.Compare(x.Point, y.Point);
        }
    }
}
