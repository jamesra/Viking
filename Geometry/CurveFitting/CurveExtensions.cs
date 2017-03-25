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
                GridVector2[] SmoothedCurvePoints = Geometry.CatmullRom.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations, true);
                CurvePoints = new GridVector2[SmoothedCurvePoints.Length + 1];
                SmoothedCurvePoints.CopyTo(CurvePoints, 0);

                //Ensure the first and last point are identical in a closed curve
                CurvePoints[CurvePoints.Length-1] = SmoothedCurvePoints[0];
            }

            return CurvePoints;
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
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations * ControlPoints.Count);
            }

            return CurvePoints;
        }
    }
}
