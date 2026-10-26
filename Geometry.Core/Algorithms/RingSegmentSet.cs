using System;
using System.Collections.Generic;

namespace Geometry
{
    /// <summary>
    /// The segments of a polygon's exterior ring and holes in flat arrays, optionally rotated into a local frame, for the
    /// distance and inside tests that label placement evaluates thousands of times per polygon.
    /// Inside tests use the even-odd rule over every ring, so points in a hole are outside.
    /// </summary>
    internal sealed class RingSegmentSet
    {
        private readonly double[] AX;
        private readonly double[] AY;
        private readonly double[] BX;
        private readonly double[] BY;

        public int Count => AX.Length;

        /// <summary>Bounds of the segments in this set's frame.</summary>
        public readonly double MinX, MinY, MaxX, MaxY;

        /// <summary>
        /// Segments of <paramref name="polygon"/> mapped into a frame rotated by −<paramref name="angle"/> about
        /// <paramref name="origin"/>, so a direction at <paramref name="angle"/> in world space is +X here.
        /// </summary>
        public RingSegmentSet(Polygon polygon, Vector2 origin, double angle)
        {
            List<Vector2[]> rings = [polygon.ExteriorRing];
            foreach (Polygon hole in polygon.InteriorPolygons)
                rings.Add(hole.ExteriorRing);

            int count = 0;
            foreach (Vector2[] ring in rings)
                count += Math.Max(ring.Length - 1, 0);

            AX = new double[count];
            AY = new double[count];
            BX = new double[count];
            BY = new double[count];

            double cos = Math.Cos(-angle);
            double sin = Math.Sin(-angle);
            MinX = MinY = double.MaxValue;
            MaxX = MaxY = double.MinValue;

            int i = 0;
            foreach (Vector2[] ring in rings)
            {
                for (int v = 0; v < ring.Length - 1; v++, i++)
                {
                    Rotate(ring[v], origin, cos, sin, out AX[i], out AY[i]);
                    Rotate(ring[v + 1], origin, cos, sin, out BX[i], out BY[i]);
                    MinX = Math.Min(MinX, AX[i]);
                    MaxX = Math.Max(MaxX, AX[i]);
                    MinY = Math.Min(MinY, AY[i]);
                    MaxY = Math.Max(MaxY, AY[i]);
                }
            }
        }

        public RingSegmentSet(Polygon polygon) : this(polygon, Vector2.Zero, 0)
        {
        }

        private static void Rotate(Vector2 p, Vector2 origin, double cos, double sin, out double x, out double y)
        {
            double dx = p.X - origin.X;
            double dy = p.Y - origin.Y;
            x = (dx * cos) - (dy * sin);
            y = (dx * sin) + (dy * cos);
        }

        /// <summary>Even-odd inside test with the half-open rule.</summary>
        public bool Contains(double x, double y)
        {
            bool inside = false;
            for (int i = 0; i < AX.Length; i++)
            {
                double ay = AY[i], by = BY[i];
                if ((ay > y) != (by > y))
                {
                    double xCross = AX[i] + ((y - ay) / (by - ay) * (BX[i] - AX[i]));
                    if (x < xCross)
                        inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Distance from the point to the nearest segment of any ring.</summary>
        public double DistanceToBoundary(double x, double y)
        {
            double best = double.MaxValue;
            for (int i = 0; i < AX.Length; i++)
            {
                double d = SegmentDistanceSquared(x, y, AX[i], AY[i], BX[i], BY[i]);
                if (d < best)
                    best = d;
            }

            return Math.Sqrt(best);
        }

        /// <summary>Distance to the boundary, negative outside the polygon (or inside a hole).</summary>
        public double SignedDistance(double x, double y)
        {
            double d = DistanceToBoundary(x, y);
            return Contains(x, y) ? d : -d;
        }

        private static double SegmentDistanceSquared(double px, double py, double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            double lengthSquared = (dx * dx) + (dy * dy);
            double t = lengthSquared > 0 ? (((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared : 0;
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            double ex = ax + (t * dx) - px;
            double ey = ay + (t * dy) - py;
            return (ex * ex) + (ey * ey);
        }

        /// <summary>
        /// Adds to <paramref name="crossings"/>[k] the X of every segment crossing the horizontal line
        /// <paramref name="firstY"/> + k·<paramref name="stepY"/>. Half-open rule, so sorted crossings pair into inside
        /// intervals. Cost is O(segments + crossings), not O(segments × scanlines).
        /// </summary>
        public void AddScanlineCrossings(double firstY, double stepY, List<double>[] crossings)
        {
            int lines = crossings.Length;
            for (int i = 0; i < AX.Length; i++)
            {
                double ay = AY[i], by = BY[i];
                if (ay == by)
                    continue;

                double lowY = Math.Min(ay, by);
                double highY = Math.Max(ay, by);

                int kStart = Math.Max(0, (int)Math.Ceiling((lowY - firstY) / stepY));
                int kEnd = Math.Min(lines - 1, (int)Math.Ceiling((highY - firstY) / stepY) - 1);
                for (int k = kStart; k <= kEnd; k++)
                {
                    double y = firstY + (k * stepY);
                    if (y < lowY || y >= highY)
                        continue;

                    crossings[k].Add(AX[i] + ((y - ay) / (by - ay) * (BX[i] - AX[i])));
                }
            }
        }

        /// <summary>True if any segment passes through the open axis-aligned rectangle in this set's frame.</summary>
        public bool CrossesRectangle(double minX, double minY, double maxX, double maxY)
        {
            for (int i = 0; i < AX.Length; i++)
            {
                if (SegmentCrossesRectangle(AX[i], AY[i], BX[i], BY[i], minX, minY, maxX, maxY))
                    return true;
            }

            return false;
        }

        /// <summary>Liang–Barsky clip of the segment against the open rectangle.</summary>
        private static bool SegmentCrossesRectangle(double ax, double ay, double bx, double by,
            double minX, double minY, double maxX, double maxY)
        {
            double t0 = 0, t1 = 1;
            double dx = bx - ax, dy = by - ay;

            if (!Clip(-dx, ax - minX, ref t0, ref t1)) return false;
            if (!Clip(dx, maxX - ax, ref t0, ref t1)) return false;
            if (!Clip(-dy, ay - minY, ref t0, ref t1)) return false;
            if (!Clip(dy, maxY - ay, ref t0, ref t1)) return false;

            // A segment that only touches the rectangle's border is not a crossing
            double mx = ax + (dx * (t0 + t1) / 2);
            double my = ay + (dy * (t0 + t1) / 2);
            return mx > minX && mx < maxX && my > minY && my < maxY;
        }

        private static bool Clip(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0)
                return q >= 0;

            double r = q / p;
            if (p < 0)
            {
                if (r > t1) return false;
                if (r > t0) t0 = r;
            }
            else
            {
                if (r < t0) return false;
                if (r < t1) t1 = r;
            }

            return true;
        }
    }
}
