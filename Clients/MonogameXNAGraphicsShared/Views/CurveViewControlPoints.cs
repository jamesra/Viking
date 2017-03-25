using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;

namespace VikingXNAGraphics
{

    public class CurveViewControlPoints
    {
        public CurveViewControlPoints(ICollection<GridVector2> cps, uint NumInterpolations, bool TryToClose)
        {
            if (cps.Count < 2)
            {
                throw new ArgumentException("Cannot create a curve with fewer than two control points");
            }
            this._NumInterpolations = NumInterpolations;
            this._TryCloseCurve = TryToClose;

            if (TryCloseCurve && cps.Count > 2)
                this.ControlPoints = cps.ToArray().AreClockwise() ? cps.Reverse().ToArray() : cps.ToArray();
            else
                this.ControlPoints = ReverseControlPointsIfTextUpsideDown(cps);
        }

        private static GridVector2[] ReverseControlPointsIfTextUpsideDown(ICollection<GridVector2> cps)
        {
            if (cps.First().X > cps.Last().X)
            {
                return cps.Reverse().ToArray();
            }

            return cps.ToArray();
        }

        /// <summary>
        /// Try to close the curve if we have enough control points
        /// </summary>
        private bool _TryCloseCurve;
        public bool TryCloseCurve
        {
            get { return _TryCloseCurve; }
            set
            {
                if (_TryCloseCurve != value)
                {
                    _TryCloseCurve = value;
                    RecalculateCurvePoints();
                }

            }
        }

        private GridVector2[] _ControlPoints;

        /// <summary>
        /// In a closed curve the control points are not looped, the first and last control points should be different
        /// </summary>
        public GridVector2[] ControlPoints
        {
            get { return _ControlPoints; }
            set
            {
                _ControlPoints = value;
                while (_ControlPoints[0] == _ControlPoints[_ControlPoints.Length - 1])
                {
                    _ControlPoints = RemoveLastEntry(_ControlPoints);
                }

                RecalculateCurvePoints();
            }
        }

        private GridVector2[] _CurvePoints;
        public GridVector2[] CurvePoints
        {
            get { return _CurvePoints; }
        }

        /// <summary>
        /// Return the interpolated points between the two control point indicies
        /// </summary>
        /// <param name="iStart"></param>
        /// <param name="iEnd"></param>
        /// <returns></returns>
        public GridVector2[] CurvePointsBetweenControlPoints(int? iStart, int? iEnd)
        {
            if (!iStart.HasValue)
                iStart = 0;
            if (!iEnd.HasValue)
                iEnd = ControlPoints.Length - 1;

            int iCurveStart = iStart.Value * (int)_NumInterpolations;
            int iCurveEnd = iEnd.Value * (int)_NumInterpolations;

            if (iCurveStart > iCurveEnd)
                throw new ArgumentException("Start index greater than end index");

            GridVector2[] destArray = new GridVector2[iCurveEnd - iCurveStart];

            Array.Copy(_CurvePoints, iCurveStart, destArray, 0, destArray.Length);
            return destArray;
        }

        private uint _NumInterpolations = 1;
        public uint NumInterpolations
        {
            get { return _NumInterpolations; }
            set
            {
                if (value != _NumInterpolations)
                {
                    _NumInterpolations = value;
                    RecalculateCurvePoints();
                }
            }
        }

        public void SetPoint(int i, GridVector2 value)
        {
            _ControlPoints[i] = value;
            RecalculateCurvePoints();
        }

        private static GridVector2[] RemoveLastEntry(GridVector2[] array)
        {
            GridVector2[] cps = new GridVector2[array.Length - 1];
            Array.Copy(array, cps, array.Length - 1);
            return cps;
        }
         
        public void RecalculateCurvePoints()
        {
            this._CurvePoints = this._ControlPoints.CalculateCurvePoints(this._NumInterpolations, this._TryCloseCurve).ToArray();
        }
    }
}
