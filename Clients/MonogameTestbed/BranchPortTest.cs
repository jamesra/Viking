using Geometry;
using Rectangle = Geometry.Rectangle;
using Geometry.Meshing;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;


namespace MonogameTestbed
{

    class BranchPortView
    {
        public List<PointSet> Sets = [];
        public List<Polygon> Shapes = [];

        private PointSet _BranchPoints = null;
        public PointSet BranchPoints
        {
            get => _BranchPoints;
            set
            {
                if (_BranchPoints != null)
                {
                    _BranchPoints.CollectionChanged -= this.OnBranchShapeChanged;
                }

                _BranchPoints = value;

                if (_BranchPoints != null)
                {
                    _BranchPoints.CollectionChanged += this.OnBranchShapeChanged;
                }
            }
        }

        public Polygon BranchShape = null;
        public LineSetView BranchShapeView = new();
        public LineSetView ScaledBranchShapeView = new();

        public List<LineSetView> PolygonViews = [];

        public BranchPortView()
        {
            BranchShapeView.color = Color.White;
            ScaledBranchShapeView.color = Color.Gray;
        }


        public int AddSet(PointSet set)
        {
            Sets.Add(set);
            Shapes.Add(null);
            LineSetView newView = new()
            {
                color = new Color().Random()
            };
            PolygonViews.Add(newView);

            set.CollectionChanged += this.OnSetChanged;

            UpdateSet(set, Sets.Count - 1);
            return Sets.Count - 1;
        }

        public void OnBranchShapeChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PointSet updatedSet = sender as PointSet;
            BranchShape = updatedSet.Count >= 3 ? new Polygon(updatedSet.Points.EnsureClosedRing().ToArray()) : null;

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
                Shapes[i] = new Polygon(ps.Points.EnsureClosedRing().ToArray());//ConvexHullExtension.ConvexHull(ps.Points.ToArray(), out originalIndices);
            }
            else
            {
                Shapes[i] = null;
            }

            PolygonViews[i].UpdateViews(Shapes[i]);
        }

        public void CalculateBranchPorts()
        {
            if (BranchShape is null)
                return;

            Polygon[] UseableShapes = [.. Shapes.Where(s => s != null)];

            if (!UseableShapes.Any())
                return;

            Rectangle BranchPortBoundingRect = BranchShape.BoundingBox;

            Geometry.Vector2[] shapePoints = [.. UseableShapes.SelectMany(s => s.ExteriorRing.EnsureOpenRing())];
            Rectangle shapeBoundingBox = shapePoints.BoundingBox();

            Polygon convex_hull = new(shapePoints.ConvexHull());
            Geometry.Vector2 translate_vector = convex_hull.BoundingBox.Center - BranchShape.BoundingBox.Center;

            Polygon ScaledBranchPort = BranchShape.Translate(translate_vector);

            double maxDistance = double.MinValue;

            Geometry.Vector2 furthest_point = new();
            foreach (Geometry.Vector2 p in convex_hull.ExteriorRing.EnsureOpenRing())
            {
                double distance = Geometry.Vector2.Distance(ScaledBranchPort.Centroid, p);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    furthest_point = p;
                }
            }

            LineSegment lineToFurthestPoint = new(ScaledBranchPort.Centroid, furthest_point);
            Geometry.Vector2 IntersectionOnLine = new();
            double maxDistanceToIntersection = double.MinValue;
            foreach (LineSegment line in ScaledBranchPort.ExteriorSegments)
            {
                if (line.Intersects(lineToFurthestPoint, out Geometry.Vector2 Intersection))
                {
                    double distance = Geometry.Vector2.Distance(Intersection, ScaledBranchPort.Centroid);
                    if (distance > maxDistanceToIntersection)
                    {
                        IntersectionOnLine = Intersection;
                        maxDistanceToIntersection = distance;
                    }
                }
            }

            double DistanceToCentroid = Geometry.Vector2.Distance(IntersectionOnLine, ScaledBranchPort.Centroid);
            double DistanceToPoint = maxDistance;

            /*
            LineSegment furthest_line = new LineSegment();
            Geometry.Vector2 furthest_point = new Geometry.Vector2();
            foreach(Geometry.Vector2 p in convex_hull.ExteriorRing.EnsureOpenRing())
            {
                LineSegment line;
                double distance = ScaledBranchPort.Distance(p, out line);
                if(distance > maxDistance)
                {
                    maxDistance = distance;
                    furthest_line = line;
                    furthest_point = p;
                }
            }

            Geometry.Vector2 IntersectionOnLine; 
            furthest_line.DistanceToPoint(furthest_point, out IntersectionOnLine);

            LineSegment lineToIntersection = new LineSegment(furthest_point, IntersectionOnLine);

            double DistanceToCentroid = Geometry.Vector2.Distance(IntersectionOnLine, ScaledBranchPort.Centroid);
            double DistanceToPoint = Geometry.Vector2.Distance(furthest_point, ScaledBranchPort.Centroid);
            */

            double WidthScalar = DistanceToPoint / DistanceToCentroid;
            double HeightScalar = DistanceToPoint / DistanceToCentroid;

            ScaledBranchPort = ScaledBranchPort.Scale(new Geometry.Vector2(WidthScalar, HeightScalar));

            ScaledBranchShapeView.UpdateViews(ScaledBranchPort);
        }

        public void Draw(MonoTestbed window, Scene scene)
        {
            if (BranchShapeView != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. BranchShapeView.LineViews]);
            }

            if (ScaledBranchShapeView != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. ScaledBranchShapeView.LineViews]);
            }

            if (PolygonViews != null)
            {
                LineView.Draw(window.GraphicsDevice, scene, window.lineManager, [.. PolygonViews.SelectMany(pv => pv.LineViews)]);
            }
        }
    }

    class BranchPointTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        readonly TestInputContext Input = new();
        Scene scene;
        readonly List<PointSet> PointSets = [];
        readonly List<PointSetView> PointSetViews = [];
        readonly BranchPortView PortView = new();
        Geometry.Vector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel;

        static readonly double PointRadius = 2.0;

        bool _initialized = false;
        public bool Initialized => _initialized;

        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            //Create four point sets
            PointSets.Add([]);
            PointSets.Add([]);
            PointSets.Add([]);
            PointSets.Add([]);

            PortView.BranchPoints = PointSets[0];
            PortView.AddSet(PointSets[1]);
            PortView.AddSet(PointSets[2]);
            PortView.AddSet(PointSets[3]);


            foreach (PointSet set in PointSets)
            {
                PointSetView view = new()
                {
                    Points = set,
                    Color = new Color().Random()
                };
                PointSetViews.Add(view);
            }

            Input.UpdateTrackers();

            UpdateCursorViews(Cursor);
            return Task.CompletedTask;
        }
        public void UnloadContent(MonoTestbed window)
        {
        }

        private void UpdateCursorViews(Geometry.Vector2 position)
        {
            cursorView = new CircleView(new Circle(position, PointRadius), Color.Gray);
            cursorLabel = new LabelView(position.ToLabel(), Cursor)
            {
                FontSize = 2,
                Color = Color.Yellow
            };
        }

        public void Update()
        {
            GamePadState state = Input.UpdateTrackers();

            //StandardCameraManipulator.Update(this.Scene.Camera);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToVector2();
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

            if (Input.Gamepad.RightStick_Clicked)
            {
                scene.Camera.Downsample = 1;
                scene.Camera.LookAt = Vector2.Zero;
            }

            if (Input.Gamepad.A_Clicked)
            {
                PointSets[0].Toggle(Cursor);
                //Points_A.Toggle(Cursor);
            }

            if (Input.Gamepad.B_Clicked)
            {
                PointSets[1].Toggle(Cursor);
                //Points_B.Toggle(Cursor);
            }

            if (Input.Gamepad.Y_Clicked)
            {
                PointSets[2].Toggle(Cursor);
                //Points_C.Toggle(Cursor);
            }

            if (Input.Gamepad.X_Clicked)
            {
                PointSets[3].Toggle(Cursor);
                //Points_D.Toggle(Cursor);
            }
        }

        public void Draw(MonoTestbed window)
        {
            PortView.Draw(window, scene);

            foreach (var view in PointSetViews)
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
