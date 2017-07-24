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
        public Color color;
      
        public void UpdateViews(ICollection<GridCircle> Points)
        {
            PointViews = Points.Select(c => new CircleView(c, color)).ToArray();
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
            if(VoronoiView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, VoronoiView.LineViews.ToArray());

            if(PointsView.PointViews != null)
                CircleView.Draw(window.GraphicsDevice, scene, window.basicEffect, window.overlayEffect, PointsView.PointViews);

            if (CVView.LineViews != null)
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, CVView.LineViews.ToArray());
        }
    }


    class DividerView
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
            set { BoundaryView.color = value; }
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
            Voronoi = ConvexHullVoronoi();

            List<GridLineSegment> listBoundaryLines = StripNonBoundaryLines(Voronoi);
            VoronoiView.UpdateViews(Voronoi.ToLines());
            VoronoiView.color = Color.Gray;
            BoundaryView.UpdateViews(listBoundaryLines);
            listLabels = LabelDistances();
        }

        private TriangleNet.Voronoi.VoronoiBase ConvexHullVoronoi()
        {
            List<TriangleNet.Geometry.Vertex> verts = new List<TriangleNet.Geometry.Vertex>();
            List<int> IndexMap = new List<int>();

            for(int i = 0; i < ConvexHulls.Count; i++)
            {
                GridVector2[] set = ConvexHulls[i];
                verts.AddRange(set.Select(p =>
                    {
                        var v = new TriangleNet.Geometry.Vertex(p.X, p.Y, i, 1);
                        v.Attributes[0] = i;
                        return v;
                    }));

                IndexMap.AddRange(set.Select(p => i)); 
            }

            if (verts.Count >= 4)
            {
                var Voronoi = verts.Voronoi();
                for(int i = 0; i < IndexMap.Count; i++)
                {
                    Voronoi.Vertices[i].Label = IndexMap[i];
                }
                
                return Voronoi;
            }

            return null;
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



    class TriangleAlgorithmTest : IGraphicsTest
    {
        Scene scene;
        PointSetViewCollection Points_A = new PointSetViewCollection(Color.Blue, Color.BlueViolet, Color.PowderBlue);
        PointSetViewCollection Points_B = new PointSetViewCollection(Color.Red, Color.Pink, Color.Plum);

        DividerView DividerView = new DividerView();

        GamePadStateTracker Gamepad = new GamePadStateTracker();

        GridVector2 Cursor;
        CircleView cursorView;

        public double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));

            DividerView.AddSet(Points_A.Points);
            DividerView.AddSet(Points_B.Points);
            DividerView.Color = Color.Yellow;
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
                DividerView.UpdateSet(Points_A.Points, 0);
            }

            if (Gamepad.B_Clicked)
            {
                Points_B.TogglePoint(Cursor);
                DividerView.UpdateSet(Points_B.Points, 1);
            }
        }

        public void Draw(MonoTestbed window)
        {
            if(cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, window.basicEffect, window.overlayEffect, new CircleView[] { cursorView });

            Points_A.Draw(window, scene);
            Points_B.Draw(window, scene);

            DividerView.Draw(window, scene);
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
