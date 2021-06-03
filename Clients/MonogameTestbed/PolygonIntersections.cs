using FsCheck;
using Geometry;
using Geometry.JSON;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    enum PolygonIntersectionTestDataType
    {
        JSON_POLYGON_INTERSECTION,
        FS_CHECK
    }

    public class PolygonIntersectionView
    {
        PolygonSetView PolygonsView = null;

        public PolygonIntersectionView(GridPolygon[] polygons)
        {
            PolygonsView = new PolygonSetView(polygons);
            PolygonsView.PointLabelType = IndexLabelType.POLYGON | IndexLabelType.POSITION;
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if(PolygonsView != null)
            {
                PolygonsView.Draw(window, scene);
            }
        }
    }

    public class PolygonIntersectionTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        Scene scene;
        Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();
        GamePadStateTracker Gamepad = new GamePadStateTracker();

        PolygonIntersectionTestDataType TestType = PolygonIntersectionTestDataType.FS_CHECK;

        Task TestTask = null;

        PolygonIntersectionView polygonSetView = null;

        private static string[] PolygonIntersections1 = new string[]
        {
            "{  \"ExteriorRing\": [    {      \"X\": 84.0,      \"Y\": -87.0    },    {      \"X\": 86.352103764631451,      \"Y\": -5.1467889908256881    },   {      \"X\": 89.0,      \"Y\": 87.0    },    {      \"X\": 72.422599608099276,      \"Y\": 85.001306335728287    },    {      \"X\": -52.0,      \"Y\": 70.0    },    {      \"X\": 84.0,      \"Y\": -87.0    }  ],  \"InteriorRings\": []}",
            "{ \"ExteriorRing\": [    {      \"X\": 89.0,      \"Y\": -6.0    },    {      \"X\": 89.0,      \"Y\": 99.0    },    {      \"X\": 72.422599608099276,      \"Y\": 85.001306335728287    },    {      \"X\": -1.0,      \"Y\": 23.0    },    {      \"X\": 86.352103764631451,      \"Y\": -5.1467889908256881    },    {      \"X\": 89.0,      \"Y\": -6.0    }  ],  \"InteriorRings\": []    }"
        };

        public void Init(MonoTestbed window)
        {
            _initialized = true;
            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);
             
            PopulateTestTask();
        }

        private void PopulateTestTask()
        {
            GridRectangle rect = new GridRectangle(GridVector2.Zero, 50);
            if (TestType == PolygonIntersectionTestDataType.JSON_POLYGON_INTERSECTION)
            {
                GridPolygon[] polygons = PolygonIntersections1.Select(s => GeometryJSONExtensions.PolygonFromJSON(s)).ToArray();

                GridPolygon p1 = polygons[0];
                //FirstTriangulationDone = true;  
                rect = polygons.BoundingBox();
                scene.Camera.LookAt = rect.Center.ToXNAVector2();
                scene.Camera.Downsample = Math.Max(rect.Height, rect.Width) / Math.Min(scene.Viewport.Height, scene.Viewport.Width);

                polygonSetView = new PolygonIntersectionView(polygons);

                TestTask = new Task(() => {

                    var CorrespondingPoints = polygons.AddCorrespondingVerticies();
                    polygonSetView = new PolygonIntersectionView(polygons);

                });
            }
            else if (TestType == PolygonIntersectionTestDataType.FS_CHECK)
            {
                TestTask = new Task(() => {
                    GeometryTests.GridPolygonTest.TestPolygonIntersectionGenerator(this.OnPolygonUpdate);
                });
            }
        }

        private void OnPolygonUpdate(GridPolygon[] polygons, List<GridVector2> found, List<GridVector2> expected)
        {
            polygonSetView = new PolygonIntersectionView(polygons);
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
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            CameraManipulator.Update(scene.Camera);

            if (Gamepad.A_Clicked)
            {
                //Allows us to position the window before launching the FSCheck tests
                if (TestTask.IsCompleted || TestTask.IsCanceled || TestTask.IsFaulted)
                {
                    TestTask = null;
                }

                if (TestTask == null)
                {
                    PopulateTestTask();
                }

                TestTask.Start();
            }
        }

        public void Draw(MonoTestbed window)
        {
            if(polygonSetView != null)
                polygonSetView.Draw(window, this.scene);
        }
    }
}
