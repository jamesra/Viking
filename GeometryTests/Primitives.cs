using Geometry;
using System.Linq;

namespace GeometryTests
{
    public class Primitives
    {
        /// <summary>
        /// Create a box, note I've added an extra vertex on the X:-1 vertical line
        /// 
        ///  * - - - *
        ///  |       |
        ///  *       |
        ///  |       |
        ///  * - - - *
        /// 
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static Vector2[] BoxVerticies(double scale)
        {
            Vector2[] ExteriorPoints =
            [
                new(-1, -1),
                new(-1, 0),
                new(-1, 1),
                new(1,1),
                new(1,-1),
                new(-1,-1)
            ];

            Vector2[] ExteriorPointsScaled = [.. ExteriorPoints.Scale(scale, new Vector2(0, 0))];
            return ExteriorPointsScaled;
        }

        public static Polygon BoxPolygon(double scale) => new(BoxVerticies(scale));


        public static Vector2[] ConcaveUVerticies(double scale)
        {
            //  *--*    *--*
            //  |  |    |  |
            //  |  |    |  |  
            //  |  *----*  |
            //  *----------*
            Vector2[] ExteriorPoints =
            [
                new(-1, -1),
                new(-1, 1),
                new(-0.5, 1),
                new(-0.5, -0.5),
                new(0.5,-0.5),
                new(0.5,1),
                new(1,1),
                new(1,-1),
                new(-1,-1)
            ];

            Vector2[] ExteriorPointsScaled = [.. ExteriorPoints.Scale(scale, new Vector2(0, 0))];
            return ExteriorPointsScaled;
        }

        public static Polygon UPolygon(double scale) => new(Primitives.ConcaveUVerticies(scale));

        public static Vector2[] ConcaveCheckVerticies(double scale)
        {
            //          *
            //         /|
            //  *_    / /
            //   \ \ / /
            //    \ * /
            //     \ / 
            //      *

            Vector2[] ExteriorPoints =
            [
                new(-1, 0),
                new(0, -0.5),
                new(1, 1),
                new(0, -1),
                new(-1, 0)
            ];

            Vector2[] ExteriorPointsScaled = [.. ExteriorPoints.Scale(scale, new Vector2(0, 0))];
            return ExteriorPointsScaled;
        }

        public static Polygon ConcaveCheckPolygon(double scale) => new(Primitives.ConcaveCheckVerticies(scale));

        public static Vector2[] TrapezoidVerticies(double scale)
        {
            //          *
            //        _/|  
            //      _/  |
            //    _/    |
            //   *    _-*
            //   | _--
            //   *-  
            //    

            Vector2[] ExteriorPoints =
            [
                new(-1, 0),
                new(-1, -0.5),
                new(1, 0),
                new(1, 1),
                new(-1, 0)
            ];

            Vector2[] ExteriorPointsScaled = [.. ExteriorPoints.Scale(scale, new Vector2(0, 0))];
            return ExteriorPointsScaled;
        }

        public static Polygon TrapezoidPolygon(double scale) => new(Primitives.TrapezoidVerticies(scale));

        public static Vector2[] DiamondVerticies(double scale)
        {
            //          *
            //         / \  
            //        /   \
            //       *     *
            //        \   /
            //         \ /
            //          *


            Vector2[] ExteriorPoints =
            [
                new(-1, 0),
                new(0, -1),
                new(1, 0),
                new(0, 1),
                new(-1, 0)
            ];

            Vector2[] ExteriorPointsScaled = [.. ExteriorPoints.Scale(scale, new Vector2(0, 0))];
            return ExteriorPointsScaled;
        }

        public static Polygon DiamondPolygon(double scale) => new(Primitives.DiamondVerticies(scale));

        public static Vector2[] NotchedBoxVerticies(double scale)
        {
            /// 
            ///  *     *
            ///  |\   /|
            ///  | \ / |
            ///  *  *  |
            ///  |     |
            ///  *-----*
            /// 

            Vector2[] ExteriorPoints =
            [
                new(-1, -1),
                new(-1, 0),
                new(-1, 1),
                new(0, 0),
                new(1, 1),
                new(1, -1),
                new(-1, -1)
            ];

            Vector2[] ExteriorPointsScaled = [.. ExteriorPoints.Scale(scale, new Vector2(0, 0))];
            return ExteriorPointsScaled;
        }

        public static Polygon NotchedBoxPolygon(double scale) => new(Primitives.NotchedBoxVerticies(scale));
    }
}
