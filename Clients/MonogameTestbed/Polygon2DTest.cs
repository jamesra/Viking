using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using VikingXNAGraphics;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace MonogameTestbed
{
    public class Polygon2DTest : IGraphicsTest
    {
        public static GridVector2[] CreateTestPolygon(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = new GridVector2[] {new GridVector2(10,10),
                                      new GridVector2(5, 20),
                                      new GridVector2(15, 30),
                                      new GridVector2(30, 30),
                                      new GridVector2(25, 15),
                                      new GridVector2(45, 15),
                                      new GridVector2(45, 10),
                                      new GridVector2(55, 0),
                                      new GridVector2(25, 5)};
            if(offset.HasValue)
                output = output.Select(p => p + offset.Value).ToArray();

            return output;
        }

        public static GridVector2[] CreateInteriorRing(GridVector2? offset = new GridVector2?())
        {
            GridVector2[] output = new GridVector2[] {new GridVector2(12.5,12.5),
                                      new GridVector2(22.5, 12.5),
                                      new GridVector2(24.5, 17.5),
                                      new GridVector2(12.5, 17.5)};

            if (offset.HasValue)
                output = output.Select(p => p + offset.Value).ToArray();

            return output;
        }

        VikingXNAGraphics.MeshView<VertexPositionColor> meshView;

        public void Init(MonoTestbed window)
        {
            GridVector2[] cps = CreateTestPolygon(new GridVector2(-50, 0));

            //GridVector2[] ordered_cps = cps.OrderBy((v) => v).ToArray();
            
            this.meshView = new MeshView<VertexPositionColor>();
            
            MeshModel<VertexPositionColor> model = TriangleNetExtensions.CreateMeshForPolygon2D(cps, null, Color.Goldenrod);
            this.meshView.models.Add(model);

            GridVector2[] holy_cps = CreateTestPolygon();
            GridVector2[] holy_hole = CreateInteriorRing();

            List<GridVector2[]> listInnerRings = new List<GridVector2[]>();
            listInnerRings.Add(holy_hole);
            MeshModel<VertexPositionColor> holy_model = TriangleNetExtensions.CreateMeshForPolygon2D(holy_cps, listInnerRings, Color.Aquamarine);
            this.meshView.models.Add(holy_model);

            meshView.WireFrame = false;

        }

        public void Update()
        {
        }

        public void Draw(MonoTestbed window)
        {
            meshView.Draw(window.GraphicsDevice, window.Scene);
        }
    }
}
