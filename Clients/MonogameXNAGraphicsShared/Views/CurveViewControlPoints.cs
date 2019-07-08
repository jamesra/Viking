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
        /// <summary>
        /// Set to true if the order of control points was reversed during processing
        /// </summary>
        /// 
        bool ReversedOrder = false;
        public CurveViewControlPoints(ICollection<GridVector2> cps, uint NumInterpolations, bool TryToClose)
        {
            if (cps.Count < 2)
            {
                throw new ArgumentException("Cannot create a curve with fewer than two control points");
            }
            else if(cps.Count == 2 && TryToClose)
            {
                throw new ArgumentException("Cannot close a curve with only two points");
            }

            this._NumInterpolations = NumInterpolations;
            this._TryCloseCurve = TryToClose;

            if (NumInterpolations == 0)
            {
                this.ControlPoints = cps.ToArray();
            }
            
            if (TryCloseCurve && cps.Count > 2)
            {
                bool Reverse = cps.ToArray().AreClockwise();
                ReversedOrder = Reverse;
                this.ControlPoints = Reverse ? cps.Reverse().ToArray() : cps.ToArray();
            }
            else
                this.ControlPoints = ReverseControlPointsIfTextUpsideDown(cps, out ReversedOrder);
        }

        private static GridVector2[] ReverseControlPointsIfTextUpsideDown(ICollection<GridVector2> cps, out bool Reversed)
        {
            Reversed = false;

            if (cps.First().X > cps.Last().X)
            {
                Reversed = true;
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

            bool EndAtLastVertex = false;
            while(iEnd.Value >= ControlPoints.Length)
            {
                EndAtLastVertex = true;
                iEnd -= ControlPoints.Length;
            }

            GridVector2 startControlPoint = ControlPoints[iStart.Value];
            GridVector2 endControlPoint = ControlPoints[iEnd.Value];

            // int iCurveStart = iStart.Value * (int)_NumInterpolations;
            // int iCurveEnd = iEnd.Value * (int)_NumInterpolations;

            int iCurveStart = FindIndex(_CurvePoints, startControlPoint);
            int iCurveEnd = FindIndex(_CurvePoints, endControlPoint);

            if(EndAtLastVertex)
            {
                iCurveEnd = _CurvePoints.Length;
            }

            if (iCurveStart > iCurveEnd)
                throw new ArgumentException("Start index greater than end index");

            GridVector2[] destArray = new GridVector2[iCurveEnd - iCurveStart];

            Array.Copy(_CurvePoints, iCurveStart, destArray, 0, destArray.Length);
            return destArray;
        }

        /// <summary>
        /// Return the interpolated points between the two control point indicies
        /// </summary>
        /// <param name="iStart"></param>
        /// <param name="iEnd"></param>
        /// <returns></returns>
        public GridVector2[] CurvePointsBetweenControlPoints(GridVector2 startControlPoint, GridVector2 endControlPoint)
        {
            //If we reversed the order of the input array we need to reverse the start and end points
            GridVector2[] Points = new GridVector2[_CurvePoints.Length];

            if(ReversedOrder)
            {
                Points = _CurvePoints.Reverse().ToArray();
            }
            else
            {
                Array.Copy(_CurvePoints, Points, Points.Length);
            }

            int iCurveStart = FindIndex(Points, startControlPoint);
            int iCurveEnd = FindIndex(Points, endControlPoint);

            //If our end curve is less than our start point we may be dealing with a closed curve where the start and end verticies are the same.
            //If we are not then FindIndex throws an ArgumentException
            if(iCurveEnd < iCurveStart)
            {
                iCurveEnd = FindIndex(Points, endControlPoint, iCurveEnd + 1);
            }

            if (iCurveStart > iCurveEnd)
                throw new ArgumentException("Start index greater than end index");

            GridVector2[] destArray = new GridVector2[(iCurveEnd - iCurveStart) + 1];

            Array.Copy(Points, iCurveStart, destArray, 0, destArray.Length);
            return destArray;
        }

        /// <summary>
        /// Find the index of the point at or above the SearchStart index
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value"></param>
        /// <param name="SearchStart"></param>
        /// <returns></returns>
        private static int FindIndex(GridVector2[] array, GridVector2 value, int SearchStart = 0)
        {
            for(int i = SearchStart; i < array.Length; i++)
            {
                if(array[i] == value)
                {
                    return i;
                }
            }

            throw new ArgumentException("Value not found");
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

        /// <summary>
        /// Remove the last entry from the array
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
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
