using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VikingXNAGraphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics.Models
{
    public static class Tetrahedron
    {
        public static MeshModel<VertexPositionColor> CreateTetrahedron()
        {
            VertexPositionColor[] verts = new VertexPositionColor[]
            {
                new VertexPositionColor( new Vector3(0,0,0), Color.Red),
                new VertexPositionColor( new Vector3(0,1,0), Color.Blue),
                new VertexPositionColor( new Vector3(0,0,1), Color.Green),
                new VertexPositionColor( new Vector3(1,0,0), Color.Wheat),
            };

            int[] edges = new int[] {0,1,2,
                               0,3,1,
                               0,2,3,
                               1,3,2};

            MeshModel<VertexPositionColor> model = new MeshModel<VertexPositionColor>();
            model.Verticies = verts;
            model.Edges = edges;

            return model;
        }
    }
}
