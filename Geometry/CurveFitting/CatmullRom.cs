using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public class CatmullRom
    {
        private static void PrepareControlPointsForClosedCurve(List<GridVector2> cp)
        {
            if (cp.First() != cp.Last())
            {
                cp.Insert(0, cp.Last());
            }

            cp.AddRange(cp.GetRange(1, 2));
        }

        public static GridVector2[] FitCurve(ICollection<GridVector2> ControlPoints, int NumInterpolations, bool closed)
        {
            List<GridVector2> cp = new List<GridVector2>(ControlPoints);
            if(closed)
                PrepareControlPointsForClosedCurve(cp);

            return cp.Where((p, i) => i + 3 < cp.Count).SelectMany((p, i) => FitCurveSegment(cp[i], cp[i + 1], cp[i + 2], cp[i + 3], NumInterpolations)).ToArray();
        }

        /// <summary>
        /// Returns a curve over a range, does not return the final value which should match the control point
        /// </summary>
        /// <param name="p0"></param>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <param name="NumInterpolations"></param>
        /// <returns></returns>
        public static GridVector2[] FitCurveSegment(GridVector2 p0, GridVector2 p1,
                                                    GridVector2 p2, GridVector2 p3,
                                                    int NumInterpolations)
        {
            double alpha = 0.5;
            double t0 = 0;
            double t1 = tj(t0, p0, p1, alpha);
            double t2 = tj(t1, p1, p2, alpha);
            double t3 = tj(t2, p2, p3, alpha);
            
            double[] tvalues = new double[NumInterpolations];
            tvalues = tvalues.Select((t, i) => t1 + ((double)i / (double)(NumInterpolations)) * (t2- t1)).ToArray();

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

            double[] CX =  tvalues.Select((t, i) => ((t2 - t) / (t2 - t1)) * B1X[i] + ((t - t1) / (t2 - t1)) * B2X[i]).ToArray();
            double[] CY =  tvalues.Select((t, i) => ((t2 - t) / (t2 - t1)) * B1Y[i] + ((t - t1) / (t2 - t1)) * B2Y[i]).ToArray(); 

            return CX.Select((cx, i) => new GridVector2(cx, CY[i])).ToArray();
        }
        
        private static double tj(double ti, GridVector2 Pi, GridVector2 Pj, double Alpha=0.5)
        {
            return Math.Pow(GridVector2.Distance(Pi, Pj), Alpha) + ti;
        }
    }

}
