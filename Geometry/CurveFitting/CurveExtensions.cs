using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geometry
{
    public static class CurveExtensions
    {
        public static List<GridVector2> CalculateCurvePoints(this GridVector2 ControlPoints, uint NumInterpolations, bool closeCurve)
        {
            return ControlPoints.CalculateCurvePoints(NumInterpolations, closeCurve);
        }

        public static List<GridVector2> CalculateCurvePoints(this ICollection<GridVector2> ControlPoints, uint NumInterpolations, bool closeCurve)
        {
            if (closeCurve)
                return CalculateClosedCurvePoints(ControlPoints, NumInterpolations);
            else
                return CalculateOpenCurvePoints(ControlPoints, NumInterpolations);
        }

        private static List<GridVector2> CalculateClosedCurvePoints(this ICollection<GridVector2> ControlPoints, uint NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            else if (ControlPoints.Count >= 3)
            {
                CurvePoints = Geometry.CatmullRom.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations, true).ToList();
                CurvePoints.Add(CurvePoints.First());
            }

            return CurvePoints;
        }

        private static List<GridVector2> CalculateOpenCurvePoints(this ICollection<GridVector2> ControlPoints, uint NumInterpolations)
        {
            List<GridVector2> CurvePoints = new List<GridVector2>(ControlPoints.Count);
            if (ControlPoints.Count <= 2)
            {
                CurvePoints = new List<GridVector2>(ControlPoints);
            }
            if (ControlPoints.Count >= 3)
            {
                CurvePoints = Geometry.Lagrange.FitCurve(ControlPoints.ToArray(), (int)NumInterpolations * ControlPoints.Count).ToList();
            }

            return CurvePoints;
        }
    }
}
