using FsCheck;
using Geometry;
using Geometry.JSON;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GeometryTests;
using Newtonsoft.Json.Linq;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    enum PolygonIntersectionTestDataSource
    {
        JSON_POLYGON_INTERSECTION,
        FS_CHECK,
        JSON_FILE
    }

    public class PolygonIntersectionView
    {
        readonly PolygonSetView PolygonsView = null;
         

        public PolygonIntersectionView(GridPolygon[] polygons)
        {
            PolygonsView = new PolygonSetView(polygons, PolygonSetView.DefaultColorMapping)
            {
                PointLabelType = IndexLabelType.POLYGON | IndexLabelType.POSITION
            };
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

        private readonly string JSONFile = "PolygonIntersectionRepro.json";

        Scene scene;
        readonly Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();
        readonly GamePadStateTracker Gamepad = new GamePadStateTracker();
        //readonly PolygonIntersectionTestDataSource TestType = PolygonIntersectionTestDataSource.FS_CHECK;
        readonly PolygonIntersectionTestDataSource TestType = PolygonIntersectionTestDataSource.JSON_FILE;
        //readonly PolygonIntersectionTestDataType TestType = PolygonIntersectionTestDataType.JSON_POLYGON_INTERSECTION;

        Task TestTask = null;

        PolygonIntersectionView polygonSetView = null;

        private static readonly string[] PolygonIntersections1 = new string[]
        {
            "{\"ExteriorRing\": [{\"X\": -30.0,\"Y\": 70.0},{\"X\": -93.928035982008993,\"Y\": -77.526236881559214},{\"X\": -95.0,\"Y\": -80.0},{\"X\": -91.377245508982043,\"Y\": -76.487025948103792},{\"X\": 70.0,\"Y\": 80.0},{\"X\": -30.0,\"Y\": 70.0}],\"InteriorRings\": []}",
            "{ \"ExteriorRing\": [{\"X\": -100.0,\"Y\": -80.0},{\"X\": -95.0,\"Y\": -80.0},{\"X\": 35.0,\"Y\": -25.0},{\"X\": -91.377245508982043,\"Y\": -76.487025948103792},{\"X\": -93.928035982008993,\"Y\": -77.526236881559214},    {\"X\": -100.0,\"Y\": -80.0}],\"InteriorRings\": []}"
        };

        public Task Init(MonoTestbed window)
        {
            _initialized = true;
            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            TestTask = PopulateTestTask();
            return TestTask;
        }

        private async Task PopulateTestTask()
        {
            GridRectangle rect = new GridRectangle(GridVector2.Zero, 50);
            GridPolygon[] polygons = Array.Empty<GridPolygon>();
            if (TestType == PolygonIntersectionTestDataSource.JSON_POLYGON_INTERSECTION)
            {
                polygons = PolygonIntersections1.Select(s => GeometryJSONExtensions.PolygonFromJSON(s)).ToArray();
                 
                //FirstTriangulationDone = true;  
                
            }
            else if (TestType == PolygonIntersectionTestDataSource.FS_CHECK)
            {
                TestTask = Task.Run(() => {
                    GeometryTests.GridPolygonTest.TestPolygonGeneratorUnderpinnings(this.OnPolygonUpdate);
                }); 
            }
            else if (TestType == PolygonIntersectionTestDataSource.JSON_FILE)
            {
                FileInfo finfo = new FileInfo(JSONFile);
                if (finfo.Exists == false)
                    throw new ArgumentException($"Input file {JSONFile} not found");

                string json = System.IO.File.ReadAllText(JSONFile);
                polygons = GeometryJSONExtensions.PolygonsFromJSON(json);
            }

            if (polygons != null && polygons.Length > 0)
            { 
                rect = polygons.BoundingBox();
                scene.VisibleWorldBounds = rect;
                //scene.Camera.LookAt = rect.Center.ToXNAVector2();
                //scene.Camera.Downsample = Math.Max(rect.Height, rect.Width) / Math.Min(scene.Viewport.Height, scene.Viewport.Width);

                polygonSetView = new PolygonIntersectionView(polygons);

                TestTask = Task.Run(() => {
                    polygonSetView = new PolygonIntersectionView(polygons);
                    var result = GridPolygonTest.AssessPolygonIntersectionAndCorrespondancePoints(polygons[0], polygons[1], OnPolygonUpdate);
                    result.VerboseCheckThrowOnFailure();
                });
            }

            await TestTask;
        }


        readonly AnnotationVizLib.MorphologyGraph graph;
        private void PopulateFromOData()
        {

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
                    TestTask = PopulateTestTask();
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
