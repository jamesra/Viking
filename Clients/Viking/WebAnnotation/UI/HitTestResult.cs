using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebAnnotation
{
    public class HitTestResult
    {
        public readonly double Distance;
        public readonly int Z;
        public readonly ICanvasView obj;

        public HitTestResult(ICanvasView o, int z, double dist)
        {
            this.obj = o;
            this.Z = z;
            this.Distance = dist;
        }
    }

    public class HitTest_Z_Distance_Sorter : IComparer<HitTestResult>
    {
        public int Compare(HitTestResult x, HitTestResult y)
        {
            int compareVal = x.Z.CompareTo(y.Z);
            if (compareVal != 0)
                return compareVal;

            return x.Distance.CompareTo(y.Distance);
        }
    }


    public class HitTest_Z_Depth_Distance_Sorter : IComparer<HitTestResult>
    {
        public int Compare(HitTestResult x, HitTestResult y)
        {
            int compareVal = x.Z.CompareTo(y.Z);
            if (compareVal != 0)
                return compareVal;

            //Higher visualHeight numbers sort earlier.  They are closer to the user because they are taller I guess.
            compareVal = -x.obj.VisualHeight.CompareTo(y.obj.VisualHeight);
            if (compareVal != 0)
                return compareVal;

            return x.Distance.CompareTo(y.Distance);
        }
    }

    public class HitTest_Distance_Sorter : IComparer<HitTestResult>
    {
        public int Compare(HitTestResult x, HitTestResult y)
        {
            int compareVal = x.Distance.CompareTo(y.Distance);
            if (compareVal != 0)
                return compareVal;

            //Higher visualHeight numbers sort earlier.  They are closer to the user because they are taller I guess.
            compareVal = -x.obj.VisualHeight.CompareTo(y.obj.VisualHeight);
            return compareVal; 
        }
    }
}
