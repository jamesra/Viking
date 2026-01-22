using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VikingXNA;
using VikingXNAGraphics;

namespace MonogameTestbed
{
    class CurveSimplificationTest : IGraphicsTest
    {
        public string Title => this.GetType().Name;
        Scene scene;

        bool _initialized = false;
        public bool Initialized => _initialized;

        readonly GamePadStateTracker Gamepad = new();
        readonly Cursor2DCameraManipulator CameraManipulator = new();

        GridVector2 Cursor;
        CircleView cursorView;
        LabelView cursorLabel;
        readonly PolyLineView RawPolyLine = new(Color.Black.SetAlpha(0.25f));
        readonly PolyLineView RawInflectionPolyLine = new(Color.Gray.SetAlpha(0.25f));
        readonly PolyLineView CurvedRawInflectionPolyLine = new(Color.Gold);
        readonly PolyLineView CurvedPolyLine = new(Color.Green.SetAlpha(0.25f));
        readonly PolyLineView CurvedSimplifiedPolyLine = new(Color.Blue);
        readonly PolyLineView CurvedInflectionsPolyLine = new(Color.Orange);
        readonly PolyLineView CurvedSimplifiedWithInflectionsPolyLine = new(Color.Red);
        readonly PolyLineView CurvedRecreationFromInflectionsPolyLine = new(Color.BlueViolet);
        readonly PolyLineView MinimalCatmullFitPolyLine = new(Color.Yellow.SetAlpha(0.5f));
        readonly PolyLineView MinimalCatmullFitCurvedPolyLine = new(Color.Red.SetAlpha(0.5f));

        public double PointRadius = 2.0;

        public List<GridVector2> path = [];

        bool IsClosed = false;

        public double PointIntervalOnDrag => scene.Camera.Downsample * 16.0;

        public void Draw(MonoTestbed window)
        {
            PolyLineView.Draw(window.GraphicsDevice,
                scene, OverlayStyle.Alpha,
                [
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
                ]);

            CircleView.Draw(window.GraphicsDevice, scene, OverlayStyle.Alpha, new CircleView[] { cursorView });
            LabelView.Draw(window.spriteBatch, window.fontArial, scene, new LabelView[] { cursorLabel });
        }

        public Task Init(MonoTestbed window)
        {
            _initialized = true;

            this.scene = new Scene(window.GraphicsDevice.Viewport, window.Camera);

            Gamepad.Update(GamePad.GetState(PlayerIndex.One));
            return Task.CompletedTask;
        }

        public void UnloadContent(MonoTestbed window)
        {
        }

        public void UpdateViews()
        {
            if (path is null || path.Count == 0)
            {
                RawPolyLine.ControlPoints = null;
            }
            else
            {
                RawPolyLine.ControlPoints = this.path;

                int[] raw_inflectionPoints = path.InflectionPointIndicies();

                GridVector2[] inflectionPath = [.. raw_inflectionPoints.Select(i => path[i])];

                GridVector2[] curveFit = Geometry.CatmullRom.FitCurve(this.path, 5, IsClosed);

                RawInflectionPolyLine.ControlPoints = inflectionPath;
                CurvedRawInflectionPolyLine.ControlPoints = Geometry.CatmullRom.FitCurve(inflectionPath, 5, IsClosed);

                CurvedPolyLine.ShowControlPoints = false;
                CurvedPolyLine.ControlPoints = curveFit;
                CurvedSimplifiedPolyLine.ControlPoints = curveFit.DouglasPeuckerReduction(15);

                int[] inflectionIndicies = curveFit.InflectionPointIndicies();//curveFit.MeasureCurvature2().ApplyKernel(new double[] { 0.15, 0.70, 0.15 }).InflectionPointIndicies(); //curveFit.InflectionPointIndicies();
                GridVector2[] inflectionPoints = [.. inflectionIndicies.Select(i => curveFit[i])];
                CurvedInflectionsPolyLine.ControlPoints = inflectionPoints;

                //CurvedSimplifiedWithInflectionsPolyLine.ControlPoints = inflectionPoints.DouglasPeuckerReduction(2);
                //CurvedRecreationFromInflectionsPolyLine.ControlPoints = Geometry.CatmullRom.FitCurve(CurvedSimplifiedWithInflectionsPolyLine.ControlPoints, 5, false);

                MinimalCatmullFitPolyLine.ControlPoints = CatmullRomControlPointSimplification.IdentifyControlPoints(this.path, 1.0, IsClosed);
                MinimalCatmullFitCurvedPolyLine.ControlPoints = Geometry.CatmullRom.FitCurve([.. MinimalCatmullFitPolyLine.ControlPoints], 5, IsClosed);


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
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = cursorView.Radius / 2.0,
                    Color = Color.Yellow
                };
            }

            if (state.Buttons.LeftShoulder == ButtonState.Pressed)
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
                cursorLabel = new LabelView(Cursor.ToLabel(), Cursor)
                {
                    FontSize = cursorView.Radius / 2.0,
                    Color = Color.Yellow
                };

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
            if (path.Count == 0)
            {
                path.Add(p);
                return true;
            }

            double distance = GridVector2.Distance(path[^1], p);
            if (distance >= this.PointIntervalOnDrag)
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

            double distance = GridVector2.Distance(path[^1], p);
            if (distance <= this.PointIntervalOnDrag)
            {
                path.RemoveAt(path.Count - 1);
                return true;
            }

            return false;
        }
    }
}
