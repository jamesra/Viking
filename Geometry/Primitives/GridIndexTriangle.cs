using System;

namespace Geometry
{
    /// <summary>
    /// A triangle class that uses indicies into a list of points to record it's node positions.
    /// </summary>
    [Serializable]
    public class GridIndexTriangle
    {
        /// <summary>
        /// THIS IS A REFERENCE.  DO NOT CHANGE ANY VALUES IN THIS ARRAY
        /// </summary>
        readonly Vector2[] points;

        public readonly int i1;
        public readonly int i2;
        public readonly int i3;

        private Circle? _Circle;

        public Circle Circle
        {
            get
            {
                if (_Circle.HasValue == false)
                    _Circle = Circle.CircleFromThreePoints(points[i1], points[i2], points[i3]);

                return _Circle.Value;
            }
        }

        public GridIndexTriangle(int index1, int index2, int index3, ref Vector2[] pointArray)
        {
            i1 = index1;
            i2 = index2;
            i3 = index3;
            this.points = pointArray;
        }

        public int[] Indices() => [i1, i2, i3];

        public static implicit operator Triangle(GridIndexTriangle t)
        {
            if (t is null)
                throw new ArgumentNullException(nameof(t));


            return new Triangle(t.points[t.i1], t.points[t.i2], t.points[t.i3]);
        }

        public Vector2 P1 => points[i1];

        public Vector2 P2 => points[i2];

        public Vector2 P3 => points[i3];
    }
}
