using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public readonly struct MIVector3(GridVector3 p, PolygonIndex index) : MIConvexHull.IVertex
    {
        public readonly Geometry.GridVector3 P = p;
        public readonly PolygonIndex PolyIndex = index;

        double[] IVertex.Position => P.coords;

        public override string ToString() => P.ToString();
    }
}
