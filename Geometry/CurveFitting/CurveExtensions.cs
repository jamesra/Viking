using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public static class CurveExtensions
    {
        public static GridVector2[] CalculateCurvePoints(this GridVector2 ControlPoints, uint NumInterpolations, bool closeCurve)
        {
            return ControlPoints.CalculateCurvePoints(NumInterpolations, closeCurve);
        }

        public static GridVector2[] CalculateCurvePoints(this ICollection<GridVector2> ControlPoints, uint NumInterpolations, bool closeCurve)
        {
            if(NumInterpolations == 0)
            {
                return ControlPoints.ToArray();
            }

            if (closeCurve)
                return CalculateClosedCurvePoints(ControlPoints, NumInterpolations);
            else
                return CalculateOpenCurvePoints(ControlPoints, NumInterpolations);
        }

        private static GridVector2[] CalculateClosedCurvePoints(this ICollection<GridVector2> ControlPoints, uint NumInterpolations)
        {
            GridVector2[] CurvePoints = null;
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new GridVector2[ControlPoints.Count];
                ControlPoints.CopyTo(CurvePoints, 0);
            }
            else if (ControlPoints.Count >= 3)
            {
                CurvePoints = Geometry.CatmullRom.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations, true);

                System.Diagnostics.Debug.Assert(CurvePoints[0] == CurvePoints.Last(), "First and last point should be identical in closed curve");
                System.Diagnostics.Debug.Assert(CurvePoints[CurvePoints.Length - 2] != CurvePoints[CurvePoints.Length - 1], "The last and second last points should not match, probable bug.");

                //The SQL Spatial types are more sensitive than our geometry epsilon, so explicitly set the first and last points equal.
                CurvePoints[CurvePoints.Length - 1] = CurvePoints[0];

                //CurvePoints = new GridVector2[SmoothedCurvePoints.Length + 1];
                //SmoothedCurvePoints.CopyTo(CurvePoints, 0);

                //Ensure the first and last point are identical in a closed curve
                //CurvePoints[CurvePoints.Length-1] = SmoothedCurvePoints[0];
            }

            return CurvePoints;
        }

        /// <summary>
        /// Return an array with the curvature of each point in the array
        /// </summary>
        /// <param name="ControlPoints"></param>
        /// <returns></returns>
        public static double[] MeasureCurvature(this IReadOnlyList<GridVector2> ControlPoints)
        {
            //System.Diagnostics.Debug.Assert(ControlPoints.Count == 3, "Curve requires three points to measure");

            double[] Angles = new double[ControlPoints.Count];

            Angles[0] = 0;
            Angles[ControlPoints.Count - 1] = 0; 

            for (int i = 1; i < ControlPoints.Count - 1; i++)
            {
                GridVector2 Origin = ControlPoints[i];
                GridVector2 A = ControlPoints[i - 1];
                GridVector2 B = ControlPoints[i + 1];

                Angles[i] = GridVector2.ArcAngle(Origin, A, B);
            }

            return Angles; 
        }

        private static GridVector2[] CalculateOpenCurvePoints(this ICollection<GridVector2> ControlPoints, uint NumInterpolations)
        {
            GridVector2[] CurvePoints = null;
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new GridVector2[ControlPoints.Count];
                ControlPoints.CopyTo(CurvePoints, 0);
            }
            if (ControlPoints.Count >= 3)
            {
                //CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations * ControlPoints.Count);
                CurvePoints = Geometry.Lagrange.RecursivelyFitCurve(ControlPoints.ToArray());
#if DEBUG
                foreach(GridVector2 p in ControlPoints)
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
        public static bool TryAddTPointsAboveThreshold(GridVector2[] output, ref SortedSet<double> TPoints)
        {
            double[] TPointsArray = TPoints.ToArray();
            double[] degrees = output.MeasureCurvature();

            degrees = degrees.Select(d => Math.Abs((Math.Abs(d) - Math.PI))).ToArray();

            const double onedegree = (Math.PI * 2.0 / 360);
            double threshold = onedegree * 10.0;

            int StartingPoints = TPointsArray.Length;
            bool[] NeedsInterpolation = TPointsArray.Select(t => false).ToArray();

            for (int i = TPointsArray.Length - 2; i > 0; i--)
            {
                if (degrees[i] > threshold)
                {
                    NeedsInterpolation[i] = true;
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
}
