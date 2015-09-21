using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    /// <summary>
    /// Handles fitting a Lagrange curve by interpolation along a set of control points
    /// </summary>
    public class Lagrange
    {
        /// <summary>
        /// Return points along a curve described by three points
        /// </summary>
        /// <param name="points"></param>
        public static GridVector2[] FitCurve(GridVector2[] cp, uint NumInterpolations)
        {
            int nPoints = cp.Length;
            Debug.Assert(nPoints >= 3);
            //Linearly space space the t values along the array
            double[] TValues = cp.Select((p, i) => (double)i / ((double)nPoints-1)).ToArray();
            double[] XValues = cp.Select(p => p.X).ToArray();
            double[] YValues = cp.Select(p => p.Y).ToArray();

            double[] TPoints = new double[NumInterpolations];
            TPoints = TPoints.Select((t,i) => (double)i / (double)(NumInterpolations-1)).ToArray();

            double[] XOutput = ValuesAtTPoints(TPoints, TValues, XValues);
            double[] YOutput = ValuesAtTPoints(TPoints, TValues, YValues);

            return XOutput.Select((x, i) => new GridVector2(x, YOutput[i])).ToArray();
        }

        private static double[] ValuesAtTPoints(double[] TPoints, double[] TArray, double[] InputValues)
        {
            Debug.Assert(TArray.Length == InputValues.Length);
            int nPoints = TArray.Length;

            double[] Product = new double[TPoints.Length];
            Product = Product.Select(v => 0.0).ToArray();

            for (int j = 0; j < nPoints; j++)
            {
                double[] Weights = TPoints.Select((t, iT) => WeightForT(t, TArray, j) * InputValues[j]).ToArray();
                Product = Product.Select((p, i) => Weights[i] + Product[i]).ToArray();
            }

            return Product.ToArray();
        }
    

        /// <summary>
        /// Returns the weight at point T along a 1D line. 
        /// </summary>
        /// <param name="T"></param>
        /// <param name="TArray">1-D distance along curve as a 0 to 1.0 scalar</param>
        /// <param name="InputValues"></param>
        /// <returns></returns>
        private static double WeightForT(double T, double[] TArray, int j)
        {
            int nPoints = TArray.Length;
            double Numerator = TArray.Select((t,k) => T - TArray[k]).Where((t, k) => j != k).Aggregate(1.0, (prod, next) => prod * next);
            double Denominator = TArray.Select((t,k) => TArray[j] - TArray[k]).Where((t, k) => j != k).Aggregate(1.0, (prod, next) => prod * next);

            if (Denominator == 0)
                return 0;

            return Numerator / Denominator; 
        } 
    }
}
