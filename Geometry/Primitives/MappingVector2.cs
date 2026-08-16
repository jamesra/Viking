using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{
    public class MappingVector2Comparer(bool xyOrder = true, bool compareMappedPoints = true) : IComparer<MappingVector2>
    {
        private readonly bool XYOrder = xyOrder;
        private readonly bool CompareMappedPoints = compareMappedPoints;

        public int Compare(MappingVector2 A, MappingVector2 B)
        {
            var pointA = CompareMappedPoints ? A.MappedPoint : A.ControlPoint;
            var pointB = CompareMappedPoints ? B.MappedPoint : B.ControlPoint;
            return XYOrder ? Vector2ComparerXY.CompareXY(pointA, pointB) : Vector2ComparerYX.CompareYX(pointA, pointB);
        }
    }

    public class MappingVector2SortByMapPoints : IComparer<MappingVector2>
    {

        #region IComparer<MappingVector2> Members

        int IComparer<MappingVector2>.Compare(MappingVector2 x, MappingVector2 y)
        {
            double diff = x.MappedPoint.X - y.MappedPoint.X;

            if (diff == 0.0)
            {
                diff = x.MappedPoint.Y - y.MappedPoint.Y;
            }

            if (diff == 0)
                return 0;

            return diff > 0 ? 1 : -1;
        }

        #endregion
    }

    public class MappingVector2SortByControlPoints : IComparer<MappingVector2>
    {

        #region IComparer<MappingVector2> Members

        int IComparer<MappingVector2>.Compare(MappingVector2 x, MappingVector2 y)
        {
            double diff = x.ControlPoint.X - y.ControlPoint.X;

            if (diff == 0.0)
            {
                diff = x.ControlPoint.Y - y.ControlPoint.Y;
            }

            if (diff == 0)
                return 0;

            return diff > 0 ? 1 : -1;
        }

        #endregion
    }


    /// <summary>
    /// Records the position of a point in two different 2D planes
    /// </summary>
    [Serializable]
    public readonly struct MappingVector2(Vector2 control, Vector2 mapped) : ICloneable, IComparable, IEquatable<MappingVector2>
    {
        public readonly Vector2 MappedPoint = mapped;
        public readonly Vector2 ControlPoint = control;

        /// <summary>
        /// Return the same array with duplicates removed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static void RemoveDuplicates(IList<MappingVector2> points)
        {
            for (int i = points.Count - 1; i > 0; i--)
            {
                MappingVector2 a = points[i];
                MappingVector2 b = points[i - 1];

                if (Vector2.DistanceSquared(a.ControlPoint, b.ControlPoint) <= Global.EpsilonSquared)
                {
                    points.RemoveAt(i);
                    i++;
                }
                else if (Vector2.DistanceSquared(a.MappedPoint, b.MappedPoint) <= Global.EpsilonSquared)
                {
                    points.RemoveAt(i);
                    i++;
                }
            }
        }

        public static Vector2[] ControlPoints(MappingVector2[] mapPoints) => [.. mapPoints.Select(p => p.ControlPoint)];

        public static Vector2[] MappedPoints(MappingVector2[] mapPoints) => [.. mapPoints.Select(p => p.MappedPoint)];

        public MappingVector2 Copy() => new MappingVector2(this.ControlPoint, this.MappedPoint);

        public override string ToString() => "Ctrl: " + ControlPoint.ToString() + " Mapped: " + MappedPoint.ToString();

        public static string ToMatlab(MappingVector2[] array)
        {
            string s = "[";
            for (int i = 0; i < array.Length; i++)
            {
                s += array[i].ControlPoint.X.ToString() + " " + array[i].ControlPoint.Y.ToString() + " " + array[i].MappedPoint.X.ToString() + " " + array[i].MappedPoint.Y.ToString() + ";" + System.Environment.NewLine;
            }
            s += "]";

            return s;
        }

        /// <summary>
        /// Sorted by X coordinante of control point, using Y coordinate as tie-breaker
        /// </summary>
        /// <param name="Obj"></param>
        /// <returns></returns>
        int IComparable.CompareTo(object Obj)
        {
            if (Obj is not MappingVector2 B)
                return int.MaxValue;

            double diff = this.MappedPoint.X - B.MappedPoint.X;

            if (diff == 0.0)
            {
                diff = this.MappedPoint.Y - B.MappedPoint.Y;
            }

            if (diff > 0)
                return 1;
            if (diff < 0)
                return -1;

            return 0;
        }

        object ICloneable.Clone() => this.MemberwiseClone();

        public static Rectangle CalculateControlBounds(MappingVector2[] mapPoints) => mapPoints.ControlBounds();


        public static Rectangle CalculateMappedBounds(MappingVector2[] mapPoints) => mapPoints.MappedBounds();

        /// <summary>
        /// Removes duplicate points from the passed list and returns true if duplicates were removed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool RemoveControlSpaceDuplicates(List<MappingVector2> points)
        {
            bool DuplicateFound = false;
            //Remove duplicates: In the case that a line on the warpingGrid passes through a point on the fixedGrid then both ends of the line will map the point and we will get a duplicate
            points.Sort(new MappingVector2SortByControlPoints());
            int iCompareStart = 0;
            for (int iTest = 1; iTest < points.Count; iTest++)
            {
                //   Debug.Assert(newPoints[iTest - 1].ControlPoint != newPoints[iTest].ControlPoint);
                //This is slow, but even though we sort on the X axis it doesn't mean a point that is not adjacent to the point on the list isn't too close
                for (int jTest = iCompareStart; jTest < iTest; jTest++)
                {
                    if (points[jTest].ControlPoint == points[iTest].ControlPoint)
                    {
                        points.RemoveAt(iTest);
                        iTest--;
                        DuplicateFound = true;
                        break;
                    }

                    //Optimization, since the array is sorted we don't need to compare points once a point is distant enough
                    if (points[iTest].ControlPoint.X - points[jTest].ControlPoint.X > Global.Epsilon)
                    {
                        iCompareStart = jTest;
                    }
                }
            }

            return DuplicateFound;
        }

        /// <summary>
        /// Removes duplicate points from the passed list and returns true if duplicates were removed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool RemoveMappedSpaceDuplicates(List<MappingVector2> points)
        {
            bool DuplicateFound = false;
            //Remove duplicates: In the case that a line on the warpingGrid passes through a point on the fixedGrid then both ends of the line will map the point and we will get a duplicate
            points.Sort(new MappingVector2SortByMapPoints());

            int iCompareStart = 0;
            for (int iTest = 1; iTest < points.Count; iTest++)
            {
                //   Debug.Assert(newPoints[iTest - 1].ControlPoint != newPoints[iTest].ControlPoint);
                //This is slow, but even though we sort on the X axis it doesn't mean a point that is not adjacent to the point on the list isn't too close
                for (int jTest = iCompareStart; jTest < iTest; jTest++)
                {
                    if (points[jTest].MappedPoint == points[iTest].MappedPoint)
                    {
                        points.RemoveAt(iTest);
                        iTest--;
                        DuplicateFound = true;
                        break;
                    }

                    //Optimization, since the array is sorted we don't need to compare points once a point is distant enough
                    if (points[iTest].MappedPoint.X - points[jTest].MappedPoint.X > Global.Epsilon)
                    {
                        iCompareStart = jTest;
                    }
                }
            }

            return DuplicateFound;
        }

        public override int GetHashCode() =>
            GeometryHashCode.Combine(
                GeometryHashCode.Point2D(ControlPoint),
                GeometryHashCode.Point2D(MappedPoint));

        public override bool Equals(object obj)
        {
            if (obj is MappingVector2 other)
                return Equals(other);

            return false;
        }

        public bool Equals(MappingVector2 other)
        {
            return this.ControlPoint == other.ControlPoint &&
                   this.MappedPoint == other.MappedPoint;
        }

        // Implement the == operator for MappingVector2.
        public static bool operator ==(MappingVector2 v1, MappingVector2 v2) => v1.Equals(v2);

        public static bool operator !=(MappingVector2 v1, MappingVector2 v2) => !v1.Equals(v2);
    }
}
