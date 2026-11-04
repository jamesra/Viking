using System;
using System.Collections.Generic;
using System.Linq;

namespace Viking.SegmentationPrompts.Geometry
{
    /// <summary>
    /// The closed Catmull-Rom fit Viking draws a CURVEPOLYGON (TypeCode 6) ring with, and the training exporter fits
    /// before it rasterizes a mask: <c>CurveExtensions.CalculateClosedCurvePoints</c> (centripetal, alpha 0.5,
    /// 10 interpolations per span, extra samples where the curve turns more than 10 degrees).
    /// </summary>
    /// <remarks>
    /// A port of nornir <c>segmentationtraining/curves.py</c>, itself a port of <c>Geometry/CurveFitting/CatmullRom.cs</c>,
    /// kept step for step so a prompt's ring is the ring the model was trained on (the tests compare it with
    /// fixtures that file wrote). The Geometry project is not referenced: this library builds on its own.
    /// </remarks>
    internal static class ClosedCurveFit
    {
        /// <summary><c>Geometry.Global.Epsilon</c>, which GridVector2 equality uses.</summary>
        private const double Epsilon = 0.001;
        private const double EpsilonSquared = Epsilon * Epsilon;

        /// <summary><c>ShapeSmoothingExtensions.NumClosedCurveInterpolationPoints</c>.</summary>
        internal const int ClosedCurveInterpolations = 10;

        private const double CurvatureAngleDegrees = 10.0;
        private const double CurvatureDistanceThreshold = 0.0625; // 0.25 squared, TryAddTPointsAboveThreshold
        private const int MaxCurveRecursion = 32;

        /// <summary>
        /// The fitted ring of a closed control ring (first point repeated last, as WKT stores it), returned closed the
        /// same way. Under three points it is returned as given.
        /// </summary>
        public static List<Point> FitClosedRing(IReadOnlyList<Point> ring, int interpolations = ClosedCurveInterpolations)
        {
            if (ring.Count < 3)
                return ring.ToList();
            List<Point> fitted = CalculateClosedCurvePoints(ring, interpolations);
            if (fitted.Count == 0)
                return ring.ToList();
            if (!PointsEqual(fitted[0], fitted[fitted.Count - 1]))
                fitted.Add(fitted[0]);
            else
                fitted[fitted.Count - 1] = fitted[0];
            return fitted;
        }

        private static List<Point> CalculateClosedCurvePoints(IReadOnlyList<Point> controlPoints, int interpolations)
        {
            if (controlPoints.Count <= 2)
                return controlPoints.ToList();
            if (interpolations == 0)
            {
                List<Point> closed = controlPoints.ToList();
                if (!PointsEqual(closed[0], closed[closed.Count - 1]))
                    closed.Add(closed[0]);
                return closed;
            }

            List<Point> padded = ControlPointsForClosedCurve(controlPoints);
            var samples = new List<Point>();
            for (int i = 0; i < padded.Count - 3; i++)
                samples.AddRange(RecursivelyFitCurveSegment(padded[i], padded[i + 1], padded[i + 2], padded[i + 3], interpolations));
            List<Point> fitted = RemoveAdjacentDuplicatesPreserving(samples, controlPoints);
            if (fitted.Count > 0)
                fitted[fitted.Count - 1] = fitted[0];
            return fitted;
        }

        /// <summary>
        /// Four-point windows only emit the span between the middle pair, so a closed ring A-B-C-D-A is padded to
        /// D-A-B-C-D-A-B (<c>CatmullRom.GetControlPointsForClosedCurve</c>).
        /// </summary>
        private static List<Point> ControlPointsForClosedCurve(IReadOnlyList<Point> controlPoints)
        {
            List<Point> points = controlPoints.ToList();
            int index = 1;
            while (index < points.Count - 1)
            {
                if (DistanceSquared(points[index - 1], points[index]) < EpsilonSquared)
                {
                    points.RemoveAt(index);
                    continue;
                }
                index++;
            }
            if (!PointsEqual(points[0], points[points.Count - 1]))
                points.Insert(0, points[points.Count - 1]);
            Point afterStart = points[1];
            Point beforeStart = points[points.Count - 2];
            points.Insert(0, beforeStart);
            points.Add(afterStart);
            return points;
        }

        private static List<Point> RecursivelyFitCurveSegment(Point p0, Point p1, Point p2, Point p3, int interpolations)
        {
            var scalars = new SortedSet<double>();
            if (interpolations <= 1)
            {
                if (interpolations == 1)
                    scalars.Add(0.5);
                else
                {
                    scalars.Add(0.0);
                    scalars.Add(1.0);
                }
            }
            else
            {
                for (int i = 0; i < interpolations; i++)
                    scalars.Add(i / (interpolations - 1.0));
            }
            return RecursivelyFitCurveSegment(p0, p1, p2, p3, scalars, 0);
        }

        private static List<Point> RecursivelyFitCurveSegment(Point p0, Point p1, Point p2, Point p3, SortedSet<double> scalars, int depth)
        {
            List<double> ordered = scalars.ToList();
            double t1 = Tj(0.0, p0, p1);
            double t2 = Tj(t1, p1, p2);
            double t3 = Tj(t2, p2, p3);
            List<double> tValues = ordered.Select(scalar => t1 + scalar * (t2 - t1)).ToList();
            List<Point> output = FitCurveSegmentWithTValues(p0, p1, p2, p3, 0.0, t1, t2, t3, tValues);
            if (depth >= MaxCurveRecursion)
                return output;
            if (!TryAddTPointsAboveThreshold(output, ordered, out SortedSet<double> updated))
                return output;
            return RecursivelyFitCurveSegment(p0, p1, p2, p3, updated, depth + 1);
        }

        /// <summary>
        /// The centripetal knot step, <c>|pj - pi|^0.5</c>. Square roots, not <c>Math.Pow</c>: a square root is correctly
        /// rounded on every runtime, so Viking on Windows and the gallery's native build on Linux fit the same bits.
        /// </summary>
        private static double Tj(double ti, Point pi, Point pj) => Math.Sqrt(Distance(pi, pj)) + ti;

        /// <summary><c>CatmullRom.FitCurveSegmentWithTValues</c>, centripetal; the arithmetic keeps the port's order.</summary>
        private static List<Point> FitCurveSegmentWithTValues(
            Point p0, Point p1, Point p2, Point p3, double t0, double t1, double t2, double t3, IEnumerable<double> tValues)
        {
            var output = new List<Point>();
            foreach (double t in tValues)
            {
                double a1x = ((t1 - t) / (t1 - t0)) * p0.X + ((t - t0) / (t1 - t0)) * p1.X;
                double a1y = ((t1 - t) / (t1 - t0)) * p0.Y + ((t - t0) / (t1 - t0)) * p1.Y;
                double a2x = ((t2 - t) / (t2 - t1)) * p1.X + ((t - t1) / (t2 - t1)) * p2.X;
                double a2y = ((t2 - t) / (t2 - t1)) * p1.Y + ((t - t1) / (t2 - t1)) * p2.Y;
                double a3x = ((t3 - t) / (t3 - t2)) * p2.X + ((t - t2) / (t3 - t2)) * p3.X;
                double a3y = ((t3 - t) / (t3 - t2)) * p2.Y + ((t - t2) / (t3 - t2)) * p3.Y;
                double b1x = ((t2 - t) / (t2 - t0)) * a1x + ((t - t0) / (t2 - t0)) * a2x;
                double b1y = ((t2 - t) / (t2 - t0)) * a1y + ((t - t0) / (t2 - t0)) * a2y;
                double b2x = ((t3 - t) / (t3 - t1)) * a2x + ((t - t1) / (t3 - t1)) * a3x;
                double b2y = ((t3 - t) / (t3 - t1)) * a2y + ((t - t1) / (t3 - t1)) * a3y;
                double cx = ((t2 - t) / (t2 - t1)) * b1x + ((t - t1) / (t2 - t1)) * b2x;
                double cy = ((t2 - t) / (t2 - t1)) * b1y + ((t - t1) / (t2 - t1)) * b2y;
                output.Add(new Point(cx, cy));
            }
            return output;
        }

        /// <summary><c>CurveExtensions.TryAddTPointsAboveThreshold</c>: a mid scalar either side of each sharp turn.</summary>
        private static bool TryAddTPointsAboveThreshold(List<Point> output, List<double> tPoints, out SortedSet<double> updated)
        {
            List<Point> points = output.ToList();
            List<double> scalars = tPoints.ToList();
            int index = 1;
            while (index < points.Count - 1)
            {
                if (DistanceSquared(points[index - 1], points[index]) < EpsilonSquared ||
                    DistanceSquared(points[index], points[index + 1]) < EpsilonSquared)
                {
                    points.RemoveAt(index);
                    scalars.RemoveAt(index);
                    continue;
                }
                index++;
            }
            double[] degrees = MeasureCurvature(points).Select(Math.Abs).ToArray();
            double threshold = (Math.PI * 2.0 / 360.0) * CurvatureAngleDegrees;
            var needs = new bool[scalars.Count];
            for (int i = scalars.Count - 2; i > 0; i--)
            {
                if (degrees[i] > threshold)
                {
                    double distance = DistanceSquared(points[i - 1], points[i]) + DistanceSquared(points[i], points[i + 1]);
                    needs[i] = distance > CurvatureDistanceThreshold;
                }
            }
            updated = new SortedSet<double>(scalars);
            int starting = updated.Count;
            for (int i = 0; i < needs.Length - 1; i++)
            {
                if (needs[i] || needs[i + 1])
                    updated.Add((scalars[i] + scalars[i + 1]) / 2.0);
            }
            return starting != updated.Count;
        }

        /// <summary><c>CurveExtensions.MeasureCurvature</c>: each vertex's turn from its incoming chord.</summary>
        private static double[] MeasureCurvature(IReadOnlyList<Point> points)
        {
            var angles = new double[points.Count];
            if (points.Count < 3)
                return angles;
            for (int i = 1; i < points.Count - 1; i++)
            {
                if (points[i - 1].X == points[i].X && points[i - 1].Y == points[i].Y)
                    throw new InvalidOperationException("Duplicate points found in MeasureCurvature");
                if (DistanceSquared(points[i], points[i - 1]) < EpsilonSquared)
                    continue;
                Point incoming = PointAlongLine(points[i - 1], points[i], 2.0);
                angles[i] = ArcAngle(points[i], incoming, points[i + 1]);
            }
            return angles;
        }

        /// <summary>
        /// <c>GridVector2.ArcAngle</c> (counter-clockwise negative). Only compared with the 10 degree threshold, so a
        /// last-bit difference in a runtime's <c>Atan2</c> changes nothing unless a turn is within that bit of 10 degrees.
        /// </summary>
        private static double ArcAngle(Point origin, Point a, Point b)
        {
            double angle = Math.Atan2(a.Y - origin.Y, a.X - origin.X) - Math.Atan2(b.Y - origin.Y, b.X - origin.X);
            if (angle <= -Math.PI)
                angle += Math.PI * 2;
            else if (angle > Math.PI)
                angle -= Math.PI * 2;
            return angle;
        }

        /// <summary>
        /// <c>RemoveAdjacentDuplicates(points, preserved_path)</c>: drops samples within epsilon of their neighbour, but
        /// keeps every control point the curve passes through. The read-ahead inside the loop consumes the same
        /// sequence the loop walks, as the original's iterator does.
        /// </summary>
        private static List<Point> RemoveAdjacentDuplicatesPreserving(IReadOnlyList<Point> points, IReadOnlyList<Point> preserved)
        {
            if (points.Count == 0)
                throw new ArgumentException("Must have at least one point to remove adjacent duplicates", nameof(points));
            int preservedIndex = 0;
            Point? NextPreserved() => preservedIndex < preserved.Count ? preserved[preservedIndex++] : (Point?)null;
            int next = 0;
            Point? Read() => next < points.Count ? points[next++] : (Point?)null;

            Point? keep = NextPreserved();
            var result = new List<Point>();
            Point current = points[next++];
            Point? following = null;
            bool advanced = false;
            while (next < points.Count)
            {
                following = Read();
                advanced = true;
                if (keep.HasValue && current.X == keep.Value.X && current.Y == keep.Value.Y)
                {
                    while (following.HasValue && PointsEqual(following.Value, keep.Value))
                        following = Read();
                    result.Add(current);
                    keep = NextPreserved();
                }
                else if (keep.HasValue && PointsEqual(current, keep.Value))
                {
                    if (!following.HasValue || !PointsEqual(following.Value, keep.Value))
                    {
                        result.Add(keep.Value);
                        keep = NextPreserved();
                    }
                }
                else if (!following.HasValue || !PointsEqual(current, following.Value))
                {
                    result.Add(current);
                }
                if (!following.HasValue)
                    break;
                current = following.Value;
            }
            if (!advanced)
                result.Add(current);
            else if (following.HasValue)
                result.Add(following.Value);
            return result;
        }

        private static Point PointAlongLine(Point a, Point b, double fraction) =>
            new Point(a.X + (b.X - a.X) * fraction, a.Y + (b.Y - a.Y) * fraction);

        private static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private static double Distance(Point a, Point b) => Math.Sqrt(DistanceSquared(a, b));

        private static bool PointsEqual(Point a, Point b) => DistanceSquared(a, b) <= EpsilonSquared;
    }
}
