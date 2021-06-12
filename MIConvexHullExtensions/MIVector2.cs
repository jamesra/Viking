using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public struct MIVector2 :  MIConvexHull.IVertex
    {
        public readonly Geometry.GridVector2 P;
        public readonly Geometry.PolygonIndex PolyIndex;


        public MIVector2(GridVector2 p, Geometry.PolygonIndex index)
        {
            this.P = p;
            this.PolyIndex = index;
        }

        double[] IVertex.Position
        {
            get
            {
                return new double[] { P.X, P.Y };
            }
        }
    }
}
