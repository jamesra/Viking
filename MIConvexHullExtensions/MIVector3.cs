using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public struct MIVector3 :  MIConvexHull.IVertex
    {
        public readonly Geometry.GridVector3 P;
        public readonly PolygonIndex PolyIndex;


        public MIVector3(GridVector3 p, PolygonIndex index)
        {
            this.P = p;
            PolyIndex = index; 
        }

        double[] IVertex.Position
        {
            get
            {
                return P.coords;
            }
        }

        public override string ToString()
        {
            return P.ToString();
        }
    }
}
