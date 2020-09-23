using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using TriangleNet;
using VikingXNA;
using VikingXNAGraphics;
using System.Collections.Generic;
using System.Linq;

namespace MonogameTestbed
{
    class CurveSimplificationTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        Scene scene;

        bool _initialized = false;
        public bool Initialized { get { return _initialized; } }

        GamePadStateTracker Gamepad = new GamePadStateTracker();
        Cursor2DCameraManipulator CameraManipulator = new Cursor2DCameraManipulator();

        GridVector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel;

        PolyLineView RawPolyLine = new PolyLineView(Color.Black.SetAlpha(0.25f));
        PolyLineView RawInflectionPolyLine = new PolyLineView(Color.Gray.SetAlpha(0.25f));
        PolyLineView CurvedRawInflectionPolyLine = new PolyLineView(Color.Gold);
        PolyLineView CurvedPolyLine = new PolyLineView(Color.Green.SetAlpha(0.25f));
        PolyLineView CurvedSimplifiedPolyLine = new PolyLineView(Color.Blue);
        PolyLineView CurvedInflectionsPolyLine = new PolyLineView(Color.Orange);
        PolyLineView CurvedSimplifiedWithInflectionsPolyLine = new PolyLineView(Color.Red);
        PolyLineView CurvedRecreationFromInflectionsPolyLine = new PolyLineView(Color.BlueViolet);
        PolyLineView MinimalCatmullFitPolyLine = new PolyLineView(Color.Yellow.SetAlpha(0.5f));
        PolyLineView MinimalCatmullFitCurvedPolyLine = new PolyLineView(Color.Red.SetAlpha(0.5f));

        public double PointRadius = 2.0;

        public List<GridVector2> path = new List<GridVector2>();

        bool IsClosed = false;

        public double PointIntervalOnDrag
        {
            get
            {
                return scene.Camera.Downsample * 16.0;
            }
        }
        
        public void Draw(MonoTestbed window)
        {
            PolyLineView.Draw(window.GraphicsDevice,
                scene, OverlayStyle.Alpha,
                new PolyLineView[] {
                    RawPolyLine,
                    //RawInflectionPolyLine,
                    //CurvedRawInflectionPolyLine, 
                    CurvedPolyLine,
                    //CurvedInflectionsPolyLine,
                    //CurvedSimplifiedPolyLine, //Doesn't work, too much reduction
                    //CurvedSimplifiedWithInflectionsPolyLine,
                    //CurvedRecreationFromInflectionsPolyLine,
                    MinimalCatmullFitPolyLine,
                    MinimalCatmullFitCurvedPolyLine
                });

            CircleView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha, new CircleView[] { cursorView });
            LabelView.Draw(window.spriteBatch, window.fontArial, scene, new LabelView[] { cursorLabel });
        }

        public void Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);
            
            Gamepad.Update(GamePad.GetState(PlayerIndex.One));
        }

        public void UnloadContent(MonoTestbed window)
        {
        }

        public void UpdateViews()
        {
            if (path == null || path.Count == 0)
            {
                RawPolyLine.ControlPoints = null;
            }
            else
            {
                RawPolyLine.ControlPoints = this.path;

                int[] raw_inflectionPoints = path.InflectionPointIndicies();

                GridVector2[] inflectionPath = raw_inflectionPoints.Select(i => path[i]).ToArray();

                GridVector2[] curveFit = Geometry.CatmullRom.FitCurve(this.path, 5, IsClosed);

                RawInflectionPolyLine.ControlPoints = inflectionPath;
                CurvedRawInflectionPolyLine.ControlPoints = Geometry.CatmullRom.FitCurve(inflectionPath, 5, IsClosed);

                CurvedPolyLine.ShowControlPoints = false;
                CurvedPolyLine.ControlPoints = curveFit;
                CurvedSimplifiedPolyLine.ControlPoints = curveFit.DouglasPeuckerReduction(15);

                int[] inflectionIndicies = curveFit.InflectionPointIndicies();//curveFit.MeasureCurvature2().ApplyKernel(new double[] { 0.15, 0.70, 0.15 }).InflectionPointIndicies(); //curveFit.InflectionPointIndicies();
                GridVector2[] inflectionPoints = inflectionIndicies.Select(i => curveFit[i]).ToArray();
                CurvedInflectionsPolyLine.ControlPoints = inflectionPoints;

                //CurvedSimplifiedWithInflectionsPolyLine.ControlPoints = inflectionPoints.DouglasPeuckerReduction(2);
                //CurvedRecreationFromInflectionsPolyLine.ControlPoints = Geometry.CatmullRom.FitCurve(CurvedSimplifiedWithInflectionsPolyLine.ControlPoints, 5, false);

                MinimalCatmullFitPolyLine.ControlPoints = CatmullRomControlPointSimplification.IdentifyControlPoints(this.path, 1.0, IsClosed);
                MinimalCatmullFitCurvedPolyLine.ControlPoints = Geometry.CatmullRom.FitCurve(MinimalCatmullFitPolyLine.ControlPoints.ToArray(), 5, IsClosed);

               
            }
        }



        double lastDownsample = 0;
        public void Update()
        {
            GamePadState state = GamePad.GetState(PlayerIndex.One);
            Gamepad.Update(state);

            CameraManipulator.Update(scene.Camera);

            if (state.ThumbSticks.Left != Vector2.Zero)
            {
                Cursor += state.ThumbSticks.Left.ToGridVector2();
                cursorView = new CircleView(new GridCircle(Cursor, scene.Camera.Downsample < 1 ? 1.0 : scene.Camera.Downsample), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor);
                cursorLabel.FontSize = cursorView.Radius / 2.0;
                cursorLabel.Color = Color.Yellow;
            } 

            if(state.Buttons.LeftShoulder == ButtonState.Pressed)
            {
                if (TryAddPathPoint(Cursor))
                {
                    UpdateViews();
                }
            }

            if (Gamepad.RightShoulder_Clicked)
            {
                if (path.Count > 0)
                { 
                    path.RemoveAt(path.Count - 1);
                    UpdateViews();
                }
            }

            if (Gamepad.B_Clicked)
            {
                this.IsClosed = !this.IsClosed;
                UpdateViews();
            }

            if (Gamepad.Y_Clicked)
            {
                UpdateViews();
            }

            if (lastDownsample != scene.Camera.Downsample)
            {
                cursorView = new CircleView(new GridCircle(Cursor, scene.Camera.Downsample < 1 ? 1.0 : scene.Camera.Downsample), Color.Gray);
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor);
                cursorLabel.FontSize = cursorView.Radius / 2.0;
                cursorLabel.Color = Color.Yellow;

                double LineWidth = scene.Camera.Downsample < 1 ? 1.0 : scene.Camera.Downsample;
                RawPolyLine.LineWidth = LineWidth / 2.0;
                RawInflectionPolyLine.LineWidth = LineWidth / 2.0;
                CurvedRawInflectionPolyLine.LineWidth = LineWidth / 2.0;
                CurvedPolyLine.LineWidth = LineWidth / 2.0;
                CurvedSimplifiedPolyLine.LineWidth = LineWidth / 2.0;
                CurvedInflectionsPolyLine.LineWidth = LineWidth / 2.0;
                CurvedSimplifiedWithInflectionsPolyLine.LineWidth = LineWidth / 2.0;
                CurvedRecreationFromInflectionsPolyLine.LineWidth = LineWidth / 2.0;
                MinimalCatmullFitPolyLine.LineWidth = LineWidth / 2.0;
                MinimalCatmullFitCurvedPolyLine.LineWidth = LineWidth / 2.0;

                MinimalCatmullFitPolyLine.ControlPointRadius = LineWidth / 4.0;
                MinimalCatmullFitCurvedPolyLine.ControlPointRadius = LineWidth / 4.0;

                lastDownsample = scene.Camera.Downsample;
            }
        }

        public bool TryAddPathPoint(GridVector2 p)
        {
            if(path.Count == 0)
            {
                path.Add(p);
                return true;
            }

            double distance = GridVector2.Distance(path[path.Count - 1], p);
            if(distance >= this.PointIntervalOnDrag)
            {
                path.Add(p);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove a point from the path if it is close enough to the passed point
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public bool TryRemovePathPoint(GridVector2 p)
        {
            if (path.Count == 0)
            {  
                return false;
            }

            double distance = GridVector2.Distance(path[path.Count - 1], p);
            if (distance <= this.PointIntervalOnDrag)
            {
                path.RemoveAt(path.Count - 1);
                return true;
            }

            return false;
        }
    }
}
