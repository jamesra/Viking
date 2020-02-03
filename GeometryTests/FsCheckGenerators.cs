using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsCheck;
using Geometry;

namespace GeometryTests
{
    public static class GlobalGenerators
    {
        static GlobalGenerators()
        {
            Arb.Register<GridVector2Generators>();
            Arb.Register<GridLineSegmentGenerators>();
        }
    }

    public class GridLineSegmentGenerators
    {
        public static Arbitrary<GridLineSegment> RandomPoints()
        {
            return Arb.From(ChooseFrom(RandomLines(100)));
            //return Arb.From(Fresh()); 
        }

        private static GridLineSegment[] RandomLines(int size)
        {
            GridLineSegment[] lines= new GridLineSegment[size];
            for (int i = 0; i < size; i++)
            {
                lines[i] = new GridLineSegment(GridVector2.Random(), GridVector2.Random());
            }

            return lines;
        }

        public static Gen<GridLineSegment> ChooseFrom(GridLineSegment[] items)
        {
            return from i in Gen.Choose(0, items.Length - 1)
                   select items[i];
        }
    }

        public class GridVector2Generators
    {

        static System.Random random = new System.Random();

        public static Arbitrary<GridVector2> RandomPoints()
        {
            Gen<GridVector2> RandPoints = ChooseFrom(RandomPoints(100));
            List<GridVector2> l = new List<GridVector2>();
            Gen<GridVector2> GridPoints = ChooseFrom(PointsOnGrid1D(21, 21, new GridRectangle(-10, 10, -10, 10)));
            return Arb.From(Gen.OneOf(RandPoints, GridPoints));
            //return Arb.From(Fresh()); 
        }

        private static GridVector2[] RandomPoints(int size)
        {
            GridVector2[] points = new GridVector2[size];
            for (int i = 0; i < size; i++)
            {
                points[i] = GridVector2.Random();
            }

            return points; 
        }

        private static GridVector2[] PointsOnGrid1D(int GridDimX, int GridDimY, GridRectangle bounds)
        {
            GridVector2[,] points = PointsOnGrid(GridDimX, GridDimY, bounds);
            List<GridVector2> listPoints = new List<GridVector2>(GridDimX * GridDimY);

            for(int i = 0; i < points.GetLength(0); i++)
            {
                for (int j = 0; j < points.GetLength(1); j++)
                {
                    listPoints.Add(points[i, j]);
                }
            }

            return listPoints.ToArray();
        }

        private static GridVector2[,] PointsOnGrid(int GridDimX, int GridDimY, GridRectangle bounds)
        {
            GridVector2[,] points = new GridVector2[GridDimX,GridDimY];
            double XStep = bounds.Width / (GridDimX-1);
            double YStep = bounds.Height / (GridDimY-1);

            double X = bounds.Left; 
            for (int iX = 0; iX < GridDimX; iX++)
            {
                double Y = bounds.Bottom;
                for(int iY = 0; iY < GridDimY; iY++)
                {
                    points[iX, iY] = new GridVector2(X, Y);
                    Y += YStep;
                }

                X += XStep;
            }

            return points;
        }
         

        public static Gen<GridVector2> GenPoints(int size)
        { 
            return ChooseFrom(RandomPoints(size));
        }

        public static Gen<GridVector2> ChooseFrom(GridVector2[] items)
        {
            return from i in Gen.Choose(0, items.Length-1)
                   select items[i];
        }
       

        public static Gen<GridVector2> Fresh()
        {
            System.Random random = new System.Random();
            GridVector2 p = new GridVector2(random.NextDouble(), random.NextDouble());
            System.Diagnostics.Trace.WriteLine(string.Format("{0}", p));
            return Gen.Constant<GridVector2>(p);
        }
    }
}
