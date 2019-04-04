using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using MIConvexHull;

namespace MIConvexHullExtensions
{
    public struct MIVector3 :  MIConvexHull.IVertex
    {
        public readonly Geometry.GridVector3 P;
        public readonly PointIndex PolyIndex;


        public MIVector3(GridVector3 p, PointIndex index)
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
