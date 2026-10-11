using System.Collections.Generic;

namespace WebAnnotation
{
    /// <summary>
    /// One candidate returned by annotation hit testing: the hit object plus the keys used to rank it against other candidates.
    /// Immutable; instances are created per query and ranked with one of the sorters below. Not thread-safe to enumerate while the source list is modified.
    /// </summary>
    /// <param name="o">The object that was hit.</param>
    /// <param name="z">Z-order of the hit object; lower sorts first.</param>
    /// <param name="visual_height">Visual height of the object; taller objects are treated as nearer the user.</param>
    /// <param name="dist">Distance from the query point; 0 means the point is inside the object.</param>
    public class HitTestResult(VikingXNAGraphics.IHitTesting o, int z, int visual_height, double dist)
    {
        /// <summary>
        /// Contains for LocationPolygonView is semi-broken because we need to select holes in the polygon for UI purposes.  However for pen
        /// purposes we want contains to return false.  The workaround is that if the point is inside the interior hole it has a distance > 1
        /// where any other annotation that returns contains == true would have a distance == 0
        /// </summary>
        public readonly double Distance = dist;
        /// <summary>Z-order of the hit object.</summary>
        public readonly int Z = z;
        /// <summary>Visual height of the hit object, used to break ties between overlapping annotations.</summary>
        public readonly int VisualHeight = visual_height;
        /// <summary>The object that was hit.</summary>
        public readonly VikingXNAGraphics.IHitTesting obj = o;
    }

    /// <summary>Orders hits by Z, then by nearest distance. Used for structure links.</summary>
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

    /// <summary>Orders hits by Z, then tallest <see cref="HitTestResult.VisualHeight"/> first, then by nearest distance. Used for locations on the current section.</summary>
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

    /// <summary>Orders hits by nearest distance, then tallest <see cref="HitTestResult.VisualHeight"/> first. Used for objects on adjacent sections and for links.</summary>
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
