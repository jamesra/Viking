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
        public static GridVector2[] BoxVerticies(double scale)
        {
            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 0),
                new GridVector2(-1, 1),
                new GridVector2(1,1),
                new GridVector2(1,-1),
                new GridVector2(-1,-1)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridPolygon BoxPolygon(double scale) => new GridPolygon(BoxVerticies(scale));


        public static GridVector2[] ConcaveUVerticies(double scale)
        {
            //  *--*    *--*
            //  |  |    |  |
            //  |  |    |  |  
            //  |  *----*  |
            //  *----------*
            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 1),
                new GridVector2(-0.5, 1),
                new GridVector2(-0.5, -0.5),
                new GridVector2(0.5,-0.5),
                new GridVector2(0.5,1),
                new GridVector2(1,1),
                new GridVector2(1,-1),
                new GridVector2(-1,-1)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridPolygon UPolygon(double scale) => new GridPolygon(Primitives.ConcaveUVerticies(scale));

        public static GridVector2[] ConcaveCheckVerticies(double scale)
        {
            //          *
            //         /|
            //  *_    / /
            //   \ \ / /
            //    \ * /
            //     \ / 
            //      *

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, 0),
                new GridVector2(0, -0.5),
                new GridVector2(1, 1),
                new GridVector2(0, -1),
                new GridVector2(-1, 0)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridPolygon ConcaveCheckPolygon(double scale) => new GridPolygon(Primitives.ConcaveCheckVerticies(scale));

        public static GridVector2[] TrapezoidVerticies(double scale)
        {
            //          *
            //        _/|  
            //      _/  |
            //    _/    |
            //   *    _-*
            //   | _--
            //   *-  
            //    

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, 0),
                new GridVector2(-1, -0.5),
                new GridVector2(1, 0),
                new GridVector2(1, 1),
                new GridVector2(-1, 0)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridPolygon TrapezoidPolygon(double scale) => new GridPolygon(Primitives.TrapezoidVerticies(scale));

        public static GridVector2[] DiamondVerticies(double scale)
        {
            //          *
            //         / \  
            //        /   \
            //       *     *
            //        \   /
            //         \ /
            //          *
            

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, 0),
                new GridVector2(0, -1),
                new GridVector2(1, 0),
                new GridVector2(0, 1),
                new GridVector2(-1, 0)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridPolygon DiamondPolygon(double scale) => new GridPolygon(Primitives.DiamondVerticies(scale));

        public static GridVector2[] NotchedBoxVerticies(double scale)
        {
            /// 
            ///  *     *
            ///  |\   /|
            ///  | \ / |
            ///  *  *  |
            ///  |     |
            ///  *-----*
            /// 

            GridVector2[] ExteriorPoints =
            {
                new GridVector2(-1, -1),
                new GridVector2(-1, 0),
                new GridVector2(-1, 1),
                new GridVector2(0, 0),
                new GridVector2(1, 1),
                new GridVector2(1, -1),
                new GridVector2(-1, -1)
            };

            GridVector2[] ExteriorPointsScaled = ExteriorPoints.Scale(scale, new GridVector2(0, 0)).ToArray();
            return ExteriorPointsScaled;
        }

        public static GridPolygon NotchedBoxPolygon(double scale) => new GridPolygon(Primitives.NotchedBoxVerticies(scale));
    }
}
