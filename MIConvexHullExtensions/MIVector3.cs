using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public readonly struct MIVector3(Vector3 p, PolygonIndex index) : MIConvexHull.IVertex
    {
        public readonly Geometry.Vector3 P = p;
        public readonly PolygonIndex PolyIndex = index;

        double[] IVertex.Position => P.Coords;

        public override string ToString() => P.ToString();
    }
}
