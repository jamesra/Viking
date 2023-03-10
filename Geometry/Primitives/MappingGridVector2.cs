using System;
using System.Collections.Generic;
using System.Linq;

namespace Geometry
{

    public class MappingGridVector2SortByMapPoints : IComparer<MappingGridVector2>
    {

        #region IComparer<MappingGridVector2> Members

        int IComparer<MappingGridVector2>.Compare(MappingGridVector2 x, MappingGridVector2 y)
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

    public class MappingGridVector2SortByControlPoints : IComparer<MappingGridVector2>
    {

        #region IComparer<MappingGridVector2> Members

        int IComparer<MappingGridVector2>.Compare(MappingGridVector2 x, MappingGridVector2 y)
        {
            double diff = x.ControlPoint.X - y.ControlPoint.X;

            if (diff == 0.0)
            {
                diff = x.ControlPoint.Y - y.ControlPoint.Y;
            }

            if(diff == 0)
                return 0;

            return diff > 0 ? 1 : -1;
        }

        #endregion
    }


    /// <summary>
    /// Records the position of a point in two different 2D planes
    /// </summary>
    [Serializable]
    public readonly struct MappingGridVector2 : ICloneable, IComparable, IEquatable<MappingGridVector2>
    {
        public readonly GridVector2 MappedPoint;
        public readonly GridVector2 ControlPoint;

        public MappingGridVector2(GridVector2 control, GridVector2 mapped)
        {
            MappedPoint = mapped;
            ControlPoint = control;
        }

        /// <summary>
        /// Return the same array with duplicates removed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static void RemoveDuplicates(IList<MappingGridVector2> points)
        {
            for (int i = points.Count - 1; i > 0; i--)
            {
                MappingGridVector2 a = points[i];
                MappingGridVector2 b = points[i - 1];

                if (GridVector2.DistanceSquared(a.ControlPoint, b.ControlPoint) <= Global.EpsilonSquared)
                {
                    points.RemoveAt(i);
                    i++;
                }
                else if (GridVector2.DistanceSquared(a.MappedPoint, b.MappedPoint) <= Global.EpsilonSquared)
                {
                    points.RemoveAt(i);
                    i++;
                }
            }
        }

        public static GridVector2[] ControlPoints(MappingGridVector2[] mapPoints)
        {
            return mapPoints.Select(p => p.ControlPoint).ToArray();
        }

        public static GridVector2[] MappedPoints(MappingGridVector2[] mapPoints)
        {
            return mapPoints.Select(p => p.MappedPoint).ToArray();
        }

        public MappingGridVector2 Copy()
        {
            return new MappingGridVector2(this.ControlPoint, this.MappedPoint);
        }

        public override string ToString()
        {
            return "Ctrl: " + ControlPoint.ToString() + " Mapped: " + MappedPoint.ToString();
        }

        public static string ToMatlab(MappingGridVector2[] array)
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
            if (!(Obj is MappingGridVector2 B))
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

        object ICloneable.Clone()
        {
            return this.MemberwiseClone();
        }

        public static GridRectangle CalculateControlBounds(MappingGridVector2[] mapPoints)
        {
            return mapPoints.ControlBounds();
        }


        public static GridRectangle CalculateMappedBounds(MappingGridVector2[] mapPoints)
        {
            return mapPoints.MappedBounds();
        }

        /// <summary>
        /// Removes duplicate points from the passed list and returns true if duplicates were removed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool RemoveControlSpaceDuplicates(List<MappingGridVector2> points)
        {
            bool DuplicateFound = false;
            //Remove duplicates: In the case that a line on the warpingGrid passes through a point on the fixedGrid then both ends of the line will map the point and we will get a duplicate
            points.Sort(new MappingGridVector2SortByControlPoints());
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
        public static bool RemoveMappedSpaceDuplicates(List<MappingGridVector2> points)
        {
            bool DuplicateFound = false;
            //Remove duplicates: In the case that a line on the warpingGrid passes through a point on the fixedGrid then both ends of the line will map the point and we will get a duplicate
            points.Sort(new MappingGridVector2SortByMapPoints());

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

        public override int GetHashCode()
        {
            //It is not possible to return a hash code for a point because a point can be within an epsilon distance of two other points which generate two 
            //different hash codes.  The solution is either to throw an exception or return a single value for GetHashCode.

            //throw new InvalidOperationException($"It is not mathematically possible to implement {nameof(GetHashCode)} for a point where equality is epsilon based");
            return 0;
        }

        public override bool Equals(object obj)
        {
            if (obj is MappingGridVector2 other)
                return Equals(other);

            return false;
        }

        public bool Equals(MappingGridVector2 other)
        {  
            return this.ControlPoint == other.ControlPoint &&
                   this.MappedPoint == other.MappedPoint;
        }
    }
}
