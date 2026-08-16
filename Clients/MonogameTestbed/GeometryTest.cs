using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TriangleNet;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MonogameTestbed
{
    class GeometryTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;

        readonly List<IShape2D> shapes = [];
        readonly LineView lineView;
        readonly CircleView circleView;

        List<IColorView> ShapeViews = [];
        List<IColorView> GroundTruth = [];

        int iSelectedView = 0;

        bool ShowGroundTruth = false;

        GamePadState LastGamepadState;

        bool _initialized = false;
        public bool Initialized => _initialized;

        public void InitGeometry()
        {
            LineSegment lineSegment;
            Triangle triangle;
            Circle circle;
            Polygon polygon;

            lineSegment = new LineSegment(new Geometry.Vector2(0, 0), new Geometry.Vector2(5, 5));
            circle = new Circle(new Geometry.Vector2(-10, -10), 4);

            polygon = StandardGeometryModels.CreateTestPolygon(true);

            triangle = new Triangle(new Geometry.Vector2(-10, 10),
                                                new Geometry.Vector2(-12, 20),
                                                 new Geometry.Vector2(-15, 10)
                                                );



            shapes.Add(lineSegment);
            shapes.Add(circle);
            shapes.Add(triangle);
            shapes.Add(polygon);
        }

        public Task Init(MonoTestbed window)
        {
            _initialized = true;
            InitGeometry();

            ShapeViews = CreateViewsForGeometries(shapes);
            GroundTruth = CreateViewsForGeometries(shapes);

            LastGamepadState = GamePad.GetState(PlayerIndex.One);
            return Task.CompletedTask;
        }
        public void UnloadContent(MonoTestbed window)
        {
        }

        public static List<IColorView> CreateViewsForGeometries(ICollection<IShape2D> shapes)
        {
            List<IColorView> Views = [];

            foreach (IShape2D shape in shapes)
            {
                IColorView view = null;
                if (shape is LineSegment lineSegment)
                {
                    view = new LineView(lineSegment.A, lineSegment.B, 1, Color.Red, LineStyle.Standard);
                }
                else if (shape is Circle circle)
                {
                    view = new CircleView(circle, Color.Red);
                }
                else if (shape is Triangle triangle)
                {
                    view = TriangleNetExtensions.CreateMeshForPolygon2D(triangle.Points, null, Color.Red);
                }
                else
                {
                    view = shape is Polygon polygon
                        ? (IColorView)TriangleNetExtensions.CreateMeshForPolygon2D(polygon, Color.Red)
                        : throw new ArgumentException("Unexpected shape type");
                }

                Views.Add(view);
            }

            return Views;
        }

        public void Update()
        {
            foreach (IColorView colorView in ShapeViews)
            {
                colorView.Color = Color.Green;
            }

            ShapeViews[iSelectedView].Color = Color.Blue;

            for (int i = 0; i < shapes.Count; i++)
            {
                for (int j = i + 1; j < shapes.Count; j++)
                {
                    if (shapes[i].Intersects(shapes[j]))
                    {
                        //Change the color of the intersecting shapes
                        ShapeViews[i].Color = Color.Red;
                        ShapeViews[j].Color = Color.Red;
                    }
                }
            }

            ProcessGamePad();

            GroundTruth = CreateViewsForGeometries(shapes);
            foreach (IColorView colorView in GroundTruth)
            {
                colorView.Color = Color.Gray;
            }
        }

        private void ProcessGamePad()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);

            if (state.Buttons.A == ButtonState.Pressed && state.Buttons.A != LastGamepadState.Buttons.A)
            {
                ShowGroundTruth = !ShowGroundTruth;
            }

            if (state.Buttons.LeftShoulder == ButtonState.Pressed &&
                state.Buttons.LeftShoulder != LastGamepadState.Buttons.LeftShoulder)
            {
                DecrementSelectedView();
            }

            if (state.Buttons.RightShoulder == ButtonState.Pressed &&
                state.Buttons.RightShoulder != LastGamepadState.Buttons.RightShoulder)
            {
                IncrementSelectedView();
            }

            if (state.ThumbSticks.Left.X != 0 || state.ThumbSticks.Left.Y != 0)
            {
                IColorView shapeView = ShapeViews[iSelectedView];
                IShape2D shape = shapes[iSelectedView];
                shapes[iSelectedView] = shape.Translate(state.ThumbSticks.Left.ToVector2());
                if (shapeView is IViewPosition2D)
                {
                    IViewPosition2D view = shapeView as IViewPosition2D;
                    view.Position += state.ThumbSticks.Left.ToVector2();
                }
                else if (shapeView is IViewPosition3D)
                {
                    IViewPosition3D view = shapeView as IViewPosition3D;
                    view.Position += state.ThumbSticks.Left.ToVector3();
                }
            }

            LastGamepadState = state;
        }

        private void IncrementSelectedView()
        {
            iSelectedView++;
            if (iSelectedView >= ShapeViews.Count)
            {
                iSelectedView = 0;
            }
        }

        private void DecrementSelectedView()
        {
            iSelectedView--;
            if (iSelectedView < 0)
            {
                iSelectedView = ShapeViews.Count - 1;
            }
        }

        public void Draw(MonoTestbed window)
        {
            window.GraphicsDevice.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil | ClearOptions.Target, Color.DarkGray, float.MaxValue, 0);

            //Draw where we know the geometries are and where the views say the geometries are.  These should match or one
            //of the translation routines has a bug

            if (ShowGroundTruth)
                DrawViews(window, GroundTruth);
            else
                DrawViews(window, ShapeViews);



            /*
        LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, new LineView[] { lineView });
        CircleView.Draw(window.GraphicsDevice, window.Scene, window.basicEffect, window.overlayEffect, new CircleView[] { circleView });
        //MeshView<VertexPositionColor>.Draw(window.GraphicsDevice, window.Scene, new MeshView<VertexPositionColor>[] { triView, polyView });
        polyView.Draw(window.GraphicsDevice, window.Scene);
        */
        }

        /*
        private void DrawCentroidsAndIndicies(MonoTestbed window)
        {
            foreach (IShape2D shape in shapes)
            {
                if (shape is Polygon poly)
                {
                    long FirstIndex = MorphologyMesh.SmoothMeshGenerator.FirstIndex(poly.ExteriorRing, out Geometry.Vector2 convexHullCentroid);

                    CircleView firstIndexView = new CircleView(new Circle(poly.ExteriorRing[FirstIndex], Math.Sqrt(poly.Area) / 10), Color.Black);
                    CircleView centroidView = new CircleView(new Circle(poly.Centroid, Math.Sqrt(poly.Area) / 20), Color.Yellow);
                    CircleView.Draw(window.GraphicsDevice, window.Scene, OverlayStyle.Alpha, new CircleView[] { firstIndexView, centroidView });
                }
            }
        }
        */

        public static void DrawViews(MonoTestbed window, ICollection<IColorView> listViews)
        {
            //DrawCentroidsAndIndicies(window);

            MeshView<VertexPositionColor> meshView = new();
            foreach (IColorView view in listViews)
            {
                if (view is LineView)
                {
                    LineView.Draw(window.GraphicsDevice, window.Scene, window.lineManager, [view as LineView]);
                }
                else if (view is CircleView)
                {
                    CircleView.Draw(window.GraphicsDevice, window.Scene, OverlayStyle.Alpha, new CircleView[] { view as CircleView });
                }
                else if (view is PositionColorMeshModel)
                {
                    PositionColorMeshModel modelView = view as PositionColorMeshModel;
                    meshView.models.Add(modelView);
                    //MeshView<VertexPositionColor>.Draw(window.GraphicsDevice, window.Scene, new MeshModel<VertexPositionColor>[] { view as PositionColorMeshModel });
                }
            }

            meshView.Draw(window.GraphicsDevice, window.Scene, CullMode.None);
        }
    }
}
