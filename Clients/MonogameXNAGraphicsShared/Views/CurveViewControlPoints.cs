using Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace VikingXNAGraphics
{

    public class CurveViewControlPoints
    {
        /// <summary>
        /// Set to true if the order of control points was reversed during processing
        /// </summary>
        /// 
        readonly bool ReversedOrder = false;
        public CurveViewControlPoints(ICollection<Vector2> cps, uint NumInterpolations, bool TryToClose)
        {
            if (cps.Count < 2)
            {
                throw new ArgumentException("Cannot create a curve with fewer than two control points");
            }
            else if (cps.Count == 2 && TryToClose)
            {
                throw new ArgumentException("Cannot close a curve with only two points");
            }

            this._NumInterpolations = NumInterpolations;
            this._TryCloseCurve = TryToClose;

            if (NumInterpolations == 0)
            {
                this.ControlPoints = [.. cps];
            }

            if (TryCloseCurve && cps.Count > 2)
            {
                bool Reverse = cps.ToArray().AreClockwise();
                ReversedOrder = Reverse;
                this.ControlPoints = Reverse ? [.. ((IEnumerable<Vector2>)cps).Reverse()] : [.. cps];
            }
            else
                this.ControlPoints = ReverseControlPointsIfTextUpsideDown(cps, out ReversedOrder);
        }

        private static Vector2[] ReverseControlPointsIfTextUpsideDown(ICollection<Vector2> cps, out bool Reversed)
        {
            Reversed = false;

            if (cps.First().X > cps.Last().X)
            {
                Reversed = true;
                return [.. ((IEnumerable<Vector2>)cps).Reverse()];
            }

            return [.. cps];
        }

        /// <summary>
        /// Try to close the curve if we have enough control points
        /// </summary>
        private bool _TryCloseCurve;
        public bool TryCloseCurve
        {
            get => _TryCloseCurve;
            set
            {
                if (_TryCloseCurve != value)
                {
                    _TryCloseCurve = value;
                    RecalculateCurvePoints();
                }

            }
        }

        private Vector2[] _ControlPoints;

        /// <summary>
        /// In a closed curve the control points are not looped, the first and last control points should be different
        /// </summary>
        public Vector2[] ControlPoints
        {
            get => _ControlPoints;
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

        private Vector2[] _CurvePoints;
        public Vector2[] CurvePoints => _CurvePoints;

        /// <summary>
        /// Return the interpolated points between the two control point indicies
        /// </summary>
        /// <param name="iStart"></param>
        /// <param name="iEnd"></param>
        /// <returns></returns>
        public Vector2[] CurvePointsBetweenControlPoints(int? iStart, int? iEnd)
        {
            if (!iStart.HasValue)
                iStart = 0;
            if (!iEnd.HasValue)
                iEnd = ControlPoints.Length - 1;

            bool EndAtLastVertex = false;
            while (iEnd.Value >= ControlPoints.Length)
            {
                EndAtLastVertex = true;
                iEnd -= ControlPoints.Length;
            }

            Vector2 startControlPoint = ControlPoints[iStart.Value];
            Vector2 endControlPoint = ControlPoints[iEnd.Value];

            // int iCurveStart = iStart.Value * (int)_NumInterpolations;
            // int iCurveEnd = iEnd.Value * (int)_NumInterpolations;

            int iCurveStart = FindIndex(_CurvePoints, startControlPoint);
            int iCurveEnd = FindIndex(_CurvePoints, endControlPoint);

            if (EndAtLastVertex)
            {
                iCurveEnd = _CurvePoints.Length;
            }

            if (iCurveStart > iCurveEnd)
                throw new ArgumentException("Start index greater than end index");

            Vector2[] destArray = new Vector2[iCurveEnd - iCurveStart];

            Array.Copy(_CurvePoints, iCurveStart, destArray, 0, destArray.Length);
            return destArray;
        }

        /// <summary>
        /// Return the interpolated points between the two control point indicies
        /// </summary>
        /// <param name="iStart"></param>
        /// <param name="iEnd"></param>
        /// <returns></returns>
        public Vector2[] CurvePointsBetweenControlPoints(Vector2 startControlPoint, Vector2 endControlPoint)
        {
            //If we reversed the order of the input array we need to reverse the start and end points
            Vector2[] Points = new Vector2[_CurvePoints.Length];

            if (ReversedOrder)
            {
                Points = [.. ((IEnumerable<Vector2>)_CurvePoints).Reverse()];
            }
            else
            {
                Array.Copy(_CurvePoints, Points, Points.Length);
            }

            int iCurveStart = FindIndex(Points, startControlPoint);
            int iCurveEnd = FindIndex(Points, endControlPoint);

            //If our end curve is less than our start point we may be dealing with a closed curve where the start and end verticies are the same.
            //If we are not then FindIndex throws an ArgumentException
            if (iCurveEnd < iCurveStart)
            {
                iCurveEnd = FindIndex(Points, endControlPoint, iCurveEnd + 1);
            }

            if (iCurveStart > iCurveEnd)
                throw new ArgumentException("Start index greater than end index");

            Vector2[] destArray = new Vector2[(iCurveEnd - iCurveStart) + 1];

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
        private static int FindIndex(Vector2[] array, Vector2 value, int SearchStart = 0)
        {
            for (int i = SearchStart; i < array.Length; i++)
            {
                if (array[i] == value)
                {
                    return i;
                }
            }

            throw new ArgumentException("Value not found");
        }

        private uint _NumInterpolations = 1;
        public uint NumInterpolations
        {
            get => _NumInterpolations;
            set
            {
                if (value != _NumInterpolations)
                {
                    _NumInterpolations = value;
                    RecalculateCurvePoints();
                }
            }
        }

        public void SetPoint(int i, Vector2 value)
        {
            _ControlPoints[i] = value;
            RecalculateCurvePoints();
        }

        /// <summary>
        /// Remove the last entry from the array
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        private static Vector2[] RemoveLastEntry(Vector2[] array)
        {
            Vector2[] cps = new Vector2[array.Length - 1];
            Array.Copy(array, cps, array.Length - 1);
            return cps;
        }

        public void RecalculateCurvePoints() => this._CurvePoints = [.. this._ControlPoints.CalculateCurvePoints(this._NumInterpolations, this._TryCloseCurve)];
    }
}
