using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public struct MIVector2 :  MIConvexHull.IVertex
    {
        public readonly Geometry.GridVector2 P;
        public readonly PointIndex PolyIndex;


        public MIVector2(GridVector2 p, PointIndex index)
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
