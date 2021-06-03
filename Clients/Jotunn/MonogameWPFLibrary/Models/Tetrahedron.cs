using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonogameWPFLibrary.Models
{
    /// <summary>
    /// Built in model used for testing
    /// </summary>
    public static class Tetrahedron
    {
        public static readonly VertexPositionColor[] verts;
        public static readonly int[] edges;

        static Tetrahedron()
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
    }
}
