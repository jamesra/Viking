//#define TRACEMESH

using FsCheck;
using Geometry;

namespace GeometryTests.FSCheck
{
    public static class GeometryArbitraries
    {
        public static void Register()
        {
            Arb.Register<GridVector2Generators>();
            Arb.Register<GridLineSegmentGenerators>();
            Arb.Register<GridPolygonGenerators>();

            Global.ResetRollingSeed();
        }

        public static Arbitrary<GridVector2> PointGenerator()
        {
            return GridVector2Generators.ArbRandomPoint();
        }

        public static Arbitrary<GridVector2[]> DistinctPointsGenerator()
        {
            return GridVector2Generators.ArbRandomDistinctPoints();
        }

        public static Arbitrary<GridLineSegment> LineSegmentGenerator()
        {
            return GridLineSegmentGenerators.ArbRandomLine();
        }

        public static Arbitrary<GridPolyline> PolyLineGenerator()
        {
            return GridLineSegmentGenerators.ArbPolyLine();
        }
    }

    /*
    public class GridPolygonGenerators
    {
        public static Arbitrary<GridLineSegment> ArbRandomPolygon()
        {
            return Arb.From(GenPoly());
        }

        public static Gen<GridPolygon> GenPoly(int nVerts)
        {
            
            GridVector2Generators.GenDistinctPoints(nVerts).Where()
                
        }
    }
    */
}
