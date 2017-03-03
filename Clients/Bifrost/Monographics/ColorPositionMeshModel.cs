using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Monographics
{
    class ColorPositionMeshModel
    { 
        public Texture2D texture;

        public VertexPositionColor[] verts;
        public int[] edges;

        public ColorPositionMeshModel()
        { 
        }

        public static ColorPositionMeshModel CreateTetrahedron()
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

            ColorPositionMeshModel model = new Monographics.ColorPositionMeshModel();
            model.verts = verts;
            model.edges = edges;

            return model;
        }

        public void Update()
        {

        }
    }
}
