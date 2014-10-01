using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

            if (diff > 0)
                return 1;
            if (diff < 0)
                return -1;

            return 0;  
        }

        #endregion
    }
    /// <summary>
    /// Records the position of a point in two different 2D planes
    /// </summary>
    [Serializable]
    public class MappingGridVector2 : ICloneable, IComparable
    {
        public GridVector2 MappedPoint;
        public GridVector2 ControlPoint;

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
                MappingGridVector2 b = points[i-1];

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
            GridVector2[] array = new GridVector2[mapPoints.Length];

            for (int i = 0; i < mapPoints.Length; i++)
            {
                array[i] = mapPoints[i].ControlPoint;
            }

            return array; 
        }

        public static GridVector2[] MappedPoints(MappingGridVector2[] mapPoints)
        {
            GridVector2[] array = new GridVector2[mapPoints.Length];

            for (int i = 0; i < mapPoints.Length; i++)
            {
                array[i] = mapPoints[i].MappedPoint;
            }

            return array;
        }

        public MappingGridVector2 Copy()
        {
            return ((ICloneable)this).Clone() as MappingGridVector2;
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
            MappingGridVector2 B = Obj as MappingGridVector2;
            if (B == null)
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
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //Looking at gridIndicies isn't efficient, but it prevents adding removed verticies to 
            //boundary
            for (int i = 0; i < mapPoints.Length; i++)
            {
                minX = Math.Min(minX, mapPoints[i].ControlPoint.X);

                maxX = Math.Max(maxX, mapPoints[i].ControlPoint.X);

                minY = Math.Min(minY, mapPoints[i].ControlPoint.Y);

                maxY = Math.Max(maxY, mapPoints[i].ControlPoint.Y);
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        }


        public static GridRectangle CalculateMappedBounds(MappingGridVector2[] mapPoints)
        {
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            //   Debug.Assert(mapPoints.Length > 0); 

            //Looking at gridIndicies isn't efficient, but it prevents adding removed verticies to 
            //boundary
            for (int i = 0; i < mapPoints.Length; i++)
            {
                minX = Math.Min(minX, mapPoints[i].MappedPoint.X);
                maxX = Math.Max(maxX, mapPoints[i].MappedPoint.X);
                minY = Math.Min(minY, mapPoints[i].MappedPoint.Y);
                maxY = Math.Max(maxY, mapPoints[i].MappedPoint.Y);
            }

            return new GridRectangle(minX, maxX, minY, maxY);
        }

        /// <summary>
        /// Removes duplicate points from the passed list and returns true if duplicates were removed
        /// </summary>
        /// <param name="points"></param>
        /// <returns></returns>
        public static bool RemoveDuplicates(List<MappingGridVector2> points)
        {
            bool DuplicateFound = false; 
            //Remove duplicates: In the case that a line on the warpingGrid passes through a point on the fixedGrid then both ends of the line will map the point and we will get a duplicate
            points.Sort();
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
    }
}
