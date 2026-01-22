using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VikingXNAGraphics.Models
{
    public static class Tetrahedron
    {
        public static MeshModel<VertexPositionColor> CreateTetrahedron()
        {
            VertexPositionColor[] verts =
            [
                new( new Vector3(0,0,0), Color.Red),
                new( new Vector3(0,1,0), Color.Blue),
                new( new Vector3(0,0,1), Color.Green),
                new( new Vector3(1,0,0), Color.Wheat),
            ];

            int[] edges = [0,1,2,
                               0,3,1,
                               0,2,3,
                               1,3,2];

            MeshModel<VertexPositionColor> model = new()
            {
                Verticies = verts,
                Edges = edges
            };

            return model;
        }
    }
}
