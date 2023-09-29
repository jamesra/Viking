using Geometry;
using System;
using System.Collections.Generic;

namespace GeometryTests.FSCheck
{
    internal class PointTuple : Tuple<GridVector2, int>, IEquatable<PointTuple>
    {
        public PointTuple(GridVector2 item1, int item2) : base(item1, item2)
        {
        }

        public GridVector2 Point => this.Item1;
        public int Value => this.Item2;

        public bool Equals(PointTuple other)
        {
            if (ReferenceEquals(this, other))
                return true;

            return other.Point.Equals(this.Point) && other.Value.Equals(this.Value);
        }

        public static implicit operator GridVector2(PointTuple t) => t.Point;

        public override string ToString()
        {
            return $"{Point} : {Value}";
        }
    }

    internal class PointTupleComparer : IComparer<PointTuple>
    {
        public AXIS Axis = AXIS.X;

        private readonly IComparer<GridVector2> Comparer;

        public PointTupleComparer(AXIS axis)
        {
            Axis = axis;

            if (axis == AXIS.Y)
                Comparer = new GridVectorComparerYX();
            else
                Comparer = new GridVectorComparerXY();
        }

        public int Compare(PointTuple x, PointTuple y)
        {
            if (ReferenceEquals(x,y))
                return 0;

            if (x == null)
                return -1;
            if (y == null)
                return 1;

            return Comparer.Compare(x.Point, y.Point);
        }
    }
}
