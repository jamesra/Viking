using FsCheck;
using Geometry;
using Rectangle = Geometry.Rectangle;
using Geometry.JSON;
using Geometry.Meshing;
using GeometryTests.Algorithms;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    enum DelaunayTestDataType
    {
        JSON,
        GRID,
        POLYGONS,
        RANDOM,
        JSON_POLYGON_CONSTRAINED,
        JSON_POLYGON_CONSTRAINED_FROMFILE,
        JSON_POLYGON_INTERSECTION,
        FSCHECK_DELAUNAY,
        FSCHECK_CONSTRAINED_DELAUNAY,
        FSCHECK_POLY_TRIANGULATION,
        FSCHECK_POLY_TRIANGULATION_WITH_INTERIOR_POINTS
    }

    public class FsCheckRunnerWithEvents : IRunner
    {
        public event OnFinishedDelegate OnFinished;
        public delegate void OnFinishedDelegate(string val, TestResult result);

        public FsCheckRunnerWithEvents(OnFinishedDelegate OnFinished)
        {
            this.OnFinished = OnFinished;
        }

        void IRunner.OnArguments(int value1, FSharpList<object> value2, FSharpFunc<int, FSharpFunc<FSharpList<object>, string>> value3) => FsCheck.Runner.consoleRunner.OnArguments(value1, value2, value3);

        void IRunner.OnFinished(string value1, TestResult result)
        {
            FsCheck.Runner.consoleRunner.OnFinished(value1, result);

            OnFinished?.Invoke(value1, result);
        }

        void IRunner.OnShrink(FSharpList<object> value1, FSharpFunc<FSharpList<object>, string> value2) => FsCheck.Runner.consoleRunner.OnShrink(value1, value2);

        void IRunner.OnStartFixture(Type value) => FsCheck.Runner.consoleRunner.OnStartFixture(value);
    }

    /// <summary>
    /// Visualizes the operation of the Delaunay Triangulation algorithm
    /// </summary>
    class VikingDelaunay2DTest : IGraphicsTest
    {
        private static readonly string DebugJSONA = null;

        private static readonly string DebugPolyArray7 = LoadTestdata("DebugPolyArray7.json");
        private static readonly string DebugConstrainedPoly4 = LoadTestdata("DebugConstrainedPoly4.json");
        private static string DebugJSONPoints = null;

        private static string LoadTestdata(string fileName)
        {
            string baseDir = AppContext.BaseDirectory;
            string path = System.IO.Path.Combine(baseDir, "Testdata", fileName);
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(baseDir, fileName);
            return System.IO.File.ReadAllText(path);
        }

        readonly TestInputContext Input = new();
        Scene scene;
        readonly PointSetViewCollection Points_A = new(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new(Color.Red, Color.Pink, Color.Plum);
        readonly PointSetViewCollection Points_C = new(Color.Green, Color.Pink, Color.GreenYellow);
        PointSet Points_X = [];
        readonly PointSetView Points_X_View = new(Color.DarkGray);
        readonly VikingDelaunayView PolyAView = new();
        readonly VikingDelaunayView PolyBView = new();
        readonly VikingDelaunayView PolyCView = new();
        Geometry.Vector2 Cursor = Geometry.Vector2.Zero;
        CircleView cursorView;
        LabelView cursorLabel;

        LineSetView LineSorterView = null;
        LabelView[] SortedLineLabels = null;

        LineSetView TriangulatedEdgeView = null;
        LabelView[] TriangulatedEdgeLabels = null;

        LabelView[] FaceLabels = null;

        MeshView<VertexPositionNormalColor> meshView = new();

        int[] sortedPointsByAngle = null;

        public double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized => _initialized;

        public string Title => this.GetType().Name;

        MonoTestbed Window;

        private bool ShowMeshFaces = false;
        //readonly DelaunayTestDataType testData = DelaunayTestDataType.FSCHECK_DELAUNAY;
        private readonly DelaunayTestDataType testData = DelaunayTestDataType.JSON_POLYGON_CONSTRAINED_FROMFILE;
        //////////////////////////////////////////////////////////

        ConstrainedDelaunayModel model;

        Task TestTask = null;

        private readonly string JSONFile = "PolygonDelaunayRepro.json";

        public Task Init(MonoTestbed window)
        {
            GeometryTests.FSCheck.TriangulatedMeshGenerators.OnProgress = this.OnTriangulationProgress;
            Points_X_View.LabelColor = Color.Gold;
            Points_X_View.LabelType = PointLabelType.INDEX;

            _initialized = true;
            Window = window;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            PolyAView.Color = Color.Red;
            PolyBView.Color = Color.Blue;
            PolyCView.Color = Color.Green;

            PolyCView.Polygon = GeometryJSONExtensions.PolygonFromJSON(DebugJSONA);

            if (PolyCView.Polygon != null)
            {
                scene.Camera.LookAt = PolyCView.Polygon.ExteriorRing[0].ToXNAVector2();
            }

            Input.UpdateTrackers();

            if (PolyCView.Polygon != null)
            {
                Points_C.Points = [.. PolyCView.Polygon.AllSegments.Select(l => l.A)];
                Cursor = PolyCView.Polygon.Centroid;
            }

            cursorView = new CircleView(new Circle(Cursor, PointRadius), Color.Gray);
            cursorLabel = new LabelView(Cursor.ToLabel(), Cursor);

            ResetTestTask();

            return Task.CompletedTask;
        }

        private void ResetTestTask()
        {
            List<Geometry.Vector2> listPoints = null;

            Rectangle rect = new(Geometry.Vector2.Zero, 50);

            if (testData == DelaunayTestDataType.JSON && DebugJSONPoints != null)
            {
                FirstTriangulationDone = true;

                listPoints = [.. GeometryJSONExtensions.PointsFromJSON(DebugJSONPoints)];
                //listPoints = listPoints.Scale(2, Geometry.Vector2.Zero).ToList();
                Geometry.Vector2 avg = listPoints.Average();
                //listPoints = listPoints.Select(p => p - avg).ToList();
                scene.Camera.LookAt = listPoints.Average().ToXNAVector2();
                rect = listPoints.BoundingBox();
                scene.Camera.Downsample = Math.Max(rect.Height, rect.Width) / Math.Min(scene.Viewport.Height, scene.Viewport.Width);

                TestTask = new Task<TriangulationMesh<IVertex2D>>(() => GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh([.. listPoints.Select(p => new TriangulationVertex(p))], OnTriangulationProgress));
            }
            else if (testData == DelaunayTestDataType.POLYGONS && DebugPolyArray7 != null)
            {
                Polygon[] polygons = GeometryJSONExtensions.PolygonsFromJSON(DebugPolyArray7);
                listPoints = [.. polygons.SelectMany(p => p.ExteriorRing.Union(p.InteriorRings.SelectMany(ir => ir))).Distinct()];

                Geometry.Vector2 avg = listPoints.Average();
                listPoints = [.. listPoints.Select(p => p - avg)];

                //scene.Camera.LookAt = listPoints[567].ToXNAVector2();//listPoints.Average().ToXNAVector2();
                scene.VisibleWorldBounds = listPoints.BoundingBox();
            }
            else if (testData == DelaunayTestDataType.JSON_POLYGON_CONSTRAINED)
            {
                Polygon polygon = GeometryJSONExtensions.PolygonFromJSON(DebugConstrainedPoly4);

                FirstTriangulationDone = true;

                TestTask = new Task(() =>
                {
                    var mesh = DelaunayTest.TriangulatePoly(polygon, out List<IEdgeKey> expectedConstrainedEdges, OnTriangulationProgress);
                    polygon.ValidatePolygonTriangulation(mesh, expectedConstrainedEdges);
                });

                scene.VisibleWorldBounds = polygon.BoundingBox;
            }
            else if (testData == DelaunayTestDataType.JSON_POLYGON_CONSTRAINED_FROMFILE)
            {
                FileInfo finfo = new(JSONFile);
                if (finfo.Exists == false)
                    throw new ArgumentException($"Input file {JSONFile} not found");

                string json = System.IO.File.ReadAllText(JSONFile);
                Polygon polygon = GeometryJSONExtensions.PolygonFromJSON(json);

                FirstTriangulationDone = true;

                TestTask = new Task(() =>
                {
                    var mesh = DelaunayTest.TriangulatePoly(polygon, out List<IEdgeKey> expectedConstrainedEdges, OnTriangulationProgress);
                    polygon.ValidatePolygonTriangulation(mesh, expectedConstrainedEdges);
                });

                //scene.Camera.LookAt = polygon.Centroid.ToXNAVector2();
                scene.VisibleWorldBounds = polygon.BoundingBox;
                //scene.Camera.Downsample = Math.Max(rect.Height, rect.Width) / Math.Min(scene.Viewport.Height, scene.Viewport.Width);
            }
            else if (testData == DelaunayTestDataType.FSCHECK_DELAUNAY)
            {
                FirstTriangulationDone = true;

                //Start a thread running FSCheck test cases
                TestTask = new Task(() =>
                {
                    DelaunayTest test = new();
                    test.DelaunayGeneratorParameterTestFromModel();
                });

                //T.Start();
            }
            else if (testData == DelaunayTestDataType.FSCHECK_CONSTRAINED_DELAUNAY)
            {
                FirstTriangulationDone = true;

                //Start a thread running FSCheck test cases
                TestTask = new Task(() =>
                {
                    DelaunayTest test = new();
                    test.ConstrainedDelaunayTestWithArbModel();
                });

                //T.Start();
            }
            else if (testData == DelaunayTestDataType.FSCHECK_POLY_TRIANGULATION)
            {
                FirstTriangulationDone = true;

                //Start a thread running FSCheck test cases
                TestTask = new Task(() =>
                {
                    DelaunayTest test = new();
                    DelaunayTest.TriangulatePolygonTest(this.OnTriangulationProgress);
                });

                //T.Start();
            }
            else if (testData == DelaunayTestDataType.FSCHECK_POLY_TRIANGULATION_WITH_INTERIOR_POINTS)
            {
                FirstTriangulationDone = true;

                //Start a thread running FSCheck test cases
                TestTask = new Task(() =>
                {
                    DelaunayTest test = new();
                    DelaunayTest.TriangulatePolygonTestWithInteriorPoints(this.OnTriangulationProgress);
                });

                //T.Start();
            }
            else
            {
                if (testData == DelaunayTestDataType.GRID)
                {

                    /*
                    listPoints = rect.Corners.ToList();
                    listPoints.Add(Geometry.Vector2.Zero);
                    listPoints.Add(new Geometry.Vector2(0, 15));
                    listPoints.Add(new Geometry.Vector2(0, -15));
                    listPoints.Add(new Geometry.Vector2(-5, 17.5));
                    */

                    listPoints = [];
                    int GridDims = 5;
                    for (int y = 0; y < GridDims; y++)
                    {
                        for (int x = 0; x < GridDims; x++)
                        {
                            //if(listPoints.Count < 4)
                            listPoints.Add(new Geometry.Vector2(x * 10, y * 10));

                        }
                    }
                }
                else if (testData == DelaunayTestDataType.RANDOM)
                {
                    // Random points 
                    int nPoints = 101;
                    //int nPoints = 30;
                    Geometry.Vector2[] points = new Geometry.Vector2[nPoints];
                    System.Random rand = new(1);
                    for (int i = 0; i < nPoints; i++)
                    {
                        double X = (rand.NextDouble() * rect.Width) + rect.Left;
                        double Y = (rand.NextDouble() * rect.Height) + rect.Bottom;
                        Geometry.Vector2 p = new(X, Y);
                        points[i] = p;
                    }

                    System.Array.Sort(points, new Vector2ComparerXY());

                    listPoints = [.. points];

                    scene.Camera.LookAt = Vector2.Zero;
                    scene.Camera.Downsample = rect.Height / scene.Viewport.Height;
                }
            }

            if (listPoints != null)
            {
                Points_X = [.. listPoints];
                Points_X_View.Points = Points_X;
                Points_X_View.LabelType = PointLabelType.INDEX;
            }
        }

        public void UnloadContent(MonoTestbed window)
        {
        }

        private readonly int? ConstrainedEdgeStart;


        private bool FirstTriangulationDone = false;
        public void Update()
        {
            if (!FirstTriangulationDone)
            {
                UpdateTriangulation();
                FirstTriangulationDone = true;
            }

            GamePadState state = Input.Update(scene);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToVector2();
                cursorView = new CircleView(new Circle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = 2,
                    Color = Color.Yellow
                };
            }

            if (state.Buttons.RightStick == ButtonState.Pressed)
            {
                Cursor = this.scene.Camera.LookAt.ToVector2();
                cursorView = new CircleView(new Circle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = 2,
                    Color = Color.Yellow
                };
            }

            if (TriangulationTask != null)
            {
                if (TriangulationTask.IsCompleted)
                {
                    trimesh = TriangulationTask.Result;
                    this.TriangulationTask = null;

                    model = new ConstrainedDelaunayModel(trimesh);

                    if (this.testData == DelaunayTestDataType.POLYGONS)
                    {
                        //TODO: Add constrained edges for the polygon rings
                    }
                }
            }

            bool UpdatePoints = Input.Gamepad.A_Clicked || Input.Gamepad.B_Clicked || Input.Gamepad.Y_Clicked;
            if (Input.Gamepad.A_Clicked)
            {
                if (TestTask is null)
                    ResetTestTask();

                //Allows us to position the window before launching the FSCheck tests
                TestTask?.Start();
                TestTask = null;
            }

            if (Input.Gamepad.B_Clicked)
            {
                /*
                Points_B.TogglePoint(Cursor);
                if (Points_B.Points.Count >= 3)
                {
                    PolyBView.Polygon = new Polygon(Points_B.Points.Points.EnsureClosedRing());
                }
                */
                ShowMeshFaces = !ShowMeshFaces;
            }

            if (Input.Gamepad.Y_Clicked)
            {
                /*if(!ConstrainedEdgeStart.HasValue)
{
    ConstrainedEdgeStart = 
}*/

                //trimesh.AddContrainedEdge(new Edge(1, 3 * (trimesh.Vertices.Count / 4)));
                trimesh?.AddConstrainedEdge(new ConstrainedEdge(0, 1), OnTriangulationProgress);
                /*
                Points_C.TogglePoint(Cursor);
                if (Points_C.Points.Count >= 3)
                    PolyCView.Polygon = new Polygon(Points_C.Points.Points.EnsureClosedRing());
                }
                */
            }

            if (Input.Gamepad.X_Clicked)
            {
                Points_X.Toggle(Cursor);
                UpdateTriangulation();
            }

            if (UpdatePoints || state.ThumbSticks.Left != Vector2.Zero)
            {

                if (Points_A.Points.Count > 0 && (Points_B.Points.Count > 0 || Points_C.Points.Count > 0))
                {
                    Geometry.Vector2[] points = [.. Points_A.Points.Points.Union(Points_B.Points.Points).Union(Points_C.Points.Points)];
                    Geometry.Vector2 origin = points[0];

                    Mesh2D mesh = new();
                    mesh.AddVerticies(points.Select(p => new Vertex2D(p)));

                    //OK, until we get Delaunay working, create an edge between all B Points to the first PointA.  Then sort and iterate the edges.
                    Geometry.Vector2[] connectedPoints = new Geometry.Vector2[points.Length - 1];
                    for (int iPoint = 1; iPoint < mesh.Vertices.Count; iPoint++)
                    {
                        mesh.AddEdge(new Edge(0, iPoint));
                        connectedPoints[iPoint - 1] = points[iPoint];
                    }

                    LineSorterView = new LineSetView
                    {
                        LineRadius = 1.5,
                        color = Color.Beige
                    };

                    Line originLine = new(origin, Cursor - mesh[0].Position == Geometry.Vector2.Zero ? Geometry.Vector2.UnitY : Geometry.Vector2.Normalize(Cursor - mesh[0].Position));

                    //Sort the edges in rotation order
                    CompareAngle compareAngle = new(originLine);
                    sortedPointsByAngle = connectedPoints.SortAndIndex(compareAngle);

                    LineSegment[] sortedLines = new LineSegment[sortedPointsByAngle.Length];
                    LineView[] lineViews = new LineView[sortedPointsByAngle.Length];

                    LabelView[] lineLabels = new LabelView[sortedPointsByAngle.Length];
                    for (int i = 0; i < sortedLines.Length; i++)
                    {
                        int iPoint = sortedPointsByAngle[i];
                        sortedLines[i] = new LineSegment(origin, connectedPoints[iPoint]);
                        lineViews[i] = new LineView(sortedLines[i], 1.5, Color.Beige.SetAlpha((float)(iPoint + 1) / (float)sortedLines.Length), LineStyle.Glow);
                        lineLabels[i] = new LabelView(iPoint.ToString(), sortedLines[i].PointAlongLine(0.33), scaleFontWithScene: true, fontSize: 2.0);
                    }

                    LineSorterView.LineViews = [.. lineViews];
                    SortedLineLabels = lineLabels;
                }
                else
                {
                    LineSorterView = null;
                    sortedPointsByAngle = null;
                }


            }
        }

        readonly System.Threading.ReaderWriterLockSlim RWLock = new();

        private void OnTriangulationProgress(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            UpdateTriangulationViews(mesh);
            System.Threading.Thread.Sleep(0);
        }


        private void UpdateTriangulationViews(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            try
            {
                RWLock.EnterWriteLock();

                if (mesh.Vertices.Count == 0)
                {
                    Points_X = [];
                    TriangulatedEdgeView = new LineSetView();
                    FaceLabels = new LabelView[mesh.Faces.Count];
                    meshView = new MeshView<VertexPositionNormalColor>();
                    return;
                }

                if ((Points_X.Count != mesh.Vertices.Count) || (Points_X.Points.First() != mesh.Vertices.First().Position))
                {
                    Points_X = [.. mesh.Vertices.Select(v => v.Position)];
                    Points_X_View.Points = Points_X;
                    Points_X_View.LabelType = PointLabelType.INDEX;

                    Rectangle rect = Points_X.BoundingBox();
                    rect = Rectangle.Scale(rect, 1.05);
                    scene.Camera.LookAt = rect.Center.ToXNAVector2();
                    scene.Camera.Downsample = rect.Height / scene.Viewport.Height;
                }

                LineView[] lineViews = new LineView[mesh.Edges.Count];
                LabelView[] lineLabels = new LabelView[lineViews.Length];
                LineSegment[] sortedLines = new LineSegment[lineViews.Length];

                TriangulatedEdgeView = new LineSetView
                {
                    LineRadius = 1.5,
                    color = Color.Beige
                };

                var edgeKeys = mesh.Edges.Keys.ToArray();
                for (int i = 0; i < lineViews.Length; i++)
                {
                    IEdgeKey key = edgeKeys[i];
                    sortedLines[i] = mesh.ToLineSegment(key);
                    lineViews[i] = new LineView(sortedLines[i], 1.5, mesh[key] as ConstrainedEdge != null ? Color.Yellow : Color.LightGray, LineStyle.Standard);
                    /*
                    lineLabels[i] = new CurveLabel(key.ToString(),
                        new Geometry.Vector2[] { sortedLines[i].A, sortedLines[i].B },
                        Color.Black,
                        TryToClose: false, lineWidth: this.PointRadius,
                        numInterpolations: 0); // key.ToString(), sortedLines[i].PointAlongLine(0.5), scaleFontWithScene: true, fontSize: 2.0);
                    */

                    //lineLabels[i] = new LabelView(key.ToString(), sortedLines[i].PointAlongLine(0.5), scaleFontWithScene: true, fontSize: 2.0);
                    lineLabels[i] = new LabelView(key.ToString(), sortedLines[i], scaleFontWithScene: true, lineWidth: 2.0);
                }

                FaceLabels = new LabelView[mesh.Faces.Count];
                FaceLabels = [.. mesh.Faces.Select((f, i) => FaceLabels[i] = new LabelView(f.ToString(),
                                                                         new Triangle([.. f.iVerts.Select(iVert => mesh[iVert].Position)]).BaryToVector(new Geometry.Vector2(1 / 3.0, 1 / 3.0)),
                                                                         mesh.IsClockwise(f) ? Color.Red.SetAlpha(0.75f) : Color.LightBlue.SetAlpha(0.75f),
                                                                         scaleFontWithScene: true,
                                                                         fontSize: 2.0)
                                 )];


                /*foreach (LabelView label in FaceLabels)
                {
                    label.Color = Color.Blue.SetAlpha(0.5f);
                }*/

                TriangulatedEdgeView.LineViews = [.. lineViews];
                TriangulatedEdgeLabels = lineLabels;

                meshView = new MeshView<VertexPositionNormalColor>();
                MeshModel<VertexPositionNormalColor> model = CreateMeshModel(mesh);
                meshView.models.Add(model);

            }
            finally
            {
                RWLock.ExitWriteLock();
            }
        }

        static MeshModel<VertexPositionNormalColor> CreateMeshModel(IReadOnlyMesh2D<IVertex2D> mesh)
        {
            MeshModel<VertexPositionNormalColor> model = new()
            {
                Vertices = [.. mesh.Vertices.Select(v => new VertexPositionNormalColor(v.Position.ToXNAVector3(0), Vector3.UnitZ, ColorExtensions.Random().SetAlpha(0.5f)))],
                Edges = [.. mesh.Faces.SelectMany(f => f.iVerts)]
            };
            return model;
        }

        private Task<TriangulationMesh<IVertex2D>> TriangulationTask = null;

        TriangulationMesh<IVertex2D> trimesh = null;

        private void UpdateTriangulation()
        {
            if (Points_X.Points.Count >= 3)
            {
                //PolyXView.Polygon = new Polygon(Points_C.Points.Points.EnsureClosedRing());

                if (TriangulationTask is null)
                {
                    TriangulationTask = new Task<TriangulationMesh<IVertex2D>>(() => GenericDelaunayMeshGenerator2D<IVertex2D>.TriangulateToMesh([.. Points_X.Points.Select(p => new TriangulationVertex(p))], OnTriangulationProgress));
                    TriangulationTask.Start();
                }

                /*
                var lineViews = new LineView[mesh.Edges.Count];
                var sortedLines = new LineSegment[lineViews.Length];
                var lineLabels = new LabelView[lineViews.Length];

                TriangulatedEdgeView = new LineSetView();
                TriangulatedEdgeView.LineRadius = 1.5;
                TriangulatedEdgeView.color = Color.Beige;

                var edgeKeys = mesh.Edges.Keys;
                for (int i = 0; i < lineViews.Length; i++)
                {
                    IEdgeKey key = edgeKeys[i];
                    sortedLines[i] = mesh.ToLineSegment(key);
                    lineViews[i] = new LineView(sortedLines[i], 1.5, Color.LightGray, LineStyle.Glow);
                    lineLabels[i] = new LabelView(key.ToString(), sortedLines[i].PointAlongLine(0.5), scaleFontWithScene: true, fontSize: 2.0);
                }

                TriangulatedEdgeView.LineViews = lineViews.ToList();
                TriangulatedEdgeLabels = lineLabels;
                */
            }
            else
            {
                TriangulatedEdgeLabels = null;
                TriangulatedEdgeView = null;
            }
        }

        public void Draw(MonoTestbed window)
        {
            try
            {
                RWLock.EnterReadLock();

                if (cursorView != null)
                    CircleView.Draw(window.GraphicsDevice, this.scene, OverlayStyle.Alpha, new CircleView[] { cursorView });

                Points_A.Draw(window, scene);
                Points_B.Draw(window, scene);
                Points_C.Draw(window, scene);
                Points_X_View.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);

                PolyAView.Draw(window);
                PolyBView.Draw(window);
                PolyCView.Draw(window);

                if (sortedPointsByAngle != null)
                {
                    LineView.Draw(window.GraphicsDevice, this.scene, window.lineManager, [.. this.LineSorterView.LineViews]);
                    LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, this.SortedLineLabels);
                }

                if (TriangulatedEdgeView != null && ShowMeshFaces == false)
                {
                    LineView.Draw(window.GraphicsDevice, this.scene, window.lineManager, [.. this.TriangulatedEdgeView.LineViews]);
                    LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, this.TriangulatedEdgeLabels);
                    //CurveLabel.Draw(window.GraphicsDevice, this.scene, window.spriteBatch, window.fontArial, window.curveManager, this.TriangulatedEdgeLabels);
                }

                if (FaceLabels != null)
                {
                    LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, this.FaceLabels);
                }

                if (cursorLabel != null)
                    LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, new LabelView[] { cursorLabel });

                if (meshView != null && ShowMeshFaces)
                {
                    meshView.Draw(window.GraphicsDevice, window.Scene, Microsoft.Xna.Framework.Graphics.CullMode.None);
                }
            }
            catch (System.ApplicationException e)
            {
                return;
            }
            finally
            {
                RWLock.ExitReadLock();
            }
        }

    }
}
