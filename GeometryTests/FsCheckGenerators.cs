using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FsCheck;
using Geometry;
using Geometry.Meshing;

namespace GeometryTests
{
    public static class GeometryArbitraries
    {
        public static void Register()
        {
            Arb.Register<GridVector2Generators>();
            //Arb.Register<GridLineSegmentGenerators>();
            //Arb.Register<TriangulatedMeshGenerators>();
        }

        public static Arbitrary<GridVector2> PointGenerator()
        {
            return GridVector2Generators.ArbRandomPoint();
        }

        
        public static Arbitrary<GridVector2[]> PointsGenerator()
        {
            return GridVector2Generators.ArbRandomPoints();
        }
        

        public static Arbitrary<GridLineSegment> LineSegmentGenerator()
        {
            return GridLineSegmentGenerators.ArbRandomLine();
        }

        public static Arbitrary<TriangulationMesh<Vertex2D>> TriangulatedMeshGenerator()
        {
            return TriangulatedMeshGenerators.ArbRandomMesh();
        }
    }

    public class GridLineSegmentGenerators
    {
        public static Arbitrary<GridLineSegment> ArbRandomLine()
        {
            return Arb.From(GenLine());  
        }

        public static Gen<GridLineSegment> GenLine()
        {
            var coords = Arb.Default.NormalFloat().Generator.Four();
            return coords.Select(t => new GridLineSegment( new GridVector2((double)t.Item1, (double)t.Item2),
                                                           new GridVector2((double)t.Item3, (double)t.Item4)));
        }
         
        public static Gen<GridLineSegment> ChooseFrom(GridLineSegment[] items)
        {
            return from i in Gen.Choose(0, items.Length - 1)
                   select items[i];
        }
    }

    public class GridVector2Generators
    {
        static Gen<GridVector2> GridPoints = ChooseFrom(PointsOnGrid1D(21, 21, new GridRectangle(-10, 10, -10, 10)));

        public static Arbitrary<GridVector2> ArbRandomPoint()
        {
            return Arb.From(RandomPoint());
        }
        
        public static Arbitrary<GridVector2[]> ArbRandomPoints()
        {
            return Arb.From(GenPoints(), Arb.Default.Array<GridVector2>().Shrinker );
        }

        public static Gen<GridVector2> RandomPoint()
        {
            Gen<GridVector2> RandPoints = GenPoint();
            return Gen.Frequency(
                Tuple.Create(2, RandPoints),
                Tuple.Create(1, GridPoints));
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
        
        public static Gen<GridVector2[]> GenPoints()
        {
            return Gen.Sized(size => GenPoints(size));
        }
        
        public static Gen<GridVector2[]> GenPoints(int nPoints)
        {
            return RandomPoint().ArrayOf(nPoints).Where(points => points.Distinct().Count() == nPoints);
        }
        
        public static Gen<GridVector2> ChooseFrom(GridVector2[] items)
        {
            return from i in Gen.Choose(0, items.Length-1)
                   select items[i];
        }

        public static Gen<GridVector2> GenPoint()
        {
            var coords = Arb.Default.NormalFloat().Generator.Two();
            return coords.Select(t => new GridVector2((double)t.Item1, (double)t.Item2));
        } 
    }
    
    
    public class TriangulatedMeshGenerators
    {
        public static Gen<TriangulationMesh<Vertex2D>> GenMesh(int nVerts)
        {
            return GridVector2Generators.GenPoint().ArrayOf(nVerts).Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));            
            //return GridVector2Generators.GenPoints().Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));
        }

        /*
        public static Gen<TriangulationMesh<Vertex2D>> GenMesh(GridVector2[] points)
        {
            //return GridVector2Generators.GenPoint().ArrayOf(nVerts).Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));
    
            return GridVector2Generators.GenPoints().Select(verts => GenericDelaunayMeshGenerator2D<Vertex2D>.TriangulateToMesh(verts.Select(v => new Vertex2D(v)).ToArray()));
        }
        */

        public static Gen<TriangulationMesh<Vertex2D>> RandomMesh()
        {
            return Gen.Sized(size => GenMesh(size)); 
        }

        public static Arbitrary<TriangulationMesh<Vertex2D>> ArbRandomMesh()
        {
            return Arb.From(RandomMesh());
        }
    }
    
}
