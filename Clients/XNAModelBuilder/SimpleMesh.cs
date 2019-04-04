using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry; 

namespace XNAModelBuilder
{
    public class SimpleMesh
    {
        public GridVector3[] Verticies;
        public int[] Edges; 

        public SimpleMesh(GridVector3[] v, int[] e)
        {
            Verticies = v;
            Edges = e;
        }
    }
}

