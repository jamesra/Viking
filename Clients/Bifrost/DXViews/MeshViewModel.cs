using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DXViews
{
    /// <summary>
    /// Describes a mesh
    /// </summary>
    class MeshViewModel
    {
        public Texture2D texture;

        public VertexPositionColor[] verts;
        public int[] edges;

        public MeshViewModel()
        {
            Initialize();
        }

        protected void Initialize()
        {
            verts = new VertexPositionColor[]
            {
                new VertexPositionColor( new Vector3(0,0,0), Color.Red),
                new VertexPositionColor( new Vector3(0,1,0), Color.Blue),
                new VertexPositionColor( new Vector3(0,0,1), Color.Green),
                new VertexPositionColor( new Vector3(1,0,0), Color.Wheat),
            };

            edges = new int[] {0,1,2,
                               0,3,1,
                               0,2,3,
                               1,3,2};
        }

        public void Update()
        {

        }
    }
}
