using Geometry;
using Rectangle = Geometry.Rectangle;
using Geometry.Meshing;
using Microsoft.SqlServer.Types;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MorphologyMesh;
using SqlGeometryUtils;
using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TriangleNet;
using TriangleNet.Meshing;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    /*
    class MeshMergeIncrementalView
    {
        public Polygon[] Polys;
        public GraphLib.Edge<ulong>[] Edges;
        public double[] ZLevels;

        private readonly PolygonSetView PolyViews; 

        private readonly LineSetView TrianglesView = new LineSetView();

        private PointSetView StartingIndexView = null;
        
        private LineView StartingLine = null;

        public bool ShowFinalLines = false;

        public int NumLinesToDraw
        {
            get { return _NumLinesToDraw; }
            set
            {
                _NumLinesToDraw = value;
                UpdateLinesToDraw();
            }
        }

        private int _NumLinesToDraw = 1;
        private LineView[] lineViewsToDraw = null;
        readonly List<LineView> polyRingViews;

        public Color Color
        {
            get { return TrianglesView.color; }
            set
            {
                TrianglesView.color = value;
            }
        }

        public MeshMergeIncrementalView(Polygon[] polys, GraphLib.Edge<ulong>[] edges, double[] Z)
        {
            this.Polys = polys;
            this.Edges = edges;
            this.ZLevels = Z; 
            this.Color = Color.Blue;

            PolyViews = new PolygonSetView(Polys, PolygonSetView.DefaultColorMapping); 

            UpdateWrapping();
        }

        public void UpdateWrapping()
        {
            StartingIndexView = new PointSetView
            {
                Color = Color.Yellow,
                PointRadius = 7.5
            };

            MeshGraph graph = StandardModels.BuildMeshGraph(this.Polys, this.ZLevels, this.Edges, 10, Geometry.Vector3.Zero);

            Mesh3D<IVertex3D<ulong>> CompositeMesh = SmoothMeshGenerator.Generate(graph, out List<LineSegment> addedMeshEdges);

            //LineSegment[] lines = CompositeMesh.Edges.Values.Select(e => new LineSegment(CompositeMesh.Vertices[e.A].Position, CompositeMesh.Vertices[e.B].Position)).ToArray();

            StartingLine = new LineView(addedMeshEdges[0], 2, Color.Red, LineStyle.Standard);

            TrianglesView.UpdateViews(addedMeshEdges);
            UpdateLinesToDraw();
        }

        private void UpdateLinesToDraw()
        {
            if (TrianglesView is null)
            {
                lineViewsToDraw = null;
            }

            if (_NumLinesToDraw < 0)
                _NumLinesToDraw = TrianglesView.LineViews.Count;

            if (_NumLinesToDraw > TrianglesView.LineViews.Count)
            {
                _NumLinesToDraw = 0;
            }

            lineViewsToDraw = new LineView[_NumLinesToDraw];
            TrianglesView.LineViews.CopyTo(0, lineViewsToDraw, 0, NumLinesToDraw);

            TrianglesView.color = Color.Gray;

            foreach (LineView view in lineViewsToDraw)
            {
                view.Color = Color.Yellow;
            }
        }


        public void Draw(MonoTestbed window, Scene scene)
        {
            PolyViews?.Draw(window, scene);

            if (TrianglesView != null && ShowFinalLines)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, TrianglesView.LineViews.ToArray());
            }

            if (lineViewsToDraw != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, lineViewsToDraw);
            }

            if (StartingLine != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, new LineView[] { StartingLine });
            }

            //if (StartingIndexView != null)
            //    StartingIndexView.Draw(window, scene);
        }
    }
    */
    /*
    class TriangulationShapeWrapView
    {
        public Polygon A;
        public Polygon B;

        MeshNode NodeA;
        MeshNode NodeB;

        private readonly LineSetView TrianglesView = new LineSetView();

        private PointSetView StartingIndexView = null;

        private PointSetView PolyA = new PointSetView();
        private PointSetView PolyB = new PointSetView();

        private LineView StartingLine = null;

        public bool ShowFinalLines = false;

        public int NumLinesToDraw
        {
            get { return _NumLinesToDraw; }
            set
            {
                _NumLinesToDraw = value;
                UpdateLinesToDraw();
            }
        }

        private int _NumLinesToDraw = 1;
        private LineView[] lineViewsToDraw = null;
        readonly List<LineView> polyRingViews; 

        public Color Color
        {
            get { return TrianglesView.color; }
            set
            {
                TrianglesView.color = value;
            }
        }

        public TriangulationShapeWrapView(Polygon a, Polygon b)
        {
            this.A = a;
            this.B = b;
            this.Color = Color.Blue;
            
            UpdateWrapping();

            polyRingViews = new List<LineView>();
            polyRingViews.AddRange(a.AllSegments.Select(s => new LineView(s, 1, Color.Red.SetAlpha(0.5f), LineStyle.Standard)));
            polyRingViews.AddRange(b.AllSegments.Select(s => new LineView(s, 1, Color.Blue.SetAlpha(0.5f), LineStyle.Standard)));
        }

        public void UpdateWrapping()
        {
            StartingIndexView = new PointSetView
            {
                Color = Color.Yellow,
                PointRadius = 7.5
            };

            PolyA = new PointSetView
            {
                Color = Color.AliceBlue,
                LabelIndex = true
            };

            PolyB = new PointSetView
            {
                Color = Color.Green,
                LabelIndex = true
            };

            TrianglesView.color = Color.Gray;

            NodeA = MorphologyMesh.SmoothMeshGraphGenerator.CreateNode(0, A, 0, true);
            NodeB = MorphologyMesh.SmoothMeshGraphGenerator.CreateNode(1, B, 1, true);

            ConnectionVertices PortA = MorphologyMesh.SmoothMeshGraphGenerator.CreatePort(A, true);
            ConnectionVertices PortB = MorphologyMesh.SmoothMeshGraphGenerator.CreatePort(B, true);

            MeshEdge edge = new MeshEdge(0, 1, PortA, PortB);

            MeshGraph graph = new MeshGraph();

            graph.AddNode(NodeA);
            graph.AddNode(NodeB);
            graph.AddEdge(edge);

            MeshNode Source = graph.Nodes[edge.SourceNodeKey];
            MeshNode Target = graph.Nodes[edge.TargetNodeKey];

            Mesh3D<IVertex3D<ulong>> CompositeMesh = MorphologyMesh.SmoothMeshGenerator.MergeMeshes(Source, Target);

            bool SourceIsUpper = Source.IsNodeBelow(Target);

            Geometry.Vector2[] UpperVerticies = edge.SourcePort.ExternalBorder.Select(i => new Geometry.Vector2(CompositeMesh.Vertices[(int)i].Position.X, CompositeMesh.Vertices[(int)i].Position.Y)).ToArray();
            Geometry.Vector2[] LowerVerticies = edge.TargetPort.ExternalBorder.Select(i => new Geometry.Vector2(CompositeMesh.Vertices[(int)i].Position.X, CompositeMesh.Vertices[(int)i].Position.Y)).ToArray();

            long UpperStart = SmoothMeshGenerator.FirstIndex(UpperVerticies, out Geometry.Vector2 UpperPortConvexHullCentroid);
            long LowerStart = SmoothMeshGenerator.FirstIndex(LowerVerticies, out Geometry.Vector2 LowerPortConvexHullCentroid);

            //UpperVerticies = UpperVerticies.Translate(-UpperPortConvexHullCentroid);
            //LowerVerticies = LowerVerticies.Translate(-LowerPortConvexHullCentroid);

            PolyA.Points = UpperVerticies;
            PolyB.Points = LowerVerticies;

            Trace.WriteLine("Upper Start " + UpperStart.ToString());
            Trace.WriteLine("Lower Start " + LowerStart.ToString());

            List<Geometry.Vector2> startingPoints = new List<Geometry.Vector2>();
            long UpperStartVertex = edge.SourcePort.ExternalBorder[(int)UpperStart];
            long LowerStartVertex = edge.TargetPort.ExternalBorder[(int)LowerStart];

            Trace.WriteLine("Upper Start Mesh Index " + UpperStartVertex.ToString());
            Trace.WriteLine("Lower Start Mesh Index " + LowerStartVertex.ToString());

            startingPoints.Add(CompositeMesh.Vertices[(int)UpperStartVertex].Position.XY());
            startingPoints.Add(CompositeMesh.Vertices[(int)LowerStartVertex].Position.XY());

            StartingIndexView.Points = startingPoints; 

            List<LineSegment> CreatedLines = MorphologyMesh.SmoothMeshGenerator.AttachPorts(CompositeMesh,
                        SourceIsUpper ? edge.TargetPort : edge.SourcePort,
                        SourceIsUpper ? edge.SourcePort : edge.TargetPort);

            //LineSegment[] lines = CompositeMesh.Edges.Values.Select(e => new LineSegment(CompositeMesh.Vertices[e.A].Position, CompositeMesh.Vertices[e.B].Position)).ToArray();

            StartingLine = new LineView(UpperVerticies[UpperStart], LowerVerticies[LowerStart], 2, Color.Red, LineStyle.Standard);
             
            TrianglesView.UpdateViews(CreatedLines);
            UpdateLinesToDraw();
        }

        private void UpdateLinesToDraw()
        {
            if (TrianglesView is null)
            {
                lineViewsToDraw = null;
            }

            if (_NumLinesToDraw < 0)
                _NumLinesToDraw = TrianglesView.LineViews.Count;

            if (_NumLinesToDraw > TrianglesView.LineViews.Count)
            {
                _NumLinesToDraw = 0;
            } 

            lineViewsToDraw = new LineView[_NumLinesToDraw];
            TrianglesView.LineViews.CopyTo(0, lineViewsToDraw, 0, NumLinesToDraw);

            TrianglesView.color = Color.Gray;

            foreach (LineView view in lineViewsToDraw)
            {
                view.Color = Color.Yellow;
            }
        }


        public void Draw(MonoTestbed window, Scene scene)
        { 
            if(TrianglesView != null && ShowFinalLines)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, TrianglesView.LineViews.ToArray());
            }

            if (lineViewsToDraw != null)
            {  
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, lineViewsToDraw);
            }

            if (polyRingViews != null)
            {
                LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, polyRingViews.ToArray());
            }

            PolyA?.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);

            PolyB?.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha); 

            if(StartingLine != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, new LineView[] { StartingLine });
            }

            //if (StartingIndexView != null)
            //    StartingIndexView.Draw(window, scene);
        }
    }
    */
    class PolywrapView
    {
        public Polygon A;
        public Polygon B;

        private readonly LineSetView TrianglesView = new LineSetView();

        public Color Color
        {
            get { return TrianglesView.color; }
            set
            {
                TrianglesView.color = value;
            }
        }

        public PolywrapView(Polygon a, Polygon b)
        {
            this.A = a;
            this.B = b;
            this.Color = Color.Blue;

            UpdateWrapping();
        }

        public void UpdateWrapping()
        {
            Polygon[] shapes = new Polygon[] { A, B };

            List<Geometry.Vector2> points = A.ExteriorRing.ToList();
            points.AddRange(B.ExteriorRing);

            IMesh mesh = points.Triangulate();

            List<LineSegment> lines = mesh.ToLines();
            
            //Figure out which endpoints are included in the wrapping.
            //Lines between polygons are included.
            for(int i = lines.Count-1; i >= 0; i--)
            {
                LineSegment l = lines[i];

                if (A.ExteriorSegments.Contains(l) || B.ExteriorSegments.Contains(l))
                    continue;                

                if(A.ExteriorRing.Contains(l.A) &&
                   A.ExteriorRing.Contains(l.B))
                {
                    lines.RemoveAt(i);
                }

                if (B.ExteriorRing.Contains(l.A) &&
                    B.ExteriorRing.Contains(l.B))
                {
                    lines.RemoveAt(i);
                }
            }
            
            TrianglesView.UpdateViews(lines);
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (TrianglesView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, TrianglesView.LineViews.ToArray());
        }
    }

    /// <summary>
    /// This tests how we create faces that connect two polygons at different Z levels
    /// </summary>
    class PolywrappingTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;

        static string LoadWkt(string name) =>
            File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "Testdata", name));

        static readonly string PolyA = LoadWkt("PolyA.wkt");
        static readonly string PolyB = LoadWkt("PolyB.wkt");

        readonly TestInputContext Input = new();
        Scene scene;
        Polygon A;
        Polygon B;
        readonly PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        readonly PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);
        //TriangulationShapeWrapView wrapView = null;
        PolywrapView wrapView = null; 

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }
         
        public Task Init(MonoTestbed window)
        {
            window.Scene.RestoreCamera(TestMode.POLYWRAPPING);
            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Input.UpdateTrackers();

            A = SqlGeometry.STPolyFromText(PolyA.ToSqlChars(), 0).ToPolygon();
            B = SqlGeometry.STPolyFromText(PolyB.ToSqlChars(), 0).ToPolygon();

            Geometry.Vector2 Centroid = A.Centroid;
            A = A.Translate(-Centroid);
            B = B.Translate(-Centroid);

            Points_A.Points = new PointSet(A.ExteriorRing);
            Points_B.Points = new PointSet(B.ExteriorRing);

            wrapView = new PolywrapView(A, B);

            _initialized = true;
            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {
            this.scene?.SaveCamera(TestMode.POLYWRAPPING);
        }

        public void Update()
        {
            GamePadState state = Input.Update(scene);
        }

        public void Draw(MonoTestbed window)
        { 
            wrapView?.Draw(window, scene);
        }
    }
}
