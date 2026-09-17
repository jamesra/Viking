using System;

namespace Geometry
{
    /// <summary>
    /// Hash helpers for epsilon-based geometry equality. Coordinates are rounded to
    /// <see cref="Tolerance.SignificantDigits"/> so points equal within epsilon share a hash code
    /// except at rounding-bin boundaries.
    /// </summary>
    internal static class GeometryHashCode
    {
        internal static int QuantizedCoord(double value) =>
            (int)Math.Round(value * Math.Pow(10, Tolerance.SignificantDigits));

        internal static int Point2D(double x, double y)
        {
            unchecked
            {
                int hx = QuantizedCoord(x);
                int hy = QuantizedCoord(y);
                return (hx * 397) ^ hy;
            }
        }

        internal static int Point2D(in Vector2 point) => Point2D(point.X, point.Y);

        internal static int Point3D(double x, double y, double z)
        {
            unchecked
            {
                return Combine(QuantizedCoord(x), QuantizedCoord(y), QuantizedCoord(z));
            }
        }

        internal static int PointN(double[] coords)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < coords.Length; i++)
                {
                    hash = (hash * 397) ^ QuantizedCoord(coords[i]);
                }

                return hash;
            }
        }

        internal static int LineSegmentDirected(in Vector2 a, in Vector2 b) => Combine(Point2D(a), Point2D(b));

        internal static int LineSegmentUndirected(in Vector2 a, in Vector2 b)
        {
            int ha = Point2D(a);
            int hb = Point2D(b);
            return ha <= hb ? Combine(ha, hb) : Combine(hb, ha);
        }

        internal static int Combine(int h1, int h2)
        {
            unchecked
            {
                return (h1 * 397) ^ h2;
            }
        }

        internal static int Combine(int h1, int h2, int h3) => Combine(Combine(h1, h2), h3);
    }
}
