using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public readonly struct MIVector2(Vector2 p, Geometry.PolygonIndex index) : MIConvexHull.IVertex
    {
        public readonly Geometry.Vector2 P = p;
        public readonly Geometry.PolygonIndex PolyIndex = index;

        double[] IVertex.Position => [P.X, P.Y];
    }
}
