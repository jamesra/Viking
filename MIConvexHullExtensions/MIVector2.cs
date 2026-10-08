using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    /// <summary>
    /// Wraps a 2D geometry vertex and its <see cref="PolygonIndex"/> for MIConvexHull triangulation.
    /// </summary>
    public readonly struct MIVector2(Vector2 p, Geometry.PolygonIndex index) : MIConvexHull.IVertex
    {
        /// <summary>Planar position in volume coordinates.</summary>
        public readonly Geometry.Vector2 P = p;

        /// <summary>Source polygon vertex index carried through the hull.</summary>
        public readonly Geometry.PolygonIndex PolyIndex = index;

        double[] IVertex.Position => [P.X, P.Y];
    }
}
