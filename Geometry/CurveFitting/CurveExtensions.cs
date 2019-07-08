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
            if (NumInterpolations == 0)
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
                foreach (GridVector2 p in ControlPoints)
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
            const double threshold = onedegree * 10.0;
            const double distance_threshold = 0.0625; // Math.Pow(0.25,2);

            int StartingPoints = TPointsArray.Length;
            bool[] NeedsInterpolation = TPointsArray.Select(t => false).ToArray();

            for (int i = TPointsArray.Length - 2; i > 0; i--)
            {
                if (degrees[i] > threshold)
                {
                    double distance = GridVector2.DistanceSquared(output[i - 1], output[i]) + GridVector2.DistanceSquared(output[i], output[i + 1]);
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

        /// <summary>
        /// Uses the Douglas Peucker algorithm to reduce the number of points.
        /// </summary>
        /// <param name="Points">The points.</param>
        /// <param name="Tolerance">The tolerance.</param>
        /// <returns></returns>
        public static List<GridVector2> DouglasPeuckerReduction
            (this List<GridVector2> Points, Double Tolerance)
        {
            if (Points == null || Points.Count < 3)
                return Points;

            Int32 firstPoint = 0;
            Int32 lastPoint = Points.Count - 1;
            List<Int32> pointIndexsToKeep = new List<Int32>();

            //Add the first and last index to the keepers
            pointIndexsToKeep.Add(firstPoint);
            pointIndexsToKeep.Add(lastPoint);

            //The first and the last point cannot be the same
            while (Points[firstPoint].Equals(Points[lastPoint]))
            {
                lastPoint--;
            }

            DouglasPeuckerReduction(Points, firstPoint, lastPoint,
            Tolerance, ref pointIndexsToKeep);

            List<GridVector2> returnPoints = new List<GridVector2>();
            pointIndexsToKeep.Sort();
            foreach (Int32 index in pointIndexsToKeep)
            {
                returnPoints.Add(Points[index]);
            }

            return returnPoints;
        }

        /// <summary>
        /// Douglases the peucker reduction.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="firstPoint">The first point.</param>
        /// <param name="lastPoint">The last point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="pointIndexsToKeep">The point index to keep.</param>
        private static void DouglasPeuckerReduction(List<GridVector2>
            points, Int32 firstPoint, Int32 lastPoint, Double tolerance,
            ref List<Int32> pointIndexsToKeep)
        {
            Double maxDistance = 0;
            Int32 indexFarthest = 0;

            for (Int32 index = firstPoint; index < lastPoint; index++)
            {
                Double distance = PerpendicularDistance
                    (points[firstPoint], points[lastPoint], points[index]);
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

        /// <summary>
        /// The distance of a point from a line made from point1 and point2.
        /// </summary>
        /// <param name="pt1">The PT1.</param>
        /// <param name="pt2">The PT2.</param>
        /// <param name="p">The p.</param>
        /// <returns></returns>
        public static Double PerpendicularDistance
            (GridVector2 Point1, GridVector2 Point2, GridVector2 Point)
        {
            //Area = |(1/2)(x1y2 + x2y3 + x3y1 - x2y1 - x3y2 - x1y3)|   *Area of triangle
            //Base = v((x1-x2)²+(x1-x2)²)                               *Base of Triangle*
            //Area = .5*Base*H                                          *Solve for height
            //Height = Area/.5/Base

            Double area = Math.Abs(.5 * (Point1.X * Point2.Y + Point2.X *
            Point.Y + Point.X * Point1.Y - Point2.X * Point1.Y - Point.X *
            Point2.Y - Point1.X * Point.Y));
            Double bottom = Math.Sqrt(Math.Pow(Point1.X - Point2.X, 2) +
            Math.Pow(Point1.Y - Point2.Y, 2));
            Double height = area / bottom * 2;

            return height;

            //Another option
            //Double A = Point.X - Point1.X;
            //Double B = Point.Y - Point1.Y;
            //Double C = Point2.X - Point1.X;
            //Double D = Point2.Y - Point1.Y;

            //Double dot = A * C + B * D;
            //Double len_sq = C * C + D * D;
            //Double param = dot / len_sq;

            //Double xx, yy;

            //if (param < 0)
            //{
            //    xx = Point1.X;
            //    yy = Point1.Y;
            //}
            //else if (param > 1)
            //{
            //    xx = Point2.X;
            //    yy = Point2.Y;
            //}
            //else
            //{
            //    xx = Point1.X + param * C;
            //    yy = Point1.Y + param * D;
            //}

            //Double d = DistanceBetweenOn2DPlane(Point, new Point(xx, yy));
        }

    }
}
