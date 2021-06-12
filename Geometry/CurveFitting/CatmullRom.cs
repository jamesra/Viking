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
        private static List<GridVector2> GetControlPointsForClosedCurve(IList<GridVector2> cp)
        {
            ///
            /// Catmull rom implementation only returns the points between the middle two points of a set of four. 
            /// So if our points are A - B - C - D - E 
            /// We need to input E - A - B - C - D - E - A - B to return a complete curve with the same starting/ending point as the input
            ///

            //Remove any points that are closer than epsilon distanceu04908
            for (int i = 1; i < cp.Count - 1; i++)
            {
                if (GridVector2.DistanceSquared(cp[i - 1], cp[i]) < Global.EpsilonSquared)
                {
                    cp.RemoveAt(i);
                    i = i - 1;
                }
            }

            List<GridVector2> output = cp.ToList();
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

            GridVector2 AfterStart = output[1];
            GridVector2 BeforeStart = output[output.Count - 2];

            //output.AddRange(output.GetRange(1, 2));

            output.Insert(0, BeforeStart);
            output.Add(AfterStart);

            return output;
        }

        /// <summary>
        /// Extrapolate a point using a straight line from the last two points, half the distance between the two control points
        /// </summary>
        /// <param name="cp"></param>
        private static List<GridVector2> GetControlPointsForOpenCurve(IList<GridVector2> cp)
        {
            List<GridVector2> output = cp.ToList();

            GridVector2 zeroPoint = GetStartingPointForOpenCurve(output);
            output.Insert(0, zeroPoint);
            GridVector2 lastPoint = GetEndingPointForOpenCurve(output);
            output.Add(lastPoint);

            return output;
        }


        internal static GridVector2 GetStartingPointForOpenCurve(IReadOnlyList<GridVector2> cp)
        {
            GridLineSegment start = new GridLineSegment(cp[0], cp[1]);
            GridVector2 zeroPoint = start.PointAlongLine(-0.5);
            return zeroPoint;
        }

        internal static GridVector2 GetEndingPointForOpenCurve(IReadOnlyList<GridVector2> cp)
        {
            GridLineSegment end = new GridLineSegment(cp[cp.Count - 2], cp[cp.Count - 1]);
            GridVector2 lastPoint = end.PointAlongLine(1.5);
            return lastPoint;
        }


        public static GridVector2[] FitCurve(IReadOnlyList<GridVector2> ControlPoints, uint NumInterpolations, bool closed)
        {
            //Two points are a straight line, so don't bother interpolating
            if (ControlPoints.Count <= 2 || NumInterpolations == 0)
            {
                return ControlPoints.ToArray();
            }

            List<GridVector2> cp = new List<GridVector2>(ControlPoints);
            if (closed)
                cp = GetControlPointsForClosedCurve(cp);
            else
                cp = GetControlPointsForOpenCurve(cp);

            //return cp.Where((p, i) => i + 3 < cp.Count).SelectMany((p, i) => FitCurveSegment(cp[i], cp[i + 1], cp[i + 2], cp[i + 3], NumInterpolations)).ToArray();
            GridVector2[] points = cp.Where((p, i) => i + 3 < cp.Count).SelectMany((p, i) => RecursivelyFitCurveSegment(cp[i], cp[i + 1], cp[i + 2], cp[i + 3], NumInterpolations: NumInterpolations)).ToArray();
            points = points.RemoveAdjacentDuplicates();

#if DEBUG
            for (int i = 0; i < ControlPoints.Count; i++)
            {
                Debug.Assert(points.Contains(ControlPoints[i]), string.Format("FitCurve output is missing control point #{0} of {1} {2}", i, ControlPoints.Count, ControlPoints[i]));
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
        public static GridVector2[] FitCurveSegment(GridVector2 p0, GridVector2 p1,
                                                    GridVector2 p2, GridVector2 p3,
                                                    int NumInterpolations)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            //double t3 = tj(t2, p2, p3, alpha); //TODO: Check why this is calculated but not used

            double[] tvalues = new double[NumInterpolations];

            double[] tPointsArray;
            if (NumInterpolations == 1)
            {
                tPointsArray = new double[] { 0.5 };
            }
            else
            {
                tPointsArray = tvalues.Select((t, i) => ((double)i / ((double)NumInterpolations - 1.0))).ToArray();
            }

            tvalues = tPointsArray.Select((t, i) => t1 + tPointsArray[i] * (t2 - t1)).ToArray();

            GridVector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);
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
        public static GridVector2[] FitCurveSegment(GridVector2 p0, GridVector2 p1,
                                                    GridVector2 p2, GridVector2 p3,
                                                    double[] tPointsArray)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            //double t3 = tj(t2, p2, p3, alpha); //TODO: Check why this is calculated but not used

            double[] tvalues = TScalarsToTValues(tPointsArray, t1, t2);

            GridVector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);
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
        public static GridVector2[] FitCurveSegment(IReadOnlyList<GridVector2> Points, int iStart, double[] tpointsArray)
        {
            //Cannot add a curve to a straight line
            if (Points.Count < 2)
                return Points.ToArray();

            if (iStart >= Points.Count - 1)
                throw new ArgumentException($"Starting point {iStart} is the last point in the Points array and a curve cannot be calculated.");

            GridVector2 p0, p1, p2, p3;

            p0 = iStart - 1 >= 0 ? Points[iStart - 1] : GetStartingPointForOpenCurve(Points);
            p1 = Points[iStart];
            p2 = Points[iStart + 1];
            p3 = iStart + 2 < Points.Count ? Points[iStart + 2] : GetEndingPointForOpenCurve(Points);

            return FitCurveSegment(p0,p1,p2,p3, tpointsArray);
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
        public static GridVector2[] FitCurveSegment(IReadOnlyList<IPoint2D> Points, int iStart, double[] tpointsArray)
        {
            return FitCurveSegment(Points.Select(p => p.ToGridVector2()).ToArray(), iStart, tpointsArray);
        }

        /// <summary>
        /// Returns a curve over a range, does not return the final value which should match the control point
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="tvalues">The fractional distances between P1 and P2 that we would like returned</param>
        /// <returns></returns>
        private static GridVector2[] FitCurveSegmentWithTValues(GridVector2 p0, GridVector2 p1,
                                                    GridVector2 p2, GridVector2 p3,
                                                    double[] tvalues)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            double t3 = tj(t2, p2, p3, alpha);

            double[] A1X = tvalues.Select(t => (t1 - t) / (t1 - t0) * p0.X + (t - t0) / (t1 - t0) * p1.X).ToArray();
            double[] A1Y = tvalues.Select(t => (t1 - t) / (t1 - t0) * p0.Y + (t - t0) / (t1 - t0) * p1.Y).ToArray();

            double[] A2X = tvalues.Select(t => (t2 - t) / (t2 - t1) * p1.X + (t - t1) / (t2 - t1) * p2.X).ToArray();
            double[] A2Y = tvalues.Select(t => (t2 - t) / (t2 - t1) * p1.Y + (t - t1) / (t2 - t1) * p2.Y).ToArray();

            double[] A3X = tvalues.Select(t => (t3 - t) / (t3 - t2) * p2.X + (t - t2) / (t3 - t2) * p3.X).ToArray();
            double[] A3Y = tvalues.Select(t => (t3 - t) / (t3 - t2) * p2.Y + (t - t2) / (t3 - t2) * p3.Y).ToArray();

            double[] B1X = tvalues.Select((t, i) => ((t2 - t) / (t2 - t0)) * A1X[i] + ((t - t0) / (t2 - t0)) * A2X[i]).ToArray();
            double[] B1Y = tvalues.Select((t, i) => ((t2 - t) / (t2 - t0)) * A1Y[i] + ((t - t0) / (t2 - t0)) * A2Y[i]).ToArray();

            double[] B2X = tvalues.Select((t, i) => ((t3 - t) / (t3 - t1)) * A2X[i] + ((t - t1) / (t3 - t1)) * A3X[i]).ToArray();
            double[] B2Y = tvalues.Select((t, i) => ((t3 - t) / (t3 - t1)) * A2Y[i] + ((t - t1) / (t3 - t1)) * A3Y[i]).ToArray();

            double[] CX = tvalues.Select((t, i) => ((t2 - t) / (t2 - t1)) * B1X[i] + ((t - t1) / (t2 - t1)) * B2X[i]).ToArray();
            double[] CY = tvalues.Select((t, i) => ((t2 - t) / (t2 - t1)) * B1Y[i] + ((t - t1) / (t2 - t1)) * B2Y[i]).ToArray();

            return CX.Select((cx, i) => new GridVector2(cx, CY[i])).ToArray();
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
        /// <param name="tPoints">If null then [NumInterpolations] points are evenly spaced along the curve </param>
        /// <param name="NumInterpolations"></param>
        /// <returns></returns>
        public static GridVector2[] RecursivelyFitCurveSegment(GridVector2 p0, GridVector2 p1,
                                                    GridVector2 p2, GridVector2 p3,
                                                    uint NumInterpolations = 5)
        {
            double[] tvalues;
            SortedSet<double> tPoints;
            tvalues = new double[NumInterpolations];
            tPoints = new SortedSet<double>(tvalues.Select((t, i) => ((double)i / ((double)NumInterpolations - 1.0))));

            return RecursivelyFitCurveSegment(p0, p1, p2, p3, tPoints);
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
        public static GridVector2[] RecursivelyFitCurveSegment(GridVector2 p0, GridVector2 p1,
                                                    GridVector2 p2, GridVector2 p3,
                                                    SortedSet<double> tPoints)
        { 
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            double t3 = tj(t2, p2, p3, alpha);

            double[] tPointsArray = tPoints.ToArray();
            double[] tvalues = TScalarsToTValues(tPoints, t1, t2);

            GridVector2[] output = FitCurveSegmentWithTValues(p0, p1, p2, p3, tvalues);

            if (!CurveExtensions.TryAddTPointsAboveThreshold(output, ref tPoints))
            {
                //We could not add any additional T points at high curvature regions, return the result
                return output;
            }

            return RecursivelyFitCurveSegment(p0, p1, p2, p3, tPoints);
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
            return TScalarsToTValues(tpoints.ToArray(), t1, t2);
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
            double[] tvalues = tpoints.Select((t, i) => t1 + tpoints[i] * (t2 - t1)).ToArray();
            return tvalues;
        }

        private static double tj(double ti, GridVector2 Pi, GridVector2 Pj, double Alpha = 0.5)
        {
            return Math.Pow(GridVector2.Distance(Pi, Pj), Alpha) + ti;
        }
    }

    /// <summary>
    /// Contains functions to identify a small set of control points required to approximate a curved path
    /// </summary>
    public static class CatmullRomControlPointSimplification
    {
        static private GridVector2[] GetControlPointSubsetForCurve(IReadOnlyList<GridVector2> path, int iStart, bool IsClosed)
        {
            return IsClosed ? GetControlPointSubsetForClosedCurve(path, iStart) : GetControlPointSubsetForOpenCurve(path, iStart);
        }

        /// <summary>
        /// Returns the four control points we can feed into CatmullRom to obtain the curve from iStart to iStart + 1
        /// </summary>
        /// <param name="path"></param>
        /// <param name="iStart"></param>
        /// <returns></returns>
        static private GridVector2[] GetControlPointSubsetForOpenCurve(IReadOnlyList<GridVector2> path, int iStart)
        {
            if (path.Count < 2)
            {
                throw new ArgumentException("Cannot generate control points for a straight line path with only two verticies.");
            }

            //CatmullRom will generate the curve between points B & C (1 & 2) accurately, needed control points will be inserted there
            int[] ProposedControlPointIndicies = new int[] {iStart - 1,
                                                                iStart,
                                                                iStart + 1,
                                                                iStart + 2};

            GridVector2[] ProposedCurveControlPoints = new GridVector2[4];
            for (int iV = 0; iV < ProposedControlPointIndicies.Length; iV++)
            {
                if (ProposedControlPointIndicies[iV] < 0)
                {
                    ProposedCurveControlPoints[iV] = CatmullRom.GetStartingPointForOpenCurve(path);
                }
                else if (ProposedControlPointIndicies[iV] >= path.Count)
                {
                    ProposedCurveControlPoints[iV] = CatmullRom.GetEndingPointForOpenCurve(path);
                }
                else
                {
                    ProposedCurveControlPoints[iV] = path[ProposedControlPointIndicies[iV]];
                }
            }

            return ProposedCurveControlPoints;
        }

        /// <summary>
        /// Returns the four control points we can feed into CatmullRom to obtain the curve from iStart to iStart + 1
        /// </summary>
        /// <param name="path"></param>
        /// <param name="iStart"></param>
        /// <returns></returns>
        static private GridVector2[] GetControlPointSubsetForClosedCurve(IReadOnlyList<GridVector2> path, int iStart)
        {
            if (path.Count < 3)
            {
                throw new ArgumentException("Cannot generate control points for a closed curve path with two or fewer verticies.");
            }

            //CatmullRom will generate the curve between points B & C (1 & 2) accurately, needed control points will be inserted there
            int[] ProposedControlPointIndicies = new int[] {iStart - 1,
                                                                iStart,
                                                                iStart + 1,
                                                                iStart + 2};

            GridVector2[] ProposedCurveControlPoints = new GridVector2[4];
            for (int iV = 0; iV < ProposedControlPointIndicies.Length; iV++)
            {
                if (ProposedControlPointIndicies[iV] < 0)
                {
                    ProposedCurveControlPoints[iV] = path[path.Count - 2];
                }
                else if (ProposedControlPointIndicies[iV] >= path.Count)
                {
                    ProposedCurveControlPoints[iV] = path[1];
                }
                else
                {
                    ProposedCurveControlPoints[iV] = path[ProposedControlPointIndicies[iV]];
                }
            }


            //IIndexSet path_indicies = new Geometry.InfiniteWrappedIndexSet(0, path.Count-1, 0);
            //return ProposedControlPointIndicies.Select(i => path_indicies[i]).Select(i => path[(int)i]).ToArray();

            return ProposedCurveControlPoints;
        }

        private static List<GridVector2> GenerateStartingSimplifiedLine(this IList<GridVector2> path, bool IsClosed)
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
        private static List<GridVector2> GenerateStartingSimplifiedOpenLine(this IList<GridVector2> path)
        {
            return new List<GridVector2>(new GridVector2[] { path.First(), path.Last() });
        }

        /// <summary>
        /// CatmullRom requires four points to describe a curve. 
        /// When fitting an closed line we use the first/last point as starting and stopping points and the points with the largest change in angle to fill in.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static List<GridVector2> GenerateStartingSimplifiedClosedLine(this IList<GridVector2> path)
        {
            //Find the two largest changes in angles in the input path
            double[] angle_change = path.ToArray().MeasureCurvature().TakeDerivative();
            int[] iSorted = angle_change.SortAndIndex();

            iSorted = iSorted.Where(i => i != 0 && i != path.Count - 1).ToArray(); //Remove the first/last vertex because we know we are including it. 

            int[] firstTwo = new int[] { iSorted[iSorted.Length - 1], iSorted[iSorted.Length - 2] };
            if (firstTwo[0] > firstTwo[1])
            {
                firstTwo = firstTwo.Reverse().ToArray();
            }

            return new List<GridVector2>(new GridVector2[] { path.First(), path[firstTwo[0]], path[firstTwo[1]], path.Last() });
        }

        /// <summary>
        /// Take a high density path, fit a curve to it using catmull rom, and remove control points until we have a smaller number of control points where all points are within a minimum distance from the curve. 
        /// </summary>
        /// <param name="path"></param>
        static public List<GridVector2> IdentifyControlPoints(this IReadOnlyList<GridVector2> input, double MaxDistanceFromSimplifiedToIdeal, bool IsClosed, uint NumInterpolations = 8)
        {
            //Copy the path so we don't modify the input
            var path = input.ToList();

            //We can't simplify the already simple...
            if (path == null || path.Count <= 2)
                return path;

            if (path.Count <= 4 && IsClosed)
                return path;

            if (IsClosed && path.First() != path.Last())
            {
                path.Add(path.First());
            }

            GridVector2[] curved_path = Geometry.CatmullRom.FitCurve(path, NumInterpolations, IsClosed);
            GridLineSegment[] curve_segments = curved_path.ToLineSegments();
            Dictionary<GridVector2, int> point_to_ideal_curve_index = new Dictionary<GridVector2, int>(curved_path.Length);
            for (int i = 0; i < curved_path.Length; i++)
            {
                if (IsClosed && i == curved_path.Length - 1)
                    continue; //Skip the last point which is a duplicate in a closed curve

                if (point_to_ideal_curve_index.ContainsKey(curved_path[i]))
                    continue;

                point_to_ideal_curve_index.Add(curved_path[i], i);
            }

#if DEBUG
            for (int i = 0; i < path.Count; i++)
            {
                Debug.Assert(point_to_ideal_curve_index.ContainsKey(path[i]), string.Format("Ideal curve dictionary is missing path point #{0} {1}", i, path[i]));
            }
#endif

            //int[] inflectionIndicies = curved_path.InflectionPointIndicies();
            //GridVector2[] inflectionPoints = inflectionIndicies.Select(i => curved_path[i]).ToArray();
            //List<GridVector2> simplified_inflection_points = inflectionPoints.DouglasPeuckerReduction(Tolerance: MaxDistanceFromSimplifiedToIdeal * 10);
            List<GridVector2> simplified_inflection_points = GenerateStartingSimplifiedLine(path, IsClosed);

            //Walk subsets of our proposed simplified curve, compare distance to ideal curve, add new control points as needed until distance is below threshold.
            int iProposedVertex = 0;
            while (iProposedVertex < simplified_inflection_points.Count - 1)
            {
                GridVector2[] proposedCurveControlPoints = null;
                if (simplified_inflection_points.Count >= 2)
                {
                    proposedCurveControlPoints = GetControlPointSubsetForCurve(simplified_inflection_points, iProposedVertex, IsClosed);
                }
                else
                {
                    proposedCurveControlPoints = simplified_inflection_points.ToArray();
                }

                GridVector2[] proposedCurve = CatmullRom.RecursivelyFitCurveSegment(proposedCurveControlPoints[0],
                                                                                    proposedCurveControlPoints[1],
                                                                                    proposedCurveControlPoints[2],
                                                                                    proposedCurveControlPoints[3],
                                                                                    NumInterpolations);

                //Find the subset of the real curve this proposed curve should accurately represent
                //identify the segment on the ideal curve we are comparing against

                if (false == point_to_ideal_curve_index.ContainsKey(proposedCurve.First()))
                {
                    iProposedVertex = iProposedVertex + 1;
                    continue;
                }

                if (false == point_to_ideal_curve_index.ContainsKey(proposedCurve.Last()))
                {
                    iProposedVertex = iProposedVertex + 1;
                    continue;
                }

                int iIdealStart = point_to_ideal_curve_index[proposedCurve.First()];
                int iIdealEnd = point_to_ideal_curve_index[proposedCurve.Last()];

                //I believe this is an impossible case, but checking anyway for debugging. 
                //If the loop is closed the final vertex could be the first vertex of the loop, so we don't check
                if (IsClosed == false)
                {
                    System.Diagnostics.Debug.Assert(iIdealEnd > iIdealStart);
                    if (iIdealEnd - iIdealStart <= 0)
                        continue;
                }
                else
                {
                    if (iIdealEnd <= iIdealStart) //We are using the first/last vertex
                    {
                        System.Diagnostics.Debug.Assert(iIdealEnd == 0);
                        iIdealEnd = curved_path.Length - 1;
                    }
                }

                //InfiniteWrappedIndexSet indicies = new InfiniteWrappedIndexSet(0, curve_segments.Length-1, iIdealStart);

                //Copy the relevant part of the ideal curve into a smaller array to narrow our search
                int num_ideal_segments = (iIdealEnd) - iIdealStart; // -1 to account for the index into a line array vs a point array
                GridLineSegment[] ideal_segments = new GridLineSegment[num_ideal_segments];
                System.Array.Copy(curve_segments, iIdealStart, ideal_segments, 0, num_ideal_segments);

                //If we need to add a control point repeat this loop iteration, otherwise increment and check the next portion of the curve
                GridVector2 ControlPointToAdd;
                if (TryFindOutlierControlPoint(ideal_segments, proposedCurve, MaxDistanceFromSimplifiedToIdeal, out ControlPointToAdd))
                {
                    Debug.Assert(simplified_inflection_points.Contains(ControlPointToAdd) == false);
#if DEBUG
                    if (simplified_inflection_points.Contains(ControlPointToAdd))
                    {
                        iProposedVertex = iProposedVertex + 1;
                        continue;
                    }
#endif 
                    simplified_inflection_points.Insert(iProposedVertex + 1, ControlPointToAdd);
                }
                else
                {
                    iProposedVertex = iProposedVertex + 1;
                }
            }

            if (IsClosed)
            {
                Debug.Assert(simplified_inflection_points.First() == simplified_inflection_points.Last());

                //simplified_inflection_points.Add(simplified_inflection_points[0]); //Close the ring
            }

            return simplified_inflection_points;
        }

        private static bool TryFindOutlierControlPoint(GridLineSegment[] ideal_path, GridVector2[] proposed_path, double MaxDistanceFromProposedToIdeal, out GridVector2 ControlPointToAdd)
        {
            double MaxDistance = 0;
            int iMaxSegment = -1; //The line segment on the ideal path that is closest to iMaxProposedVertex
            int iMaxProposedVertex = -1; //THe proposed vertex that is furthest from the ideal path
            ControlPointToAdd = GridVector2.Zero;

            GridLineSegment[] proposed_segments = proposed_path.ToLineSegments();

            for (int i = 0; i < ideal_path.Length - 1; i++)
            {
                int iNearestSegment = proposed_segments.NearestSegment(ideal_path[i].B, out double MinDistance);
                if (MinDistance > MaxDistance)
                {
                    iMaxProposedVertex = i;
                    iMaxSegment = iNearestSegment;
                    MaxDistance = MinDistance;
                }
            }

            if (MaxDistance > MaxDistanceFromProposedToIdeal)
            {
                /*
                GridLineSegment nearestIdeal = ideal_path[iMaxSegment];
                nearestIdeal.DistanceToPoint(proposed_path[iMaxProposedVertex], out GridVector2 NearestPointOnLine);
                //Identify which end of the line is closest to the nearest point on the line
                ControlPointToAdd = GridVector2.DistanceSquared(nearestIdeal.A, NearestPointOnLine) < GridVector2.DistanceSquared(nearestIdeal.B, NearestPointOnLine) ? nearestIdeal.A : nearestIdeal.B;
                */
                ControlPointToAdd = ideal_path[iMaxProposedVertex].B;
                return true;
            }

            return false;
        }

        public static GridPolygon Simplify(this GridPolygon polygon, double MaxDistanceFromSimplifiedToIdeal, uint NumInterpolations = 8)
        {
            List<GridVector2> simpleExterior = IdentifyControlPoints(polygon.ExteriorRing, MaxDistanceFromSimplifiedToIdeal, true, NumInterpolations);
            GridPolygon output = new GridPolygon(simpleExterior.ToArray());
            foreach (var innerPoly in polygon.InteriorPolygons)
            {
                GridPolygon outputInnerPoly = innerPoly.Simplify(MaxDistanceFromSimplifiedToIdeal, NumInterpolations);
                output.AddInteriorRing(outputInnerPoly);
            }

            return output;
        }

        public static GridPolyline Simplify(this GridPolyline polyline, double MaxDistanceFromSimplifiedToIdeal, uint NumInterpolations = 8)
        {
            List<GridVector2> simpleExterior = IdentifyControlPoints(polyline.Points.Select(p => new GridVector2(p)).ToList(), MaxDistanceFromSimplifiedToIdeal, true, NumInterpolations);
            GridPolyline output = new GridPolyline(simpleExterior); 
            return output;
        }

    }
}