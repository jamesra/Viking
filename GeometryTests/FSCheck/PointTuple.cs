using Geometry;
using System;
using System.Collections.Generic;

namespace GeometryTests.FSCheck
{
    internal class PointTuple(GridVector2 item1, int item2) : Tuple<GridVector2, int>(item1, item2), IEquatable<PointTuple>
    {
        public GridVector2 Point => this.Item1;
        public int Value => this.Item2;

        public bool Equals(PointTuple other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return other.Point.Equals(this.Point) && other.Value.Equals(this.Value);
        }

        public static implicit operator GridVector2(PointTuple t) => t.Point;

        public override string ToString() => $"{Point} : {Value}";
    }

    internal class PointTupleComparer(AXIS axis) : IComparer<PointTuple>
    {
        public AXIS Axis = axis;

        private readonly IComparer<GridVector2> Comparer = axis == AXIS.Y ? new GridVectorComparerYX() : new GridVectorComparerXY();

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
