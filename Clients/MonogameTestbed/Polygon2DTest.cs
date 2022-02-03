using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    public class Polygon2DTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

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

        public Task Init(MonoTestbed window)
        {
            _initialized = true; 
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

            int[] Convex_hull_idx;
            GridVector2[] cv_output_points = holy_cps.ConvexHull(out Convex_hull_idx);

            List<GridVector2> listCvPoints = new List<GridVector2>(Convex_hull_idx.Select(i => holy_cps[i]));
            GridPolygon convex_hull_poly = new GridPolygon(listCvPoints.ToArray());

            convex_hull_poly = convex_hull_poly.Translate(new GridVector2(0, 40));

            MeshModel<VertexPositionColor> cv_model = TriangleNetExtensions.CreateMeshForPolygon2D(convex_hull_poly, Color.Blue);
            this.meshView.models.Add(cv_model);
            
            MeshModel<VertexPositionColor> circle_cv_model = BuildCircleConvexHull(new GridCircle(new GridVector2(35, -35), 25));
            this.meshView.models.Add(circle_cv_model);

            MeshModel<VertexPositionColor> circle_cv_model2 = BuildCircleConvexHull(new GridCircle(new GridVector2(70, -15), 10));
            this.meshView.models.Add(circle_cv_model2);
            
            MeshModel<VertexPositionColor> circle_cv_model3 = BuildCircleConvexHull(new GridCircle(new GridVector2(-100, 0), 40));
            this.meshView.models.Add(circle_cv_model3);

            meshView.WireFrame = false;

            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {
            
        }

        private MeshModel<VertexPositionColor> BuildCircleConvexHull(ICircle2D circle)
        {

            GridVector2[] verts2D = MorphologyMesh.ShapeMeshGenerator<Geometry.Meshing.IVertex3D<object>,object>.CreateVerticiesForCircle(circle, 0, 16, null, GridVector3.Zero).Select(v => new GridVector2(v.Position.X, v.Position.Y)).ToArray();
              
            int[] cv_idx;
            GridVector2[] cv_verticies = verts2D.ConvexHull(out cv_idx);

            GridPolygon convex_hull_poly = new GridPolygon(cv_verticies);
            return TriangleNetExtensions.CreateMeshForPolygon2D(convex_hull_poly, Color.Blue);
        }

        public void Update()
        {
        }

        public void Draw(MonoTestbed window)
        {
            meshView.Draw(window.GraphicsDevice, window.Scene, CullMode.None);
        }
    }
}
