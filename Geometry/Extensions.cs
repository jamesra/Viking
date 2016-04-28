using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RTree;

namespace Geometry
{
    public static class GeometryRTreeExtensions
    {
        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, float Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridRectangle rect, int Z)
        {
            return new RTree.Rectangle((float)rect.Left, (float)rect.Bottom, (float)rect.Right, (float)rect.Top, (float)Z, (float)Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, float Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, Z, Z);
        }

        public static RTree.Rectangle ToRTreeRect(this GridVector2 p, int Z)
        {
            return new RTree.Rectangle((float)p.X, (float)p.Y, (float)p.X, (float)p.Y, (float)Z, (float)Z);
        }
    }

    public static class MathHelpers
    {
        /// <summary>
        /// Given a scalar value from 0 to 1, return the linearly interpolated value between min/max.
        /// </summary>
        /// <param name="Fraction"></param>
        /// <param name="MinVal"></param>
        /// <param name="MaxVal"></param>
        /// <returns></returns>
        public static double Interpolate(this double Fraction, double MinVal, double MaxVal)
        {
            if (Fraction < 0)
                return MinVal;
            if (Fraction > 1)
                return MaxVal;

            return ((MaxVal - MinVal) * Fraction) + MinVal;
        }
    }

}
