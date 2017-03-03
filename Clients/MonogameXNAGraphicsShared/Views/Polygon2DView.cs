using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Render a polygon described by an exterior ring and one or more interior rings
    /// </summary>
    class Polygon2DView
    {
        GridVector2[] Verticies;
        public Color Color { get; set; }

        protected ModelMesh Mesh;

        public Polygon2DView()
        {

        }

        public static ModelMesh CreateMeshForPolygon2D(GridVector2[] Verticies)
        {
            ModelMesh mesh = new ModelMesh();

            
        }
        
    }
}
