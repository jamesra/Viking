using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TriangleNet;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    public class Polygon2DTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        bool _initialized = false;
        public bool Initialized => _initialized;

        public static Geometry.Vector2[] CreateTestPolygon(Geometry.Vector2? offset = new Geometry.Vector2?())
        {
            Geometry.Vector2[] output = [new(10,10),
                                      new(5, 20),
                                      new(15, 30),
                                      new(30, 30),
                                      new(25, 15),
                                      new(45, 15),
                                      new(45, 10),
                                      new(55, 0),
                                      new(25, 5)];
            if (offset.HasValue)
                output = [.. output.Select(p => p + offset.Value)];

            return output;
        }

        public static Geometry.Vector2[] CreateInteriorRing(Geometry.Vector2? offset = new Geometry.Vector2?())
        {
            Geometry.Vector2[] output = [new(12.5,12.5),
                                      new(22.5, 12.5),
                                      new(24.5, 17.5),
                                      new(12.5, 17.5)];

            if (offset.HasValue)
                output = [.. output.Select(p => p + offset.Value)];

            return output;
        }

        VikingXNAGraphics.MeshView<VertexPositionColor> meshView;

        public Task Init(MonoTestbed window)
        {
            _initialized = true;
            Geometry.Vector2[] cps = CreateTestPolygon(new Geometry.Vector2(-50, 0));

            //Geometry.Vector2[] ordered_cps = cps.OrderBy((v) => v).ToArray();

            this.meshView = new MeshView<VertexPositionColor>();

            MeshModel<VertexPositionColor> model = TriangleNetExtensions.CreateMeshForPolygon2D(cps, null, Color.Goldenrod);
            this.meshView.models.Add(model);

            Geometry.Vector2[] holy_cps = CreateTestPolygon();
            Geometry.Vector2[] holy_hole = CreateInteriorRing();

            List<Geometry.Vector2[]> listInnerRings =
            [
                holy_hole
            ];
            MeshModel<VertexPositionColor> holy_model = TriangleNetExtensions.CreateMeshForPolygon2D(holy_cps, listInnerRings, Color.Aquamarine);
            this.meshView.models.Add(holy_model);

            Geometry.Vector2[] cv_output_points = holy_cps.ConvexHull(out int[] Convex_hull_idx);

            List<Geometry.Vector2> listCvPoints = [.. Convex_hull_idx.Select(i => holy_cps[i])];
            Polygon convex_hull_poly = new(listCvPoints.ToArray());

            convex_hull_poly = convex_hull_poly.Translate(new Geometry.Vector2(0, 40));

            MeshModel<VertexPositionColor> cv_model = TriangleNetExtensions.CreateMeshForPolygon2D(convex_hull_poly, Color.Blue);
            this.meshView.models.Add(cv_model);
            /*
            MeshModel<VertexPositionColor> circle_cv_model = BuildCircleConvexHull(new Circle(new Geometry.Vector2(35, -35), 25));
            this.meshView.models.Add(circle_cv_model);

            MeshModel<VertexPositionColor> circle_cv_model2 = BuildCircleConvexHull(new Circle(new Geometry.Vector2(70, -15), 10));
            this.meshView.models.Add(circle_cv_model2);
            
            MeshModel<VertexPositionColor> circle_cv_model3 = BuildCircleConvexHull(new Circle(new Geometry.Vector2(-100, 0), 40));
            this.meshView.models.Add(circle_cv_model3);
            */

            meshView.WireFrame = false;

            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {

        }

        /*
        private MeshModel<VertexPositionColor> BuildCircleConvexHull(ICircle2D circle)
        {

            Geometry.Vector2[] verts2D = MorphologyMesh.ShapeMeshGenerator<Geometry.Meshing.IVertex3D<object>,object>.CreateVerticiesForCircle(circle, 0, 16, null, Geometry.Vector3.Zero).Select(v => new Geometry.Vector2(v.Position.X, v.Position.Y)).ToArray();

            Geometry.Vector2[] cv_verticies = verts2D.ConvexHull(out int[] cv_idx);

            Polygon convex_hull_poly = new Polygon(cv_verticies);
            return TriangleNetExtensions.CreateMeshForPolygon2D(convex_hull_poly, Color.Blue);
        }
        */

        public void Update()
        {
        }

        public void Draw(MonoTestbed window) => meshView.Draw(window.GraphicsDevice, window.Scene, CullMode.None);
    }
}
