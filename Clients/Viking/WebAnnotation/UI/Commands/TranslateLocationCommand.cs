using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using Viking.Common;
using VikingXNAGraphics;
using VikingXNAWinForms;
using WebAnnotation.View;
using WebAnnotationModel;

namespace WebAnnotation.UI.Commands
{
    internal class TranslateClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Geometry.Vector2 VolumePosition,
                                    Geometry.Vector2[] StartVolumeControlPoints,
                                    Microsoft.Xna.Framework.Color color,
                                    double LineWidth,
TranslateCurveLocationCommand.OnCommandSuccess success_callback) : TranslateCurveLocationCommand(parent, VolumePosition, StartVolumeControlPoints, color, LineWidth, success_callback)
    {
        protected override double SizeScale { get; set; } = 1.0;

        public override double AnnotationRadius => LineWidth;

        protected double LineWidth => curveView.ControlPoints.ToPolygon().CalculateInscribedCircle().Radius;


        protected override void OnAngleChanged() => curveView = CreateView(CalculateTranslatedVolumeControlPoints(), curveView.Color);

        protected override void OnSizeScaleChanged() => curveView = CreateView(CalculateTranslatedVolumeControlPoints(), curveView.Color);

        protected override void OnTranslationChanged() => curveView = CreateView(CalculateTranslatedVolumeControlPoints(), curveView.Color);

        protected override double CalculateFinalLineWidth() => Global.DefaultClosedLineWidth;

        protected override CurveView CreateView(Geometry.Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color) => new CurveView([.. ControlPoints], color, true, numInterpolations: Global.NumClosedCurveInterpolationPoints, lineWidth: OriginalVolumeControlPoints.MinDistanceBetweenAnyPoints() * SizeScale, controlPointRadius: Global.DefaultClosedLineWidth / 2.0, lineStyle: LineStyle.HalfTube);

        protected override Geometry.Vector2[] CalculateTranslatedVolumeControlPoints()
        {
            //Geometry.Vector2 centroid = OriginalVolumeControlPoints.Centroid();
            ICollection<Geometry.Vector2> rotatedPoints = OriginalVolumeControlPoints.Rotate(Angle, VolumeRotationOrigin);
            ICollection<Geometry.Vector2> scaledPoints = rotatedPoints.Scale(SizeScale, VolumeRotationOrigin);
            ICollection<Geometry.Vector2> translatedPoints = scaledPoints.Translate(VolumePositionDeltaSum);
            return [.. translatedPoints];
        }

        protected override void Execute()
        {
            if (success_callback != null)
            {
                Geometry.Vector2[] TranslatedOriginalControlPoints = CalculateTranslatedVolumeControlPoints();
                Circle circle = TranslatedOriginalControlPoints.ToPolygon().CalculateInscribedCircle();
                success_callback(TranslatedOriginalControlPoints, circle.Radius * 2);
            }

            base.Execute();
        }

    }

    internal class TranslateOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Geometry.Vector2 VolumePosition,
                                    Geometry.Vector2[] StartVolumeControlPoints,
                                    Microsoft.Xna.Framework.Color color,
                                    double LineWidth,
TranslateCurveLocationCommand.OnCommandSuccess success_callback) : TranslateCurveLocationCommand(parent, VolumePosition, StartVolumeControlPoints, color, LineWidth, success_callback)
    {
        private double _lineWidthScale = 1.0;
        protected double LineWidthScale
        {
            get => _lineWidthScale;
            set
            {
                _lineWidthScale = value;
                _lineWidthScale = _lineWidthScale * OriginalLineWidth < 1.0 ? 1.0 / OriginalLineWidth : value;
                curveView.LineWidth = CalculateFinalLineWidth();
            }
        }

        public override double AnnotationRadius => OriginalLineWidth;

        protected override void OnAngleChanged()
        {
            curveView = CreateView(CalculateTranslatedVolumeControlPoints(),
                                          curveView.Color);
        }

        protected override void OnSizeScaleChanged()
        {
            curveView = CreateView(CalculateTranslatedVolumeControlPoints(),
                                          curveView.Color);
        }

        protected override double CalculateFinalLineWidth() => OriginalLineWidth * _lineWidthScale;

        /// <summary>Shift + wheel changes the line width here (<see cref="OnMouseWheel"/>), not the size.</summary>
        public override HelpEntry[] Help =>
        [
            .. base.Help.Where(h => !(h.Modifiers == HelpModifiers.Shift && h.Inputs is { Length: 1 } inputs && inputs[0] == HelpInput.Wheel)),
            HelpEntry.For(HelpInput.Wheel, "Change line width", HelpModifiers.Shift),
        ];

        private int scroll_wheel_delta = 0;
        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) ||
                System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift))
            {
                scroll_wheel_delta += e.Delta;
                LineWidthScale = GetScalarForScrollWheelDelta(scroll_wheel_delta);
                Parent.Invalidate();
            }
            else
            {
                base.OnMouseWheel(sender, e);
            }
        }

        protected override CurveView CreateView(Geometry.Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color)
        {
            double lineWidth = CalculateFinalLineWidth();
            return new CurveView([.. ControlPoints], color, false, Global.NumOpenCurveInterpolationPoints, lineWidth: lineWidth, lineStyle: LineStyle.Tubular, controlPointRadius: lineWidth / 2.0);
        }

        protected override Geometry.Vector2[] CalculateTranslatedVolumeControlPoints()
        {
            Geometry.Vector2 centroid = OriginalVolumeControlPoints.Average();
            ICollection<Geometry.Vector2> rotatedPoints = OriginalVolumeControlPoints.Rotate(Angle, centroid);
            ICollection<Geometry.Vector2> scaledPoints = rotatedPoints.Scale(SizeScale, centroid);
            ICollection<Geometry.Vector2> translatedPoints = scaledPoints.Translate(VolumePositionDeltaSum);
            return [.. translatedPoints];
        }


        protected override void Execute()
        {
            if (success_callback != null)
            {
                success_callback(CalculateTranslatedVolumeControlPoints(), LineWidthScale * OriginalLineWidth);
            }

            base.Execute();
        }

    }

    internal abstract class TranslateCurveLocationCommand : RotateTranslateScaleCommand
    {
        protected CurveView curveView;
        protected Geometry.Vector2[] OriginalVolumeControlPoints;
        protected double OriginalLineWidth;


        /// <summary>
        /// The control points and line width the user left on screen, in volume space. The callback maps them back to mosaic
        /// through the inverse owner (<c>LocationActions.SaveVolumeLineEdit</c>); the command never maps them itself.
        /// </summary>
        public delegate void OnCommandSuccess(Geometry.Vector2[] VolumeControlPoints, double LineWidth);
        protected OnCommandSuccess success_callback;

        protected abstract Geometry.Vector2[] CalculateTranslatedVolumeControlPoints();
        protected abstract double CalculateFinalLineWidth();

        /// <summary>Pressing Ctrl over another open curve copies its control points (<see cref="OnKeyDown"/>), then the move, rotate and resize actions.</summary>
        public override HelpEntry[] Help =>
        [
            new("Copy control points", HelpModifiers.Ctrl, Where: "over another curve"),
            .. base.Help,
        ];

        protected override Geometry.Vector2 VolumeRotationOrigin => curveView.ControlPoints.Average();

        /// <summary>
        /// Starts a move of a line or curve from the control points and width the forward owner draws
        /// (<c>LocationVolumeGeometry.ControlPoints</c>, <c>Width</c>), so the preview starts exactly where the location is drawn.
        /// </summary>
        /// <param name="OriginalVolumeControlPoints">The control points in volume space.</param>
        /// <param name="LineWidth">The drawn width, in volume units.</param>
        public TranslateCurveLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Geometry.Vector2 VolumePosition,
                                        Geometry.Vector2[] OriginalVolumeControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition)
        {
            OriginalLineWidth = LineWidth;
            this.OriginalVolumeControlPoints = OriginalVolumeControlPoints;
            curveView = CreateView(OriginalVolumeControlPoints, color);
            this.success_callback = success_callback;
        }

        protected abstract CurveView CreateView(Geometry.Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color);

        protected override void OnTranslationChanged() => curveView = CreateView(CalculateTranslatedVolumeControlPoints(), curveView.Color);

        /// <summary>
        /// Picks the hit nearest the query point, so Ctrl-copy takes the curve under the mouse instead of whichever the R-tree listed first.
        /// </summary>
        /// <param name="hits">Candidate hits; may be null, as <see cref="AnnotationOverlay.GetAnnotations(Geometry.Vector2)"/> returns null when the section has no annotations.</param>
        /// <returns>The hit with the smallest <see cref="HitTestResult.Distance"/> (the first listed wins a tie), or null when <paramref name="hits"/> is null or empty.</returns>
        internal static HitTestResult PickNearest(IEnumerable<HitTestResult> hits) => hits?.OrderBy(h => h.Distance).FirstOrDefault();

        /// <summary>
        /// Any key with Ctrl held (pressing Ctrl itself first) copies the control points of the open curve under the mouse; a
        /// modifier held over a target is not a key a <see cref="Viking.UI.KeyBinding"/> can match, so it stays outside
        /// <see cref="Viking.UI.Commands.Command.KeyTable"/>. Other keys go to the table.
        /// </summary>
        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                Geometry.Vector2 WorldPosition = oldWorldPosition;
                List<HitTestResult> listHitResults = Overlay.GetAnnotations(WorldPosition);
                HitTestResult nearest = PickNearest(listHitResults?.Where(h => h.Z == Parent.Section.Number && h.obj is LocationOpenCurveView));

                if (nearest?.obj is not LocationOpenCurveView curveToCopy)
                {
                    return;
                }

                OriginalVolumeControlPoints = curveToCopy.VolumeControlPoints;
                Geometry.Vector2 translatedPosition = TranslatedVolumePosition;
                OriginalVolumePosition = OriginalVolumeControlPoints.Average();
                VolumePositionDeltaSum = new Geometry.Vector2(0, 0);
                curveView = CreateView(CalculateTranslatedVolumeControlPoints(), curveView.Color);
            }
            else
            {
                base.OnKeyDown(sender, e);
            }
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect) => CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, [curveView]);
    }

    internal class TranslateCircleLocationCommand : TranslateScaleCommandBase, IHelp, IObservableHelp
    {
        private CircleView circleView;
        private readonly Circle OriginalCircle;

        public override double AnnotationRadius => OriginalCircle.Radius;

        public ObservableCollection<HelpEntry> ObservableHelp => new(Help);

        /// <summary>The shared move-and-resize actions, middle click to reset the size (<see cref="OnMouseDown"/>), then the keys.</summary>
        public HelpEntry[] Help =>
        [
            .. TranslateScaleHelp,
            HelpEntry.For(HelpInput.MiddleClick, "Reset to original size"),
            .. KeyHelp,
        ];

        /// <summary>
        /// The circle the user left on screen, in volume space. The callback saves it through the inverse owner
        /// (<c>LocationActions.UpdateCircleLocationCallback</c>), which maps it back to the mosaic circle.
        /// </summary>
        public delegate void OnCommandSuccess(Geometry.Vector2 VolumePosition, double NewRadius);

        private readonly OnCommandSuccess success_callback;

        protected double RadiusScale => base.SizeScale * OriginalCircle.Radius < 1.0f ? 1 / OriginalCircle.Radius : base.SizeScale;

        protected override void OnSizeScaleChanged() => CreateView(TranslatedVolumePosition, OriginalCircle.Radius * RadiusScale, circleView.Color);

        public TranslateCircleLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Circle volume_circle,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent, volume_circle.Center)
        {
            OriginalCircle = new Circle(OriginalVolumePosition, volume_circle.Radius);
            CreateView(OriginalVolumePosition, volume_circle.Radius, color);
            this.success_callback = success_callback;
        }

        public TranslateCircleLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
            Circle volume_circle,
            Geometry.Vector2 annotation_start_position,
            Microsoft.Xna.Framework.Color color,
            OnCommandSuccess success_callback) : this(parent, volume_circle, color, success_callback)
        {
            ScaleOrigin = annotation_start_position;
        }

        private void CreateView(Geometry.Vector2 Position, double Radius, Microsoft.Xna.Framework.Color color) => circleView = new CircleView(new Circle(Position, Radius * RadiusScale), color);

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Reset size scale if the middle mouse button is pushed
            if (e.Button.Middle())
            {
                SizeScale = 1.0;
                return;
            }
            else
            {
                base.OnMouseDown(sender, e);
            }
        }

        protected override void Execute()
        {
            if (success_callback != null)
            {
                success_callback(TranslatedVolumePosition, circleView.Radius);
            }

            base.Execute();
        }

        protected override void OnTranslationChanged() => UpdateView();


        protected void UpdateView() => circleView.Circle = new Circle(TranslatedVolumePosition, circleView.Radius);

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect) =>
            //TODO: Translate the LocationCanvasView before it is drawn
            CircleView.Draw(graphicsDevice, scene, OverlayStyle.Luma, new CircleView[] { circleView });//LocationObjRenderer.DrawBackgrounds(items, graphicsDevice, basicEffect, Parent.annotationOverlayEffect, Parent.LumaOverlayLineManager, scene, Parent.Section.Number);
    }

}