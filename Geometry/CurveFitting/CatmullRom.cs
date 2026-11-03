using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    public static class CatmullRom
    {
        /// <summary>
        /// In a closed curve we append some repeating control points to the list so the implementation can index them without special cases.
        /// </summary>
        private static List<Vector2> GetControlPointsForClosedCurve(IList<Vector2> cp)
        {
            ///
            /// Catmull rom implementation only returns the points between the middle two points of a set of four. 
            /// So if our points are A - B - C - D - E 
            /// We need to input E - A - B - C - D - E - A - B to return a complete curve with the same starting/ending point as the input
            ///

            //Remove any points that are closer than epsilon distanceu04908
            for (int i = 1; i < cp.Count - 1; i++)
            {
                if (Vector2.DistanceSquared(cp[i - 1], cp[i]) < Tolerance.EpsilonSquared)
                {
                    cp.RemoveAt(i);
                    i--;
                }
            }

            List<Vector2> output = [.. cp];
            /*
            if (output.First() != output.Last())
            {
                output.Add(output.First());
            }

            output.AddRange(output.GetRange(1, 2));

            
            */

            if (output.First() != output.Last())
            {
                output.Insert(0, output.Last());
            }

            Vector2 AfterStart = output[1];
            Vector2 BeforeStart = output[output.Count - 2];

            //output.AddRange(output.GetRange(1, 2));

            output.Insert(0, BeforeStart);
            output.Add(AfterStart);

            return output;
        }

        /// <summary>
        /// Extrapolate a point using a straight line from the last two points, half the distance between the two control points
        /// </summary>
        /// <param name="cp"></param>
        private static List<Vector2> GetControlPointsForOpenCurve(IList<Vector2> cp)
        {
            List<Vector2> output = [.. cp];

            Vector2 zeroPoint = GetStartingPointForOpenCurve(output);
            output.Insert(0, zeroPoint);
            Vector2 lastPoint = GetEndingPointForOpenCurve(output);
            output.Add(lastPoint);

            return output;
        }


        internal static Vector2 GetStartingPointForOpenCurve(IReadOnlyList<Vector2> cp)
        {
            LineSegment start = new(cp[0], cp[1]);
            Vector2 zeroPoint = start.PointAlongLine(-0.5);
            return zeroPoint;
        }

        internal static Vector2 GetEndingPointForOpenCurve(IReadOnlyList<Vector2> cp)
        {
            LineSegment end = new(cp[cp.Count - 2], cp[cp.Count - 1]);
            Vector2 lastPoint = end.PointAlongLine(1.5);
            return lastPoint;
        }


        public static Vector2[] FitCurve(IReadOnlyList<Vector2> ControlPoints, uint NumInterpolations, bool closed)
        {
            //Two points are a straight line, so don't bother interpolating
            if (ControlPoints.Count <= 2 || NumInterpolations == 0)
            {
                return [.. ControlPoints];
            }

            List<Vector2> cp = [.. ControlPoints];
            cp = closed ? GetControlPointsForClosedCurve(cp) : GetControlPointsForOpenCurve(cp);

            //return cp.Where((p, i) => i + 3 < cp.Count).SelectMany((p, i) => FitCurveSegment(cp[i], cp[i + 1], cp[i + 2], cp[i + 3], NumInterpolations)).ToArray();
            var curved_points = cp.Where((p, i) => i + 3 < cp.Count).SelectMany((p, i) => RecursivelyFitCurveSegment(cp[i], cp[i + 1], cp[i + 2], cp[i + 3], NumInterpolations: NumInterpolations));
            Vector2[] points = [.. curved_points.RemoveAdjacentDuplicates(ControlPoints)];

#if DEBUG
            for (int i = 0; i < ControlPoints.Count; i++)
            {
                Debug.Assert(points.Contains(ControlPoints[i]),
                    $"FitCurve output is missing control point #{i} of {ControlPoints.Count} {ControlPoints[i]}");
            }
#endif

            return points;
        }

        /// <summary>
        /// Returns a curve over a range, does not return the final value which should match p2
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="NumInterpolations">The number of points we would like returned between p1 and p2</param>
        /// <returns></returns>
        public static Vector2[] FitCurveSegment(in Vector2 p0, in Vector2 p1,
            in Vector2 p2, in Vector2 p3,
                                                    int NumInterpolations)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, in p0, in p1, alpha);
            double t2 = tj(t1, in p1, in p2, alpha);
            //double t3 = tj(t2, p2, p3, alpha); //TODO: Check why this is calculated but not used

            double[] tvalues = new double[NumInterpolations];

            if (NumInterpolations == 1)
            {
                tvalues[0] = t1 + 0.5 * (t2 - t1);
            }
            else
            {
                for (int i = 0; i < tvalues.Length; i++)
                {
                    double fraction = (double)i / ((double)NumInterpolations - 1.0);
                    tvalues[i] = t1 + fraction * (t2 - t1);
                }
            }

            Vector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);
            return output;
        }

        /// <summary>
        /// Returns a curve over a range, does not return the final value which should match p2
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="tPointsArray">Fraction distances along curve between p1 & p2 to add points</param>
        /// <returns></returns>
        public static Vector2[] FitCurveSegment(in Vector2 p0, in Vector2 p1,
                                                    in Vector2 p2, in Vector2 p3,
                                                    double[] tPointsArray)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, in p0, in p1, alpha);
            double t2 = tj(t1, in p1, in p2, alpha);
            //double t3 = tj(t2, p2, p3, alpha); //TODO: Check why this is calculated but not used

            double[] tvalues = TScalarsToTValues(tPointsArray, t1, t2);

            Vector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);
            return output;
        }

        /// <summary>
        /// Returns a point on a curve between iStart and iStart + 1 at the requested fractional amount
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="tPointsArray">Fraction distances along curve between p1 & p2 to add points</param>
        /// <returns></returns>
        public static Vector2[] FitCurveSegment(IReadOnlyList<Vector2> Points, int iStart, double[] tpointsArray)
        {
            //Cannot add a curve to a straight line
            if (Points.Count < 2)
                return [.. Points];

            if (iStart >= Points.Count - 1)
                throw new ArgumentException($"Starting point {iStart} is the last point in the Points array and a curve cannot be calculated.");

            Vector2 p0, p1, p2, p3;

            p0 = iStart - 1 >= 0 ? Points[iStart - 1] : GetStartingPointForOpenCurve(Points);
            p1 = Points[iStart];
            p2 = Points[iStart + 1];
            p3 = iStart + 2 < Points.Count ? Points[iStart + 2] : GetEndingPointForOpenCurve(Points);

            return FitCurveSegment(p0, p1, p2, p3, tpointsArray);
        }

        /// <summary>
        /// Returns a point on a curve between iStart and iStart + 1 at the requested fractional amount
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="tPointsArray">Fraction distances along curve between p1 & p2 to add points</param>
        /// <returns></returns>
        public static Vector2[] FitCurveSegment(IReadOnlyList<IPoint2D> Points, int iStart, double[] tpointsArray) => FitCurveSegment(Points.Select(p => p.ToVector2()).ToArray(), iStart, tpointsArray);

        /// <summary>
        /// Returns a curve over a range, does not return the final value which should match the control point
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="tvalues">The fractional distances between P1 and P2 that we would like returned</param>
        /// <returns>One point per t value, in the same order</returns>
        /// <remarks>
        /// Each point depends only on its own t value. The expressions and their order match the array-per-stage version this
        /// replaced, so results are bit-identical (including NaN or infinity when two control points coincide). Do not
        /// reassociate or hoist divisions into reciprocals.
        /// </remarks>
        private static Vector2[] FitCurveSegmentWithTValues(Vector2 p0, Vector2 p1,
                                                    Vector2 p2, Vector2 p3,
                                                    double[] tvalues)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            double t3 = tj(t2, p2, p3, alpha);

            Vector2[] output = new Vector2[tvalues.Length];
            for (int i = 0; i < tvalues.Length; i++)
            {
                double t = tvalues[i];

                double a1x = (t1 - t) / (t1 - t0) * p0.X + (t - t0) / (t1 - t0) * p1.X;
                double a1y = (t1 - t) / (t1 - t0) * p0.Y + (t - t0) / (t1 - t0) * p1.Y;

                double a2x = (t2 - t) / (t2 - t1) * p1.X + (t - t1) / (t2 - t1) * p2.X;
                double a2y = (t2 - t) / (t2 - t1) * p1.Y + (t - t1) / (t2 - t1) * p2.Y;

                double a3x = (t3 - t) / (t3 - t2) * p2.X + (t - t2) / (t3 - t2) * p3.X;
                double a3y = (t3 - t) / (t3 - t2) * p2.Y + (t - t2) / (t3 - t2) * p3.Y;

                double b1x = ((t2 - t) / (t2 - t0)) * a1x + ((t - t0) / (t2 - t0)) * a2x;
                double b1y = ((t2 - t) / (t2 - t0)) * a1y + ((t - t0) / (t2 - t0)) * a2y;

                double b2x = ((t3 - t) / (t3 - t1)) * a2x + ((t - t1) / (t3 - t1)) * a3x;
                double b2y = ((t3 - t) / (t3 - t1)) * a2y + ((t - t1) / (t3 - t1)) * a3y;

                double cx = ((t2 - t) / (t2 - t1)) * b1x + ((t - t1) / (t2 - t1)) * b2x;
                double cy = ((t2 - t) / (t2 - t1)) * b1y + ((t - t1) / (t2 - t1)) * b2y;

                output[i] = new Vector2(cx, cy);
            }

            return output;
        }

        /// <summary>
        /// Adds points along the curve described by p0 to p3 values. 
        /// New points are inserted at fractions described in tPoints. 
        /// 
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="NumInterpolations"></param>
        /// <returns></returns>
        public static Vector2[] RecursivelyFitCurveSegment(in Vector2 p0, in Vector2 p1,
            in Vector2 p2, in Vector2 p3,
                                                    uint NumInterpolations = 5)
        {
            // One evenly spaced point would be i / (1 - 1) = 0 / 0 = NaN, which made every curve point NaN and dropped
            // all control points (CurveView defaults to 1). Treat 1 as 2 so each segment always keeps p1 and p2 and the
            // curvature refinement below still adds points where needed. 0 still means "no points".
            if (NumInterpolations == 1)
                NumInterpolations = 2;

            double[] tvalues = new double[NumInterpolations];
            SortedSet<double> tPoints = [.. tvalues.Select((t, i) => ((double)i / ((double)NumInterpolations - 1.0)))];

            return RecursivelyFitCurveSegment(in p0, in p1, in p2, in p3, tPoints);
        }

        /// <summary>
        /// Adds points along the curve described by p0 to p3 values. 
        /// New points are inserted at fractions described in tPoints. 
        /// 
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="tPoints">Points along curve between p1 and p2 that we will insert, 0 = p1 & 1 = p2. </param> 
        /// <returns></returns>
        public static Vector2[] RecursivelyFitCurveSegment(in Vector2 p0, in Vector2 p1,
            in Vector2 p2, in Vector2 p3,
                                                    SortedSet<double> tPoints)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, in p0, in p1, alpha);
            double t2 = tj(t1, in p1, in p2, alpha);

            while (true)
            {
                double[] tvalues = TScalarsToTValues(tPoints, t1, t2);

                Vector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);

                if (!CurveExtensions.TryAddTPointsAboveThreshold(output, ref tPoints))
                {
                    //We could not add any additional T points at high curvature regions, return the result
                    return output;
                }
            }
        }

        /// <summary>
        /// Interpolates scalar values into the range t1 and t2.
        /// </summary>
        /// <param name="tpoints">Scalar values</param>
        /// <param name="t1">Min val</param>
        /// <param name="t2">Max val</param>
        /// <returns></returns>
        private static double[] TScalarsToTValues(this SortedSet<double> tpoints, double t1, double t2)
        {
            double[] tvalues = new double[tpoints.Count];
            tpoints.CopyTo(tvalues);
            for (int i = 0; i < tvalues.Length; i++)
            {
                tvalues[i] = t1 + tvalues[i] * (t2 - t1);
            }

            return tvalues;
        }

        /// <summary>
        /// Interpolates scalar values into the range t1 and t2.
        /// </summary>
        /// <param name="tpoints">Scalar values</param>
        /// <param name="t1">Min val</param>
        /// <param name="t2">Max val</param>
        /// <returns></returns>
        private static double[] TScalarsToTValues(this IReadOnlyList<double> tpoints, double t1, double t2)
        {
            double[] tvalues = new double[tpoints.Count];
            for (int i = 0; i < tvalues.Length; i++)
            {
                tvalues[i] = t1 + tpoints[i] * (t2 - t1);
            }

            return tvalues;
        }

        private static double tj(double ti, in Vector2 Pi, in Vector2 Pj, double Alpha = 0.5) => Math.Pow(Vector2.Distance(in Pi, in Pj), Alpha) + ti;
    }

    /// <summary>
    /// Contains functions to identify a small set of control points required to approximate a curved path
    /// </summary>
    public static class CatmullRomControlPointSimplification
    {
        private static List<Vector2> GenerateStartingSimplifiedLine(this IList<Vector2> path, bool IsClosed)
        {
            if (IsClosed)
            {
                return GenerateStartingSimplifiedClosedLine(path);
            }
            else
            {
                return GenerateStartingSimplifiedOpenLine(path);
            }
        }

        /// <summary>
        /// CatmullRom requires four points to describe a curve. 0
        /// When fitting an open line we begin using the first and last point.  The curve fitting will extrapolate a point before and after these points for a total of four
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static List<Vector2> GenerateStartingSimplifiedOpenLine(this IList<Vector2> path) => [path.First(), path.Last()];

        /// <summary>
        /// CatmullRom requires four points to describe a curve. 
        /// When fitting an closed line we use the first/last point as starting and stopping points and the points with the largest change in angle to fill in.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static List<Vector2> GenerateStartingSimplifiedClosedLine(this IList<Vector2> path)
        {
            //Find the two largest changes in angles in the input path
            double[] angle_change = path.ToArray().MeasureCurvature().TakeDerivative();
            int[] iSorted = angle_change.SortAndIndex();

            iSorted = [.. iSorted.Where(i => i != 0 && i != path.Count - 1)]; //Remove the first/last vertex because we know we are including it. 

            int[] firstTwo = [iSorted[iSorted.Length - 1], iSorted[iSorted.Length - 2]];
            if (firstTwo[0] > firstTwo[1])
            {
                firstTwo = [.. ((IEnumerable<int>)firstTwo).Reverse()];
            }

            return [path.First(), path[firstTwo[0]], path[firstTwo[1]], path.Last()];
        }

        /// <summary>
        /// Take a high density path, fit a curve to it using catmull rom, and pick a smaller set of the curve's points as
        /// control points such that the Catmull-Rom curve through them stays within
        /// <paramref name="MaxDistanceFromSimplifiedToIdeal"/> of the fitted curve, measured both ways (see <see cref="RefineControlPoints"/>).
        /// </summary>
        /// <param name="input">High density path to simplify</param>
        /// <param name="MaxDistanceFromSimplifiedToIdeal">Largest distance allowed between the fitted curve and the curve through the control points</param>
        /// <param name="IsClosed">True if the path is a closed ring</param>
        /// <param name="NumInterpolations">Number of points generated along each curve segment</param>
        public static List<Vector2> IdentifyControlPoints(this IReadOnlyList<Vector2> input, double MaxDistanceFromSimplifiedToIdeal, bool IsClosed, uint NumInterpolations = 8)
        {
            //Copy the path so we don't modify the input
            List<Vector2> path = [.. input.Select(p => p.Round(Tolerance.TransformSignificantDigits)).RemoveAdjacentDuplicates()];
            //var path = input.ToList();

            //We can't simplify the already simple...
            if (path is null || path.Count <= 2)
                return path;

            if (path.Count <= 4 && IsClosed)
                return path;

            if (IsClosed && path.First() != path.Last())
            {
                path.Add(path.First());
            }

            var curved_path = Geometry.CatmullRom.FitCurve(path, NumInterpolations, IsClosed).Select(p => p.Round(Tolerance.TransformSignificantDigits)).RemoveAdjacentDuplicates().ToArray();

            // The fitted curve is the reference. A closed one repeats its first point at the end; the reference does not.
            Vector2[] reference = IsClosed && curved_path.Length > 1 && curved_path[0] == curved_path[curved_path.Length - 1]
                ? curved_path.Take(curved_path.Length - 1).ToArray()
                : curved_path;

            UniquePointMap<int> point_to_ideal_curve_index = new();
            for (int i = 0; i < reference.Length; i++)
            {
                if (!point_to_ideal_curve_index.Contains(reference[i]))
                    point_to_ideal_curve_index.Add(reference[i], i);
            }

            List<int> startIndices = [];
            foreach (Vector2 start in GenerateStartingSimplifiedLine(path, IsClosed))
            {
                if (point_to_ideal_curve_index.TryFindNearest(start, out _, out int index, out _))
                    startIndices.Add(index);
            }

            if (!IsClosed)
            {
                startIndices.Add(0);
                startIndices.Add(reference.Length - 1);
            }

            List<int> controlIndices = RefineControlPoints(reference, startIndices, IsClosed, MaxDistanceFromSimplifiedToIdeal, NumInterpolations);
            List<Vector2> simplified = [.. controlIndices.Select(i => reference[i])];
            if (IsClosed)
                simplified.Add(simplified[0]);

            return simplified;
        }

        /// <summary>
        /// Inserts vertices of <paramref name="reference"/> as control points until the Catmull-Rom curve through the control
        /// points stays within <paramref name="tolerance"/> of the reference polyline. The distance is checked both ways on every
        /// span: from each reference vertex to the curve, and from each curve sample back to the reference. A point inserted in
        /// one span changes the tangent of its neighbours, so every span is checked again until a whole pass inserts nothing.
        /// Control points already given are never moved or removed, which lets a caller keep part of a ring exactly as it was.
        /// </summary>
        /// <param name="reference">The polyline to follow. A closed ring must not repeat its first vertex at the end.</param>
        /// <param name="controlIndices">
        /// Indices into <paramref name="reference"/> to start from. An open curve should include the first and last index. A closed
        /// one with fewer than three gets the middle of its longest gap added until it has three. Duplicates and order do not matter.
        /// </param>
        /// <param name="isClosed">True when <paramref name="reference"/> is a closed ring.</param>
        /// <param name="tolerance">Largest distance allowed between the reference and the curve, in the reference's units.</param>
        /// <param name="numInterpolations">Samples per span used to measure the curve (more are added where it bends).</param>
        /// <returns>The control indices in ascending order.</returns>
        public static List<int> RefineControlPoints(IReadOnlyList<Vector2> reference, IEnumerable<int> controlIndices, bool isClosed, double tolerance, uint numInterpolations)
        {
            int n = reference.Count;
            List<int> indices = [.. controlIndices.Where(i => i >= 0 && i < n).Distinct().OrderBy(i => i)];
            if (isClosed)
                AddMiddlesUntilThree(indices, n);

            if (n < 3 || indices.Count < 2 || (isClosed && indices.Count < 3))
                return indices;

            bool inserted;
            do
            {
                inserted = false;
                int span = 0;
                while (span < (isClosed ? indices.Count : indices.Count - 1))
                {
                    int iStart = indices[span];
                    // A closed ring's last span wraps to the first control point; its indices run past n and are taken mod n.
                    int iEnd = span + 1 < indices.Count ? indices[span + 1] : indices[0] + n;
                    if (iEnd - iStart < 2)
                    {
                        span++;
                        continue;
                    }

                    Vector2 p1 = reference[iStart];
                    Vector2 p2 = reference[iEnd % n];
                    Vector2 p0;
                    Vector2 p3;
                    if (isClosed)
                    {
                        p0 = reference[indices[(span - 1 + indices.Count) % indices.Count]];
                        p3 = reference[indices[(span + 2) % indices.Count]];
                    }
                    else
                    {
                        p0 = span > 0 ? reference[indices[span - 1]] : CatmullRom.GetStartingPointForOpenCurve([p1, p2]);
                        p3 = span + 2 < indices.Count ? reference[indices[span + 2]] : CatmullRom.GetEndingPointForOpenCurve([p1, p2]);
                    }

                    Vector2[] curve = CatmullRom.RecursivelyFitCurveSegment(p0, p1, p2, p3, numInterpolations);
                    int worst = WorstReferenceVertex(reference, iStart, iEnd, curve, out double worstDistance);
                    if (worstDistance > tolerance && worst >= 0)
                    {
                        int value = worst % n;
                        int position = value > indices[indices.Count - 1] ? indices.Count : indices.BinarySearch(value);
                        if (position < 0)
                            position = ~position;

                        indices.Insert(position, value);
                        inserted = true;

                        // A wrapped insertion lands at the front, so the span being refined moved one place along
                        if (position <= span)
                            span++;
                    }
                    else
                    {
                        span++;
                    }
                }
            }
            while (inserted);

            return indices;
        }

        /// <summary>
        /// Adds points along the two segments of a closed reference ring that meet each corner vertex, no more than
        /// <paramref name="spacing"/> apart. Where a pen stroke meets an old outline at an angle, the curve rounds the corner;
        /// with only the corner's two neighbours to choose from, <see cref="RefineControlPoints"/> cannot pin it, and the rounding
        /// can exceed the tolerance. The added points lie on the reference, so they do not change what the curve must follow.
        /// </summary>
        /// <param name="ring">The ring, without a closing repeat of the first vertex.</param>
        /// <param name="isCorner">For each vertex of <paramref name="ring"/>, whether to densify next to it.</param>
        /// <param name="sourceIndex">For each returned point, the index of the vertex of <paramref name="ring"/> it is, or -1 for an added point.</param>
        public static List<Vector2> DensifyNextToCorners(IReadOnlyList<Vector2> ring, IReadOnlyList<bool> isCorner, double spacing, out List<int> sourceIndex)
        {
            int n = ring.Count;
            List<Vector2> output = new(n);
            sourceIndex = new(n);
            for (int i = 0; i < n; i++)
            {
                output.Add(ring[i]);
                sourceIndex.Add(i);

                int next = (i + 1) % n;
                if (!(isCorner[i] || isCorner[next]) || spacing <= 0)
                    continue;

                double length = Vector2.Distance(ring[i], ring[next]);
                int pieces = (int)Math.Ceiling(length / spacing);
                for (int k = 1; k < pieces; k++)
                {
                    double t = (double)k / pieces;
                    output.Add(new Vector2(ring[i].X + t * (ring[next].X - ring[i].X), ring[i].Y + t * (ring[next].Y - ring[i].Y)));
                    sourceIndex.Add(-1);
                }
            }

            return output;
        }

        /// <summary>A closed curve needs three control points; adds the middle of the longest gap until it has them.</summary>
        private static void AddMiddlesUntilThree(List<int> indices, int count)
        {
            while (indices.Count is > 0 and < 3 && indices.Count < count)
            {
                int bestStart = 0;
                int bestGap = -1;
                for (int i = 0; i < indices.Count; i++)
                {
                    int next = i + 1 < indices.Count ? indices[i + 1] : indices[0] + count;
                    if (next - indices[i] > bestGap)
                    {
                        bestGap = next - indices[i];
                        bestStart = indices[i];
                    }
                }

                if (bestGap < 2)
                    return;

                indices.Add((bestStart + bestGap / 2) % count);
                indices.Sort();
            }
        }

        /// <summary>
        /// The reference vertex strictly between <paramref name="iStart"/> and <paramref name="iEnd"/> (indices taken mod the
        /// count) that best fixes the largest gap between the reference and <paramref name="curve"/>: the farthest vertex from
        /// the curve, or the vertex nearest the curve sample that is farthest from the reference, whichever gap is larger.
        /// </summary>
        /// <returns>The vertex index (not reduced mod the count), or -1 when the span has no interior vertex.</returns>
        private static int WorstReferenceVertex(IReadOnlyList<Vector2> reference, int iStart, int iEnd, Vector2[] curve, out double worstDistance)
        {
            int n = reference.Count;
            worstDistance = 0;
            int worst = -1;

            for (int i = iStart + 1; i < iEnd; i++)
            {
                double d = DistanceToPolyline(reference[i % n], curve, 0, curve.Length - 1);
                if (d > worstDistance)
                {
                    worstDistance = d;
                    worst = i;
                }
            }

            for (int c = 1; c < curve.Length - 1; c++)
            {
                double best = double.MaxValue;
                for (int i = iStart; i < iEnd; i++)
                {
                    double d = DistanceToSegment(curve[c], reference[i % n], reference[(i + 1) % n]);
                    if (d < best)
                        best = d;
                }

                if (best > worstDistance)
                {
                    worstDistance = best;
                    worst = NearestInteriorVertex(reference, iStart, iEnd, curve[c]);
                }
            }

            return worst;
        }

        private static int NearestInteriorVertex(IReadOnlyList<Vector2> reference, int iStart, int iEnd, Vector2 point)
        {
            int n = reference.Count;
            int nearest = -1;
            double best = double.MaxValue;
            for (int i = iStart + 1; i < iEnd; i++)
            {
                double d = Vector2.DistanceSquared(point, reference[i % n]);
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            return nearest;
        }

        private static double DistanceToPolyline(Vector2 point, Vector2[] polyline, int first, int last)
        {
            double best = double.MaxValue;
            for (int i = first; i < last; i++)
            {
                double d = DistanceToSegment(point, polyline[i], polyline[i + 1]);
                if (d < best)
                    best = d;
            }

            return best;
        }

        internal static double DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double lengthSquared = dx * dx + dy * dy;
            double t = lengthSquared > 0 ? ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared : 0;
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            double ex = a.X + t * dx - p.X;
            double ey = a.Y + t * dy - p.Y;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        public static Polygon Simplify(this Polygon polygon, double MaxDistanceFromSimplifiedToIdeal, uint NumInterpolations = 8)
        {
            List<Vector2> simpleExterior = IdentifyControlPoints(polygon.ExteriorRing,
                MaxDistanceFromSimplifiedToIdeal, true, NumInterpolations);
            Polygon output = new(simpleExterior.ToArray());
            foreach (var innerPoly in polygon.InteriorPolygons)
            {
                Polygon outputInnerPoly =
                    innerPoly.Simplify(MaxDistanceFromSimplifiedToIdeal, NumInterpolations);
                try
                {
                    output.AddInteriorRing(outputInnerPoly);
                }
                catch (ArgumentException)
                {
                    Trace.WriteLine($"The interior polygon with {innerPoly.AllVertices.Length} vertices (before being simplified) could not be added to the simplified outer polygon.  Trying to add unsimplified version instead.");
                    output.AddInteriorRing(innerPoly);
                }
            }

            return output;
        }

        public static Polyline Simplify(this Polyline polyline, double MaxDistanceFromSimplifiedToIdeal, uint NumInterpolations = 8)
        {
            List<Vector2> simpleExterior = IdentifyControlPoints([.. polyline.Points.Select(p => new Vector2(p))], MaxDistanceFromSimplifiedToIdeal, true, NumInterpolations);
            Polyline output = new(simpleExterior);
            return output;
        }

    }
}