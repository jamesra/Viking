using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    /// <summary>
    /// Wraps a 3D geometry vertex and its <see cref="PolygonIndex"/> for MIConvexHull Delaunay triangulation.
    /// </summary>
    public readonly struct MIVector3(Vector3 p, PolygonIndex index) : MIConvexHull.IVertex
    {
        /// <summary>Position in volume coordinates.</summary>
        public readonly Geometry.Vector3 P = p;

        /// <summary>Source polygon vertex index carried through the hull.</summary>
        public readonly PolygonIndex PolyIndex = index;

        double[] IVertex.Position => P.Coords;

        /// <inheritdoc />
        public override string ToString() => P.ToString();
    }
}
