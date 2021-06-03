using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Geometry
{
    /// <summary>
    /// Handles fitting a Lagrange curve by interpolation along a set of control points
    /// </summary>
    public class Lagrange
    {
        public static GridVector2[] RecursivelyFitCurve(GridVector2[] cp, SortedSet<double> TPoints = null)
        {
            int nPoints = cp.Length;
            int NumInterpolations;

            double[] TValues = cp.Select((p, i) => (double)i / ((double)nPoints - 1)).ToArray();
            double[] XValues = cp.Select(p => p.X).ToArray();
            double[] YValues = cp.Select(p => p.Y).ToArray();

            //Linearly space space the t values along the array
            if (TPoints == null)
            {
                TPoints = GenerateTPoints(TValues, TValues.Length * 2);
            }

            NumInterpolations = TPoints.Count;

            double[] TPointsArray = new double[NumInterpolations];
            TPointsArray = TPoints.ToArray();

            double[] XOutput = ValuesAtTPoints(TPointsArray, TValues, XValues);
            double[] YOutput = ValuesAtTPoints(TPointsArray, TValues, YValues);

            GridVector2[] output = XOutput.Select((x, i) => new GridVector2(x, YOutput[i])).ToArray();

#if DEBUG
            foreach (GridVector2 p in cp)
            {
                Debug.Assert(output.Contains(p));
            }
#endif

            if (!CurveExtensions.TryAddTPointsAboveThreshold(output, ref TPoints))
                return output;

            return RecursivelyFitCurve(cp, TPoints).RemoveAdjacentDuplicates();
        }


        /// <summary>
        /// Return TPoints for the control points and points spaced evenly along the interval of 0 to 1
        /// </summary>
        /// <param name="cp"></param>
        /// <param name="NumInterpolations"></param>
        /// <returns></returns>
        private static SortedSet<double> GenerateTPoints(double[] ControlPointTValues, int NumInterpolations)
        {
            //The TValues for control points
            double[] TValues = ControlPointTValues;//ControlPointTValues.Select((p, i) => (double)i / ((double)ControlPointTValues.Length - 1)).ToArray();
            double[] TPointsInterpolated = new double[NumInterpolations];

            //The TValues for interpolated points
            TPointsInterpolated = TPointsInterpolated.Select((t, i) => (double)i / (double)(NumInterpolations - 1)).ToArray();
            SortedSet<double> TPoints = new SortedSet<double>(TPointsInterpolated);

            //Add the points at the actual control points
            TPoints.UnionWith(TValues);
            return TPoints;
        }


        /// <summary>
        /// Return points along a curve described by three points
        /// </summary>
        /// <param name="points"></param>
        public static GridVector2[] FitCurve(GridVector2[] cp, int NumInterpolations)
        {
            int nPoints = cp.Length;
            Debug.Assert(nPoints >= 3);
            //Linearly space space the t values along the array
            double[] TValues = cp.Select((p, i) => (double)i / ((double)nPoints - 1)).ToArray();
            double[] XValues = cp.Select(p => p.X).ToArray();
            double[] YValues = cp.Select(p => p.Y).ToArray();

            SortedSet<double> TPoints = GenerateTPoints(TValues, NumInterpolations);

            double[] TPointsArray = TPoints.ToArray();

            double[] XOutput = ValuesAtTPoints(TPointsArray, TValues, XValues);
            double[] YOutput = ValuesAtTPoints(TPointsArray, TValues, YValues);

            GridVector2[] output = XOutput.Select((x, i) => new GridVector2(x, YOutput[i])).ToArray();

            return output;
        }

        private static double[] ValuesAtTPoints(double[] TPoints, IReadOnlyList<double> TArray, double[] InputValues)
        {
            Debug.Assert(TArray.Count == InputValues.Length);
            int nPoints = TArray.Count;

            double[] Product = new double[TPoints.Length];
            Product = Product.Select(v => 0.0).ToArray();

            for (int j = 0; j < nPoints; j++)
            {
                double[] Weights = TPoints.Select((t, iT) => WeightForT(t, TArray, j) * InputValues[j]).ToArray();
                Product = Product.Select((p, i) => Weights[i] + Product[i]).ToArray();
            }

            return Product;
        }


        /// <summary>
        /// Returns the weight at point T along a 1D line. 
        /// </summary>
        /// <param name="T"></param>
        /// <param name="TArray">1-D distance along curve as a 0 to 1.0 scalar</param>
        /// <param name="InputValues"></param>
        /// <returns></returns>
        private static double WeightForT(double T, IReadOnlyList<double> TArray, int j)
        {
            double Numerator = TArray.Select((t, k) => T - TArray[k]).Where((t, k) => j != k).Aggregate(1.0, (prod, next) => prod * next);
            double Denominator = TArray.Select((t, k) => TArray[j] - TArray[k]).Where((t, k) => j != k).Aggregate(1.0, (prod, next) => prod * next);

            if (Denominator == 0)
                return 0;

            return Numerator / Denominator;
        }
    }
}
