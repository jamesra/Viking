//#define TRACEMESH

using FsCheck;
using Geometry;

namespace GeometryTests.FSCheck
{
    public static class GeometryArbitraries
    {
        public static void Register()
        {
            Arb.Register<Vector2Generators>();
            Arb.Register<LineSegmentGenerators>();
            Arb.Register<PolygonGenerators>();

            Global.ResetRollingSeed();
        }

        public static Arbitrary<Vector2> PointGenerator() => Vector2Generators.ArbRandomPoint();

        public static Arbitrary<Vector2[]> DistinctPointsGenerator() => Vector2Generators.ArbRandomDistinctPoints();

        public static Arbitrary<LineSegment> LineSegmentGenerator() => LineSegmentGenerators.ArbRandomLine();

        public static Arbitrary<Polyline> PolyLineGenerator() => LineSegmentGenerators.ArbPolyLine();
    }

    /*
    public class PolygonGenerators
    {
        public static Arbitrary<LineSegment> ArbRandomPolygon()
        {
            return Arb.From(GenPoly());
        }

        public static Gen<Polygon> GenPoly(int nVerts)
        {
            
            Vector2Generators.GenDistinctPoints(nVerts).Where()
                
        }
    }
    */
}
