using System.Collections.Generic;

namespace WebAnnotation
{
    public class HitTestResult(VikingXNAGraphics.IHitTesting o, int z, int visual_height, double dist)
    {
        /// <summary>
        /// Contains for LocationPolygonView is semi-broken because we need to select holes in the polygon for UI purposes.  However for pen
        /// purposes we want contains to return false.  The workaround is that if the point is inside the interior hole it has a distance > 1
        /// where any other annotation that returns contains == true would have a distance == 0
        /// </summary>
        public readonly double Distance = dist;
        public readonly int Z = z;
        public readonly int VisualHeight = visual_height;
        public readonly VikingXNAGraphics.IHitTesting obj = o;
    }

    public class HitTest_Z_Distance_Sorter : IComparer<HitTestResult>
    {
        public int Compare(HitTestResult x, HitTestResult y)
        {
            int compareVal = x.Z.CompareTo(y.Z);
            if (compareVal != 0)
            {
                return compareVal;
            }

            return x.Distance.CompareTo(y.Distance);
        }
    }

    public class HitTest_Z_Depth_Distance_Sorter : IComparer<HitTestResult>
    {
        public int Compare(HitTestResult x, HitTestResult y)
        {
            int compareVal = x.Z.CompareTo(y.Z);
            if (compareVal != 0)
            {
                return compareVal;
            }

            //Higher visualHeight numbers sort earlier.  They are closer to the user because they are taller I guess.
            compareVal = -x.VisualHeight.CompareTo(y.VisualHeight);
            if (compareVal != 0)
            {
                return compareVal;
            }

            return x.Distance.CompareTo(y.Distance);
        }
    }

    public class HitTest_Distance_Sorter : IComparer<HitTestResult>
    {
        public int Compare(HitTestResult x, HitTestResult y)
        {
            int compareVal = x.Distance.CompareTo(y.Distance);
            if (compareVal != 0)
            {
                return compareVal;
            }

            //Higher visualHeight numbers sort earlier.  They are closer to the user because they are taller I guess.
            compareVal = -x.VisualHeight.CompareTo(y.VisualHeight);
            return compareVal;
        }
    }
}
