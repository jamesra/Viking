using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using VikingXNAGraphics;
using VikingXNA;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Geometry.Meshing;
using MathNet.Numerics.LinearAlgebra;
using AnnotationVizLib.SimpleOData;
using TriangleNet;


namespace MonogameTestbed
{
    public static class GridVector2LabelExtensions
    {
        public static string ToLabel(this GridVector2 p)
        {
            return string.Format("{0:F2} {1:F2}", p.X, p.Y);
        }
    }
    class PointSet
    {
        public double PointRadius = 2.0;
        public List<GridCircle> Circles = new List<GridCircle>();
        public ICollection<GridVector2> Points
        {
            get
            {
               return Circles.Select(c => c.Center).ToList();
            }
        }

        /// <summary>
        /// Add or remove a point from the list
        /// </summary>
        /// <param name="p"></param>
        public void Toggle(GridVector2 p)
        {
            GridCircle newCircle = new GridCircle(p, PointRadius);
            if (Circles.Any(c => c.Intersects(newCircle)))
            {
                Circles.RemoveAll(c => c.Intersects(newCircle));
            }
            else
            {
                Circles.Add(newCircle);
            }
        }
    }

    class PointSetView
    {
        public CircleView[] PointViews = new CircleView[0];
        public LabelView[] LabelViews = new LabelView[0];
        public Color color;
      
        public void UpdateViews(ICollection<GridCircle> Points)
        {
            PointViews = Points.Select(c => new CircleView(c, color)).ToArray();
            LabelViews = Points.Select(c => new LabelView(c.Center.ToLabel(), c.Center)).ToArray();

            foreach(LabelView label in LabelViews)
            {
                label.FontSize = 2; 
            }
        } 
    }
    
    class LineSetView
    {
        public List<LineView> LineViews = new List<LineView>();
        public double LineRadius = 1;
        public Color color;

        public void UpdateViews(ICollection<GridVector2> Points)
        {
            if (Points.Count >= 3)
            {
                TriangleNet.Voronoi.VoronoiBase v = Points.Voronoi();
                UpdateViews(v);
            }
            else
            {
                LineViews = new List<LineView>();
            }
        }

        public void UpdateViews(TriangleNet.Voronoi.VoronoiBase v)
        {
            if (v != null)
            { 
                LineViews = ToLines(v, color);
            }
            else
            {
                LineViews = new List<LineView>();
            }
        }

        public void UpdateViews(ICollection<GridLineSegment> lines)
        {
            if (lines != null)
            {
                LineViews = lines.Select(l => new LineView(l.A, l.B, LineRadius, color, LineStyle.Standard, false)).ToList();

            }
            else
            {
                LineViews = new List<LineView>();
            }
        }

        private List<LineView> ToLines(TriangleNet.Topology.DCEL.DcelMesh mesh, Color color)
        {
            List<LineView> listLines = new List<LineView>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new LineView(mesh.Vertices[e.P0].ToGridVector2(),
                                           mesh.Vertices[e.P1].ToGridVector2(),
                                           LineRadius,
                                           color,
                                           LineStyle.Standard,
                                           false));
            }

            return listLines;
        }
    }

    class ConvexHullView
    {
        public List<LineView> LineViews = new List<LineView>();
        public double LineRadius = 1;
        public Color color;

        public List<LineView> UpdateViews(IReadOnlyList<GridVector2> Points)
        {
            int[] original_indicies;
            GridVector2[] cv_points = ConvexHullExtension.ConvexHull(Points, out original_indicies);

            List<LineView> listLines = new List<LineView>();

            for (int i = 0; i < cv_points.Length-1; i++)
            {
                listLines.Add(new LineView(cv_points[i],
                                           cv_points[i + 1],
                                           LineRadius,
                                           color,
                                           LineStyle.Standard,
                                           false));
            }

            LineViews = listLines;
            return listLines;
        }

        private List<LineView> ToLines(TriangleNet.Topology.DCEL.DcelMesh mesh, Color color)
        {
            List<LineView> listLines = new List<LineView>();
            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            foreach (var e in mesh.Edges)
            {
                listLines.Add(new LineView(mesh.Vertices[e.P0].ToGridVector2(),
                                           mesh.Vertices[e.P1].ToGridVector2(),
                                           LineRadius,
                                           color,
                                           LineStyle.Standard,
                                           false));
            }

            return listLines;
        }
    }

    class PointSetViewCollection
    {
        public PointSet Points = new PointSet();
        private PointSetView PointsView = new PointSetView();
        private LineSetView VoronoiView = new LineSetView();
        ConvexHullView CVView = new ConvexHullView();

        public PointSetViewCollection(Color PointColor, Color VoronoiColor, Color CVViewColor)
        {
            PointsView.color = PointColor;
            VoronoiView.color = VoronoiColor;
            CVView.color = CVViewColor;
        }

        public void TogglePoint(GridVector2 p)
        {
            Points.Toggle(p);

            PointsView.UpdateViews(Points.Circles);
            VoronoiView.UpdateViews(Points.Points);
            CVView.UpdateViews(Points.Points.ToArray());
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
  //          if(VoronoiView.LineViews != null)
  //              LineView.Draw(window.GraphicsDevice, scene, window.lineManager, VoronoiView.LineViews.ToArray());

            if(PointsView.PointViews != null)
                CircleView.Draw(window.GraphicsDevice, scene, window.basicEffect, window.overlayEffect, PointsView.PointViews);

            if (PointsView.LabelViews != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, PointsView.LabelViews);

            if (CVView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, CVView.LineViews.ToArray());
        }
    }


    class PolygonDividerView
    {
        public List<PointSet> Sets = new List<PointSet>();

        public List<GridVector2[]> ConvexHulls = new List<GridVector2[]>();

        public TriangleNet.Voronoi.VoronoiBase Voronoi;

        public LineSetView VoronoiView = new LineSetView();
        public LineSetView BoundaryView = new LineSetView();

        public List<LabelView> listLabels = new List<LabelView>();

        public Color Color
        {
            get { return BoundaryView.color; }
            set
            {
                BoundaryView.color = value;
                VoronoiView.color = value;
            }
        }

        public int AddSet(PointSet set)
        {
            Sets.Add(set);
            ConvexHulls.Add(new GridVector2[0]);
            UpdateSet(set, Sets.Count - 1);
            return Sets.Count - 1; 
        }

        /// <summary>
        /// When a pointset changes we need to recalculate the dividing line between convex hulls
        /// </summary>
        public void UpdateSet(PointSet ps, int i)
        {
            int[] original_indicies; 
            ConvexHulls[i] = ConvexHullExtension.ConvexHull(ps.Points.ToArray(), out original_indicies);
            Voronoi = MeshingExperimentExtensions.ConvexHullVoronoi(ConvexHulls);

            List<GridLineSegment> listBoundaryLines = StripNonBoundaryLines(Voronoi);
            VoronoiView.UpdateViews(Voronoi.ToLines());
            VoronoiView.color = Color.Gray;
            BoundaryView.UpdateViews(listBoundaryLines);
            listLabels = LabelDistances();
        }
         
        private List<LabelView> LabelDistances()
        {
            List<LabelView> labels = new List<LabelView>();
            for(int i = 0; i < ConvexHulls.Count; i++)
            {
                if (ConvexHulls[i] == null || ConvexHulls[i].Length <= 3)
                    continue;

                GridPolygon iPoly = new GridPolygon(ConvexHulls[i]);

                for(int j = i+1; j < ConvexHulls.Count; j++)
                {
                    if (ConvexHulls[j] == null || ConvexHulls[j].Length <= 3)
                        continue;

                    GridPolygon jPoly = new GridPolygon(ConvexHulls[j]);

                    double minDistance = iPoly.Distance(jPoly);

                    LabelView newLabel = new LabelView(minDistance.ToString(), (iPoly.Centroid + jPoly.Centroid) / 2.0);
                    newLabel.FontSize /= 4.0;

                    labels.Add(newLabel);
                }
            }

            return labels; 
        }
        
        private List<GridLineSegment> StripNonBoundaryLines(TriangleNet.Voronoi.VoronoiBase voronoi)
        {
            if (voronoi == null)
                return null;

            //Build a set of LineSegments
            List<GridLineSegment> lines = voronoi.ToLines();
            if (lines == null) 
                return null;

            if (lines.Count == 0)
                return new List<GridLineSegment>();

            for (int i = 0; i < ConvexHulls.Count; i++)
            {
                GridVector2[] cv_points = ConvexHulls[i];
                if (cv_points == null || cv_points.Length <= 1)
                    continue;

                GridPolygon convexhull = new GridPolygon(cv_points);
                
                lines.RemoveAll(voronoi_line => voronoi_line.Intersects(convexhull));
                lines.RemoveAll(voronoi_line => convexhull.Contains(voronoi_line.A) || convexhull.Contains(voronoi_line.B));
            }

            return lines;
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (VoronoiView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, VoronoiView.LineViews.ToArray());

            if (BoundaryView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, BoundaryView.LineViews.ToArray());

            if (listLabels != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, listLabels);
        }
    }


    class PolygonBorderView
    {
        public List<PointSet> Sets = new List<PointSet>();

        public List<GridVector2[]> ConvexHulls = new List<GridVector2[]>();

        public TriangleNet.Voronoi.VoronoiBase Voronoi;

        public LineSetView VoronoiView = new LineSetView();
        public LineSetView DelaunayView = new LineSetView();
        public LineSetView BoundaryView = new LineSetView();

        public List<LabelView> listLabels = new List<LabelView>();

        public Color Color
        {
            get { return BoundaryView.color; }
            set
            {
                BoundaryView.color = value;
                VoronoiView.color = value;
            }
        }

        public int AddSet(PointSet set)
        {
            Sets.Add(set);
            ConvexHulls.Add(new GridVector2[0]);
            UpdateSet(set, Sets.Count - 1);
            return Sets.Count - 1;
        }

        /// <summary>
        /// When a pointset changes we need to recalculate the dividing line between convex hulls
        /// </summary>
        public void UpdateSet(PointSet ps, int i)
        {
            int[] original_indicies;

            Sets[i] = ps; 
            
            //Algorithm:
            //1. Triangulate and remove points that do not have an edge to the other polygon.
            //2. Create a voronoi diagram of the remaining points
            //3. Loop
            // a. If the voronoi edge does not intersect either polygon it is part of the border
            // b. If the edge does intersect we create a new line from any point connected to the border and the intersection of the Delaunay edge
            //    and the voronoi edge
            // c. ??? 

            ConvexHulls[i] = ConvexHullExtension.ConvexHull(ps.Points.ToArray(), out original_indicies);
            TriangleNet.Meshing.IMesh mesh = TriangulatePolygons(ConvexHulls);

            List<GridLineSegment> LinesBetweenShapes = SelectLinesBetweenShapes(mesh, ConvexHulls);

            if (mesh == null)
                DelaunayView.UpdateViews(new GridVector2[0]);
            else
                DelaunayView.UpdateViews(mesh.ToLines());

            Voronoi = MeshingExperimentExtensions.ConvexHullVoronoi(ConvexHulls);

            List<GridLineSegment> listVoronoiLines = StripNonBoundaryLines(Voronoi);
            VoronoiView.UpdateViews(listVoronoiLines);

            //DetermineBoundary
            List<GridLineSegment> listBoundaryLines = BoundaryFinder.DetermineBoundary(LinesBetweenShapes, Voronoi, ConvexHulls.Where(cv => cv.Length > 3).Select(cv => new GridPolygon(cv)).ToArray());
            BoundaryView.UpdateViews(listBoundaryLines);
        }

        

        /// <summary>
        /// This function creates the triangulation of a set of polygons returning the set of edges between polygons and the external polygon borders.
        /// </summary>
        /// <param name="PointSets"></param>
        /// <returns></returns>
        private TriangleNet.Meshing.IMesh TriangulatePolygons(List<GridVector2[]> PointSets)
        {
            GridVector2[] AllPoints = PointSets.SelectMany(ps => ps.EnsureOpenRing()).ToArray();

            if (AllPoints.Length < 3)
                return null; 

            int[] original_indicies;
            GridVector2[] EntireSetConvexHull = AllPoints.ConvexHull(out original_indicies);
            
            TriangleNet.Geometry.Polygon poly = TriangleExtensions.CreatePolygon(EntireSetConvexHull);

            foreach(GridVector2[] points in PointSets)
            {
                if (points == null || points.Length < 4)
                    continue; 

                poly.AppendCountour(points); 
            }

            if (poly.Count < 3)
                return null; 

            TriangleNet.Meshing.IMesh mesh = TriangleNet.Geometry.ExtensionMethods.Triangulate(poly);
            return mesh; 
        }
        
        private List<LabelView> LabelDistances()
        {
            List<LabelView> labels = new List<LabelView>();
            for (int i = 0; i < ConvexHulls.Count; i++)
            {
                if (ConvexHulls[i] == null || ConvexHulls[i].Length <= 3)
                    continue;

                GridPolygon iPoly = new GridPolygon(ConvexHulls[i]);

                for (int j = i + 1; j < ConvexHulls.Count; j++)
                {
                    if (ConvexHulls[j] == null || ConvexHulls[j].Length <= 3)
                        continue;

                    GridPolygon jPoly = new GridPolygon(ConvexHulls[j]);

                    double minDistance = iPoly.Distance(jPoly);

                    LabelView newLabel = new LabelView(minDistance.ToString(), (iPoly.Centroid + jPoly.Centroid) / 2.0);
                    newLabel.FontSize /= 4.0;

                    labels.Add(newLabel);
                }
            }

            return labels;
        }

        /// <summary>
        /// Given a triangulation, remove all lines that are not between verticies from seperate shapes
        /// </summary>
        /// <param name="mesh"></param>
        /// <param name="PolygonRings"></param>
        /// <returns></returns>
        private List<GridLineSegment> SelectLinesBetweenShapes(TriangleNet.Meshing.IMesh mesh, List<GridVector2[]> PolygonRings)
        {
            if (mesh == null)
                return null;

            List<GridLineSegment> lines = mesh.ToLines();
            if (lines == null)
                return null;

            if (lines.Count == 0)
                return new List<GridLineSegment>();

            //Create an index map of points

            Dictionary<GridVector2, int> PointToShapeIndex = CreatePointToShapeIndexLookup(PolygonRings);
            
            for(int i = lines.Count-1; i >= 0; i--)
            {
                GridLineSegment line = lines[i];
                if (!(PointToShapeIndex.ContainsKey(line.A) && PointToShapeIndex.ContainsKey(line.B)))
                    continue;

                int HullA = PointToShapeIndex[line.A];
                int HullB = PointToShapeIndex[line.B];
                if (HullA == HullB)
                    lines.RemoveAt(i);
            }

            return lines; 
        }

        private Dictionary<GridVector2, int> CreatePointToShapeIndexLookup(List<GridVector2[]> shapeVerticies)
        {
            Dictionary<GridVector2, int> PointToShapeIndex = new Dictionary<GridVector2, int>();
            //Create an index map of points
            List<GridVector2> listPoints = new List<GridVector2>();
            List<int> listIndicies = new List<int>();

            for (int iShape = 0; iShape < shapeVerticies.Count; iShape++)
            {
                GridVector2[] points = shapeVerticies[iShape];
                if (points == null || points.Length == 0)
                    continue;

                points = shapeVerticies[iShape].EnsureOpenRing();

                foreach (GridVector2 point in points)
                {
                    PointToShapeIndex[point] = iShape;
                }
            }

            return PointToShapeIndex;
        }

        /// <summary>
        /// Remove edges of the voronoi graph that do not divide verticies from different shapes.
        /// </summary>
        /// <param name="voronoi"></param>
        /// <returns></returns>
        private List<GridLineSegment> StripNonBoundaryLines(TriangleNet.Voronoi.VoronoiBase voronoi)
        {
            if (voronoi == null)
                return null;

            //Build a set of LineSegments
            List<GridLineSegment> lines = new List<GridLineSegment>();

            Dictionary<GridVector2, int> PointToShapeIndex = CreatePointToShapeIndexLookup(this.ConvexHulls);

            foreach(TriangleNet.Topology.DCEL.HalfEdge halfEdge in voronoi.HalfEdges)
            {
                GridVector2 FaceA = new GridVector2(halfEdge.Face.generator.X,
                                                    halfEdge.Face.generator.Y);
                GridVector2 FaceB = new GridVector2(halfEdge.Twin.Face.generator.X,
                                                    halfEdge.Twin.Face.generator.Y);

                if (!(PointToShapeIndex.ContainsKey(FaceA) && PointToShapeIndex.ContainsKey(FaceB)))
                    continue;

                if(PointToShapeIndex[FaceA] != PointToShapeIndex[FaceB])
                {
                    lines.Add(new GridLineSegment(halfEdge.Origin.ToGridVector2(),
                                                  halfEdge.Twin.Origin.ToGridVector2()));
                }
            }

            return lines;
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (DelaunayView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, DelaunayView.LineViews.ToArray());

            if (VoronoiView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, VoronoiView.LineViews.ToArray());

            if (BoundaryView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, BoundaryView.LineViews.ToArray());

            if (listLabels != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, scene, listLabels);
        }
    }



    class TriangleAlgorithmTest : IGraphicsTest
    {
        Scene scene;
        PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);
        PointSetViewCollection Points_C = new PointSetViewCollection(Color.Red, Color.Pink, Color.GreenYellow);

        PolygonBorderView PolyBorderView = new PolygonBorderView();

        GamePadStateTracker Gamepad = new GamePadStateTracker();

        GridVector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel; 

        public double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));

            PolyBorderView.AddSet(Points_A.Points);
            PolyBorderView.AddSet(Points_B.Points);
            PolyBorderView.AddSet(Points_C.Points);
            PolyBorderView.Color = Color.Yellow;
            PolyBorderView.DelaunayView.color = Color.Gray;
            PolyBorderView.BoundaryView.color = Color.Yellow;
            PolyBorderView.VoronoiView.color = Color.DarkRed;
        }

        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            //StandardCameraManipulator.Update(this.Scene.Camera);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToGridVector2();
                cursorView = new CircleView(new GridCircle(Cursor, PointRadius), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor);
                cursorLabel.FontSize = 2;
                cursorLabel.Color = Color.Yellow;
            }

            if (state.ThumbSticks.Right != Vector2.Zero)
            {
                scene.Camera.LookAt += state.ThumbSticks.Right;
            }

            if(state.Triggers.Left > 0)
            {
                scene.Camera.Downsample *= 1.0 - (state.Triggers.Left / 10);

                if(scene.Camera.Downsample <= 0.1)
                {
                    scene.Camera.Downsample = 0.1;
                }
            }

            if (state.Triggers.Right > 0)
            {
                scene.Camera.Downsample *= 1.0 + (state.Triggers.Right / 10);

                if (scene.Camera.Downsample >= 100)
                {
                    scene.Camera.Downsample = 100;
                }
            }

            if(Gamepad.RightStick_Clicked)
            {
                scene.Camera.Downsample = 1;
                scene.Camera.LookAt = Vector2.Zero;
            }

            if (Gamepad.A_Clicked)
            {
                Points_A.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_A.Points, 0);
            }

            if (Gamepad.B_Clicked)
            {
                Points_B.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_B.Points, 1);
            }

            if (Gamepad.Y_Clicked)
            {
                Points_C.TogglePoint(Cursor);
                PolyBorderView.UpdateSet(Points_C.Points, 2);
            }
        }

        public void Draw(MonoTestbed window)
        {
            if(cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, window.basicEffect, window.overlayEffect, new CircleView[] { cursorView });
             
            PolyBorderView.Draw(window, scene);

            Points_A.Draw(window, scene);
            Points_B.Draw(window, scene);
            Points_C.Draw(window, scene);

            if(cursorLabel != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, window.Scene, new LabelView[] { cursorLabel });
        }

        /*
        private DynamicRenderMesh<int> ToMesh(TriangleNet.Topology.DCEL.DcelMesh mesh)
        { 
            DynamicRenderMesh<int> DRMesh = new DynamicRenderMesh<int>();

            //Create a map of Vertex ID's to DRMesh ID's
            int[] IndexMap = mesh.Vertices.Select(v => v.ID).ToArray();

            DRMesh.AddVertex(mesh.Vertices.Select(v => new Vertex<int>(new GridVector3(v.X, v.Y, 0), GridVector3.Zero, v.ID)).ToArray());

            foreach(TriangleNet.Topology.DCEL.Face f in mesh.Faces)
            {
                if (!f.Bounded)
                    continue;

                List<int> faceIDs = new List<int>(4);
                foreach(var edge in f.EnumerateEdges())
                {
                    faceIDs.Add(edge.Origin.ID);
                    System.Diagnostics.Debug.Assert(faceIDs.Count <= 4);
                }

                Face newFace = new Face(faceIDs);
                DRMesh.AddFace(newFace);
            }

            return DRMesh;
        }
        */

        
    }
}
