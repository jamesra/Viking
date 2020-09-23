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
using System.Collections.Specialized;


namespace MonogameTestbed
{

    class BranchPortView
    {
        public List<PointSet> Sets = new List<PointSet>();
        public List<GridPolygon> Shapes = new List<GridPolygon>();

        private  PointSet _BranchPoints = null;
        public PointSet BranchPoints
        {
            get { return _BranchPoints; }
            set
            {
                if(_BranchPoints != null)
                {
                    _BranchPoints.CollectionChanged -= this.OnBranchShapeChanged;
                }

                _BranchPoints = value;

                if(_BranchPoints != null)
                {
                    _BranchPoints.CollectionChanged += this.OnBranchShapeChanged;
                }
            }
        }

        public GridPolygon BranchShape = null;
        public LineSetView BranchShapeView = new LineSetView();
        public LineSetView ScaledBranchShapeView = new LineSetView();

        public List<LineSetView> PolygonViews = new List<LineSetView>();

        public BranchPortView()
        {
            BranchShapeView.color = Color.White;
            ScaledBranchShapeView.color = Color.Gray;
        }


        public int AddSet(PointSet set)
        {
            Sets.Add(set);
            Shapes.Add(null);
            LineSetView newView = new MonogameTestbed.LineSetView();
            newView.color = new Color().Random();
            PolygonViews.Add(newView);

            set.CollectionChanged += this.OnSetChanged;

            UpdateSet(set, Sets.Count - 1);
            return Sets.Count - 1;
        }

        public void OnBranchShapeChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PointSet updatedSet = sender as PointSet; 
            if(updatedSet.Count >= 3)
            {
                BranchShape = new GridPolygon(updatedSet.Points.EnsureClosedRing().ToArray());
            }
            else
            {
                BranchShape = null; 
            }

            BranchShapeView.UpdateViews(BranchShape);
            CalculateBranchPorts();
        }

        public void OnSetChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PointSet updatedSet = sender as PointSet;
            int index = Sets.IndexOf(updatedSet);
            UpdateSet(updatedSet, index);
            CalculateBranchPorts();
        }

        public void UpdateSet(PointSet ps, int i)
        {
            Sets[i] = ps;

            //Algorithm:
            //1. Triangulate and remove points that do not have an edge to the other polygon.
            //2. Create a voronoi diagram of the remaining points
            //3. Loop
            // a. If the voronoi edge does not intersect either polygon it is part of the border
            // b. If the edge does intersect we create a new line from any point connected to the border and the intersection of the Delaunay edge
            //    and the voronoi edge
            // c. ??? 

            if (ps.Points.Count >= 3)
            {
                Shapes[i] = new GridPolygon(ps.Points.EnsureClosedRing().ToArray());//ConvexHullExtension.ConvexHull(ps.Points.ToArray(), out original_indicies);
            }
            else
            {
                Shapes[i] = null;
            }

            PolygonViews[i].UpdateViews(Shapes[i]); 
        }

        public void CalculateBranchPorts()
        {
            if (BranchShape == null)
                return;

            GridPolygon[] UseableShapes = Shapes.Where(s => s != null).ToArray();

            if (UseableShapes.Count() == 0)
                return;
             
            GridRectangle BranchPortBoundingRect = BranchShape.BoundingBox;

            GridVector2[] shapePoints = UseableShapes.SelectMany(s => s.ExteriorRing.EnsureOpenRing()).ToArray();
            GridRectangle shapeBoundingBox = shapePoints.BoundingBox();

            GridPolygon convex_hull = new GridPolygon(shapePoints.ConvexHull());
            GridVector2 translate_vector = convex_hull.BoundingBox.Center - BranchShape.BoundingBox.Center;

            GridPolygon ScaledBranchPort = BranchShape.Translate(translate_vector);

            double maxDistance = double.MinValue;
             
            GridVector2 furthest_point = new GridVector2();
            foreach (GridVector2 p in convex_hull.ExteriorRing.EnsureOpenRing())
            { 
                double distance = GridVector2.Distance(ScaledBranchPort.Centroid, p);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    furthest_point = p;
                }
            }

            GridLineSegment lineToFurthestPoint = new GridLineSegment(ScaledBranchPort.Centroid, furthest_point);
            GridVector2 IntersectionOnLine = new GridVector2();
            double maxDistanceToIntersection = double.MinValue;
            foreach (GridLineSegment line in ScaledBranchPort.ExteriorSegments)
            {
                GridVector2 Intersection;
                if(line.Intersects(lineToFurthestPoint, out Intersection))
                {
                    double distance = GridVector2.Distance(Intersection, ScaledBranchPort.Centroid);
                    if(distance > maxDistanceToIntersection)
                    {
                        IntersectionOnLine = Intersection;
                        maxDistanceToIntersection = distance; 
                    }
                }
            }
                         
            double DistanceToCentroid = GridVector2.Distance(IntersectionOnLine, ScaledBranchPort.Centroid);
            double DistanceToPoint = maxDistance;

            /*
            GridLineSegment furthest_line = new GridLineSegment();
            GridVector2 furthest_point = new GridVector2();
            foreach(GridVector2 p in convex_hull.ExteriorRing.EnsureOpenRing())
            {
                GridLineSegment line;
                double distance = ScaledBranchPort.Distance(p, out line);
                if(distance > maxDistance)
                {
                    maxDistance = distance;
                    furthest_line = line;
                    furthest_point = p;
                }
            }

            GridVector2 IntersectionOnLine; 
            furthest_line.DistanceToPoint(furthest_point, out IntersectionOnLine);

            GridLineSegment lineToIntersection = new GridLineSegment(furthest_point, IntersectionOnLine);

            double DistanceToCentroid = GridVector2.Distance(IntersectionOnLine, ScaledBranchPort.Centroid);
            double DistanceToPoint = GridVector2.Distance(furthest_point, ScaledBranchPort.Centroid);
            */

            double WidthScalar = DistanceToPoint / DistanceToCentroid;
            double HeightScalar = DistanceToPoint / DistanceToCentroid;

            ScaledBranchPort = ScaledBranchPort.Scale(new GridVector2(WidthScalar, HeightScalar));
               
            ScaledBranchShapeView.UpdateViews(ScaledBranchPort); 
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (BranchShapeView != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, BranchShapeView.LineViews.ToArray());
            }

            if (ScaledBranchShapeView != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, ScaledBranchShapeView.LineViews.ToArray());
            }

            if (PolygonViews != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, PolygonViews.SelectMany(pv => pv.LineViews).ToArray());
            }
        }
    }
    
    class BranchPointTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        Scene scene;

        List<PointSet> PointSets = new List<PointSet>();
        List<PointSetView> PointSetViews = new List<PointSetView>();

        BranchPortView PortView = new BranchPortView();

        GamePadStateTracker Gamepad = new GamePadStateTracker();

        GridVector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel;

        static double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            //Create four point sets
            PointSets.Add(new PointSet());
            PointSets.Add(new PointSet());
            PointSets.Add(new PointSet());
            PointSets.Add(new PointSet());

            PortView.BranchPoints = PointSets[0];
            PortView.AddSet(PointSets[1]);
            PortView.AddSet(PointSets[2]);
            PortView.AddSet(PointSets[3]);

            
            foreach(PointSet set in PointSets)
            {
                PointSetView view = new PointSetView();
                view.Points = set;
                view.Color = new Color().Random();
                PointSetViews.Add(view);
            }
            
            Gamepad.Update(GamePad.GetState(PlayerIndex.One));

            UpdateCursorViews(Cursor);
        }
        public void UnloadContent(MonoTestbed window)
        {
        }

        private void UpdateCursorViews(GridVector2 position)
        {
            cursorView = new CircleView(new GridCircle(position, PointRadius), Color.Gray);
            cursorLabel = new LabelView(position.ToLabel(), Cursor);
            cursorLabel.FontSize = 2;
            cursorLabel.Color = Color.Yellow;
        }

        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            //StandardCameraManipulator.Update(this.Scene.Camera);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToGridVector2();
                UpdateCursorViews(Cursor);
            }

            if (state.ThumbSticks.Right != Vector2.Zero)
            {
                scene.Camera.LookAt += state.ThumbSticks.Right;
            }

            if (state.Triggers.Left > 0)
            {
                scene.Camera.Downsample *= 1.0 - (state.Triggers.Left / 10);

                if (scene.Camera.Downsample <= 0.1)
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

            if (Gamepad.RightStick_Clicked)
            {
                scene.Camera.Downsample = 1;
                scene.Camera.LookAt = Vector2.Zero;
            }

            if (Gamepad.A_Clicked)
            {
                PointSets[0].Toggle(Cursor);
                //Points_A.Toggle(Cursor);
            }

            if (Gamepad.B_Clicked)
            {
                PointSets[1].Toggle(Cursor);
                //Points_B.Toggle(Cursor);
            }

            if (Gamepad.Y_Clicked)
            {
                PointSets[2].Toggle(Cursor);
                //Points_C.Toggle(Cursor);
            }

            if (Gamepad.X_Clicked)
            {
                PointSets[3].Toggle(Cursor);
                //Points_D.Toggle(Cursor);
            }
        }

        public void Draw(MonoTestbed window)
        {
            PortView.Draw(window, scene);

            foreach(var view in PointSetViews)
            {
                view.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha);
            }

            if (cursorView != null)
                CircleView.Draw(window.GraphicsDevice, this.scene, OverlayStyle.Alpha, new CircleView[] { cursorView });

            if (cursorLabel != null)
                LabelView.Draw(window.spriteBatch, window.fontArial, this.scene, new LabelView[] { cursorLabel });
        }
    }
}
