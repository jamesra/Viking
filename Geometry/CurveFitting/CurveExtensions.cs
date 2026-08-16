using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    public static class CurveExtensions
    {
        //private static readonly double CurveSmoothingEpsilon = 1.0;

        public static Vector2[] CalculateCurvePoints(this Vector2[] ControlPoints, uint NumInterpolations, bool closeCurve) => ((ICollection<Vector2>)ControlPoints).CalculateCurvePoints(NumInterpolations, closeCurve);

        public static Vector2[] CalculateCurvePoints(this ICollection<Vector2> ControlPoints, uint NumInterpolations, bool closeCurve)
        {
            if (NumInterpolations == 0)
            {
                return [.. ControlPoints];
            }

            if (closeCurve)
                return CalculateClosedCurvePoints(ControlPoints, NumInterpolations);
            else
                return CalculateOpenCurvePoints(ControlPoints, NumInterpolations);
        }

        public static Polyline CalculateCurvePoints(this Polyline polyline, uint NumInterpolations)
        {
            if (NumInterpolations == 0)
            {
                return polyline;
            }

            return new Polyline(CalculateOpenCurvePoints([.. polyline.Points.Select(p => new Vector2(p.X, p.Y))], NumInterpolations), polyline.AllowsSelfIntersection);
        }

        private static Vector2[] CalculateClosedCurvePoints(this ICollection<Vector2> ControlPoints, uint NumInterpolations)
        {
            Vector2[] CurvePoints = null;
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new Vector2[ControlPoints.Count];
                ControlPoints.CopyTo(CurvePoints, 0);
            }
            else if (ControlPoints.Count >= 3)
            {
                CurvePoints = Geometry.CatmullRom.FitCurve([.. ControlPoints], NumInterpolations, true);

                System.Diagnostics.Debug.Assert(CurvePoints[0] == CurvePoints.Last(), "First and last point should be identical in closed curve");
                System.Diagnostics.Debug.Assert(CurvePoints[CurvePoints.Length - 2] != CurvePoints[CurvePoints.Length - 1], "The last and second last points should not match, probable bug.");

                //The SQL Spatial types are more sensitive than our geometry epsilon, so explicitly set the first and last points equal.
                CurvePoints[CurvePoints.Length - 1] = CurvePoints[0];

                //CurvePoints = new Vector2[SmoothedCurvePoints.Length + 1];
                //SmoothedCurvePoints.CopyTo(CurvePoints, 0);

                //Ensure the first and last point are identical in a closed curve
                //CurvePoints[CurvePoints.Length-1] = SmoothedCurvePoints[0];
            }

            return CurvePoints;
        }

        /*
        /// <summary>
        /// Return an array with the curvature of each point in the array
        /// </summary>
        /// <param name="ControlPoints"></param>
        /// <returns></returns>
        public static double[] MeasureCurvature(this IReadOnlyList<Vector2> ControlPoints)
        {
            //System.Diagnostics.Debug.Assert(ControlPoints.Count == 3, "Curve requires three points to measure");

            double[] Angles = new double[ControlPoints.Count];

            Angles[0] = 0;
            Angles[ControlPoints.Count - 1] = 0;

            for (int i = 1; i < ControlPoints.Count - 1; i++)
            {
                Vector2 Origin = ControlPoints[i];
                Vector2 A = ControlPoints[i - 1];
                Vector2 B = ControlPoints[i + 1];

                Angles[i] = Vector2.ArcAngle(Origin, A, B);
            }

            return Angles;
        }*/


        /// <summary>
        /// Return an array with the curvature of each point in the array.
        /// </summary>
        /// <param name="ControlPoints"></param>
        /// <returns>A list of angles, showing how many degrees the next vertex deviates from travelling from a straight line</returns>
        public static double[] MeasureCurvature(this IList<Vector2> ControlPoints)
        {
            //System.Diagnostics.Debug.Assert(ControlPoints.Count == 3, "Curve requires three points to measure");

            double[] Angles = new double[ControlPoints.Count];

            Angles[0] = 0;
            Angles[ControlPoints.Count - 1] = 0;

            for (int i = 1; i < ControlPoints.Count - 1; i++)
            {
                //Not sure why we have duplicates... consider raising an exception, but consider the angle 0
                if (ControlPoints[i - 1] == ControlPoints[i])
                {
                    throw new ArgumentException("Duplicate points found in MeasureCurvature");
                    //Angles[i] = 0;
                    //continue;
                }
                else if (Vector2.DistanceSquared(ControlPoints[i], ControlPoints[i - 1]) < Tolerance.EpsilonSquared)
                {
                    Angles[i] = 0;
                    continue;
                }

                //Extrapolate a line past the control point, and measure how much we deviate from it
                LineSegment line = new(ControlPoints[i - 1], ControlPoints[i]);
                Vector2 Origin = ControlPoints[i];
                Vector2 A = line.PointAlongLine(2.0);//ControlPoints[i - 1];
                Vector2 B = ControlPoints[i + 1];

                Angles[i] = Vector2.ArcAngle(in Origin, in A, in B);
            }

            return Angles;
        }

        private static Vector2[] CalculateOpenCurvePoints(this ICollection<Vector2> ControlPoints, uint NumInterpolations)
        {
            Vector2[] CurvePoints = null;
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new Vector2[ControlPoints.Count];
                ControlPoints.CopyTo(CurvePoints, 0);
            }
            if (ControlPoints.Count >= 3)
            {
                //CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations * ControlPoints.Count);
                CurvePoints = Geometry.CatmullRom.FitCurve([.. ControlPoints], NumInterpolations, false);
#if DEBUG
                foreach (Vector2 p in ControlPoints)
                {
                    System.Diagnostics.Debug.Assert(CurvePoints.Contains(p));
                }
#endif
            }

            return CurvePoints;
        }

        /// <summary>
        /// Add more TPoints where the angle is too high
        /// </summary>
        /// <param name="TPoints">The positions where we evaluate the curve, from 0 to 1</param>
        /// <returns>False if no points were added</returns>
        public static bool TryAddTPointsAboveThreshold(Vector2[] output, ref SortedSet<double> TPoints, double angleThresholdInDegrees = 10.0)
        {
            //TODO: Remove points where curvature is low 
            double[] TPointsArray = [.. TPoints];

            for (int i = 1; i < output.Length - 1; i++)
            {
                if (Vector2.DistanceSquared(in output[i - 1], in output[i]) < Tolerance.EpsilonSquared ||
                   Vector2.DistanceSquared(in output[i], in output[i + 1]) < Tolerance.EpsilonSquared)
                {
                    output = output.RemoveAt(i);
                    TPoints.Remove(TPointsArray[i]);
                    TPointsArray = TPointsArray.RemoveAt(i);

                    i--;
                }
            }

            double[] degrees;

            degrees = output.MeasureCurvature();
            degrees = [.. degrees.Select(d => Math.Abs(d))];

            const double onedegree = (Math.PI * 2.0 / 360);
            double threshold = onedegree * angleThresholdInDegrees;
            const double distance_threshold = 0.0625; // Math.Pow(0.25,2);

            int StartingPoints = TPointsArray.Length;
            bool[] NeedsInterpolation = [.. TPointsArray.Select(t => false)];

            for (int i = TPointsArray.Length - 2; i > 0; i--)
            {
                if (degrees[i] > threshold)
                {
                    double distance = Vector2.DistanceSquared(in output[i - 1], in output[i]) + Vector2.DistanceSquared(in output[i], in output[i + 1]);
                    NeedsInterpolation[i] = distance > distance_threshold;
                }
            }

            //Add points between any two points where the angle was too large.
            for (int i = 0; i < NeedsInterpolation.Length - 1; i++)
            {
                if (NeedsInterpolation[i] || NeedsInterpolation[i + 1])
                {
                    double nextTValue = (TPointsArray[i] + TPointsArray[i + 1]) / 2.0;
                    TPoints.Add(nextTValue);
                }
            }

            int EndingPoints = TPoints.Count;

            return StartingPoints != EndingPoints;
        }
    }


    public static class CurveSimplificationExtensions
    {
        public static double[] ApplyKernel(this double[] values, double[] kernel)
        {
            Debug.Assert(kernel.Length % 2 == 1); //For now I want odd size kernels
            Debug.Assert(kernel.Sum() == 1.0); //I expect the kernel to sum to 1 so we don't change amplitude of signal.
            int HalfKernelLength = kernel.Length / 2;  //Rounds down
            int iStart = HalfKernelLength;
            int iStop = values.Length - HalfKernelLength;

            double[] window = new double[kernel.Length];

            double[] output = new double[values.Length];

            for (int iCenter = iStart; iCenter < iStop; iCenter++)
            {
                Array.Copy(values, iCenter - HalfKernelLength, window, 0, kernel.Length);

                double updated_value = window.Select((v, i) => v * kernel[i]).Sum();
                output[iCenter] = updated_value;
            }

            for (int i = 0; i < iStart; i++)
            {
                output[i] = values[i];
            }

            for (int i = iStop; i < values.Length; i++)
            {
                output[i] = values[i];
            }

            return output;
        }

        public static double[] TakeDerivative(this double[] input) => [.. input.Select((value, i) => i == 0 ? 0 : value - input[i - 1])];

        public static int[] InflectionPointIndices(this IList<Vector2> input)
        {
            if (input is null)
                return null;

            if (input.Count == 1)
            {
                return [0];
            }
            else if (input.Count < 2)
                return [0, 1];

            Vector2[] points = [.. input];

            double[] angles = points.MeasureCurvature();
            return angles.InflectionPointIndices(); //TODO: Angle is a measure of change, so we should probably take the first derivative instead of a 2nd in InflectionPointIndices
        }


        /// <summary>
        /// Identify the inflection points in a list of values.  Obtained by taking the second derivative and looking for zero-crossings
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static int[] InflectionPointIndices(this double[] input)
        {
            if (input is null)
                return null;

            if (input.Length == 1)
            {
                return [0];
            }
            else if (input.Length < 2)
                return [0, 1];

            double[] first_diff = input.TakeDerivative();
            double[] second_diff = first_diff.TakeDerivative();
            //double[] second_diff = first_diff;

            //Identify all zero-crossings, max/min values in the list of angles 

            SortedSet<int> inflection_points =
            [
                0,
                input.Length - 1
            ];
            int last_sign = 0; //-1, 0, or 1 to indicate direction of change in the last datapoint
            //double total_change = 0;
            //const double one_degree = Math.PI / 180.0;
            for (int iPoint = 0; iPoint < input.Length; iPoint++)
            {
                //total_change += first_diff[iPoint];
                int this_sign = second_diff[iPoint] == 0 ? 0 : second_diff[iPoint] < 0 ? -1 : 1;

                if (this_sign != last_sign)
                {
                    if (last_sign == 0)
                        inflection_points.Add(iPoint - 1);

                    inflection_points.Add(iPoint);
                }

                //}
                //total_change = 0;

                last_sign = this_sign;
            }

            return [.. inflection_points];
        }

        /// <summary>
        /// Uses the Douglas Peucker algorithm to reduce the number of points.
        /// </summary>
        /// <param name="Points">The points.</param>
        /// <param name="Tolerance">The tolerance.</param>
        /// <returns></returns>
        public static List<Vector2> DouglasPeuckerReduction(this IList<Vector2> Points, Double Tolerance, ICollection<Vector2> PointsToPreserve)
        {
            IEnumerable<int> PointsToPreserveIndicies = PointsToPreserve.Where(p => Points.Contains(p)).Select(p => Points.IndexOf(p));

            return DouglasPeuckerReduction(Points, Tolerance, PointsToPreserveIndicies);
        }

        /// <summary>
        /// Uses the Douglas Peucker algorithm to reduce the number of points.
        /// </summary>
        /// <param name="Points">The points.</param>
        /// <param name="Tolerance">The tolerance.</param>
        /// <returns></returns>
        public static List<Vector2> DouglasPeuckerReduction
        (this IList<Vector2> Points, Double Tolerance, IEnumerable<int> PointsToPreserveIndicies = null)
        {
            if (Points is null || Points.Count < 3)
                return [.. Points];

            Int32 firstPoint = 0;
            Int32 lastPoint = Points.Count - 1;
            SortedSet<Int32> pointIndexsToKeep =
            [ 
                //Add the first and last index to the keepers
                firstPoint,
                lastPoint
            ];
            if (PointsToPreserveIndicies != null)
            {
                pointIndexsToKeep.UnionWith(PointsToPreserveIndicies);
            }

            //The first and the last point cannot be the same
            while (Points[firstPoint].Equals(Points[lastPoint]))
            {
                lastPoint--;
            }

            DouglasPeuckerReduction(Points, firstPoint, lastPoint,
            Tolerance, ref pointIndexsToKeep);

            List<Vector2> returnPoints = [];
            foreach (Int32 index in pointIndexsToKeep)
            {
                returnPoints.Add(Points[index]);
            }

            return returnPoints;
        }

        /// <summary>
        /// Douglas Peucker reduction.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="firstPoint">The first point.</param>
        /// <param name="lastPoint">The last point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="pointIndexsToKeep">The point index to keep.</param>
        private static void DouglasPeuckerReduction(IList<Vector2> points, Int32 firstPoint, Int32 lastPoint, Double tolerance, ref SortedSet<Int32> pointIndexsToKeep)
        {
            Double maxDistance = 0;
            Int32 indexFarthest = 0;

            //Reference line 
            LineSegment reference_line = new(points[firstPoint], points[lastPoint]);

            for (Int32 index = firstPoint + 1; index < lastPoint; index++)
            {
                Double distance = reference_line.DistanceToPoint(points[index]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = index;
                }
            }

            if (maxDistance > tolerance && indexFarthest != 0)
            {
                //Add the largest point that exceeds the tolerance
                pointIndexsToKeep.Add(indexFarthest);

                DouglasPeuckerReduction(points, firstPoint,
                indexFarthest, tolerance, ref pointIndexsToKeep);
                DouglasPeuckerReduction(points, indexFarthest,
                lastPoint, tolerance, ref pointIndexsToKeep);
            }
        }
    }
}
