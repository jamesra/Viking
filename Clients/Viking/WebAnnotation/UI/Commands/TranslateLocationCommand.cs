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
using WebAnnotationModel.Objects;

namespace WebAnnotation.UI.Commands
{
    internal class TranslateClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Vector2 VolumePosition,
                                    Vector2[] OriginalMosaicControlPoints,
                                    Microsoft.Xna.Framework.Color color,
                                    double LineWidth,
TranslateCurveLocationCommand.OnCommandSuccess success_callback) : TranslateCurveLocationCommand(parent, VolumePosition, OriginalMosaicControlPoints, color, LineWidth, success_callback)
    {
        protected override double SizeScale { get; set; } = 1.0;

        public override double AnnotationRadius => LineWidth;

        protected double LineWidth => curveView.ControlPoints.ToPolygon().CalculateInscribedCircle(curveView.ControlPoints).Radius;


        protected override void OnAngleChanged() => curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);

        protected override void OnSizeScaleChanged() => curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);

        protected override void OnTranslationChanged() => curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);

        protected override double CalculateFinalLineWidth() => Global.DefaultClosedLineWidth;

        protected override CurveView CreateView(Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color) => new CurveView([.. ControlPoints], color, true, numInterpolations: Global.NumClosedCurveInterpolationPoints, lineWidth: OriginalVolumeControlPoints.MinDistanceBetweenAnyPoints() * SizeScale, controlPointRadius: Global.DefaultClosedLineWidth / 2.0, lineStyle: LineStyle.HalfTube);

        protected override Vector2[] CalculateTranslatedMosaicControlPoints()
        {
            //Vector2 centroid = OriginalVolumeControlPoints.Centroid();
            ICollection<Vector2> rotatedPoints = OriginalVolumeControlPoints.Rotate(Angle, VolumeRotationOrigin);
            ICollection<Vector2> scaledPoints = rotatedPoints.Scale(SizeScale, VolumeRotationOrigin);
            ICollection<Vector2> translatedPoints = scaledPoints.Translate(VolumePositionDeltaSum);
            return [.. translatedPoints];
        }

        protected override void Execute()
        {
            if (success_callback != null)
            {
                Vector2[] TranslatedOriginalControlPoints = CalculateTranslatedMosaicControlPoints();
                Vector2[] MosaicControlPoints = null;

                try
                {
                    MosaicControlPoints = mapping.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedVolumePosition.ToString(), "Command");
                    return;
                }

                Circle circle = TranslatedOriginalControlPoints.ToPolygon().CalculateInscribedCircle(TranslatedOriginalControlPoints);
                success_callback(TranslatedOriginalControlPoints, MosaicControlPoints, circle.Radius * 2);
            }

            base.Execute();
        }

    }

    internal class TranslateOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Vector2 VolumePosition,
                                    Vector2[] OriginalMosaicControlPoints,
                                    Microsoft.Xna.Framework.Color color,
                                    double LineWidth,
TranslateCurveLocationCommand.OnCommandSuccess success_callback) : TranslateCurveLocationCommand(parent, VolumePosition, OriginalMosaicControlPoints, color, LineWidth, success_callback), Viking.Common.IHelpStrings
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
            curveView = CreateView(CalculateTranslatedMosaicControlPoints(),
                                          curveView.Color);
        }

        protected override void OnSizeScaleChanged()
        {
            curveView = CreateView(CalculateTranslatedMosaicControlPoints(),
                                          curveView.Color);
        }

        protected override double CalculateFinalLineWidth() => OriginalLineWidth * _lineWidthScale;

        public override string[] HelpStrings
        {
            get
            {
                List<string> s = [.. base.HelpStrings];
                s.AddRange(TranslateOpenCurveCommand.DefaultMouseHelpStrings);
                s.Sort();
                return [.. s];
            }
        }

        public new static string[] DefaultMouseHelpStrings =
        [
            "Mouse Wheel + SHIFT: Change line width",
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

        protected override CurveView CreateView(Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color)
        {
            double lineWidth = CalculateFinalLineWidth();
            return new CurveView([.. ControlPoints], color, false, Global.NumOpenCurveInterpolationPoints, lineWidth: lineWidth, lineStyle: LineStyle.Tubular, controlPointRadius: lineWidth / 2.0);
        }

        protected override Vector2[] CalculateTranslatedMosaicControlPoints()
        {
            Vector2 centroid = OriginalVolumeControlPoints.Average();
            ICollection<Vector2> rotatedPoints = OriginalVolumeControlPoints.Rotate(Angle, centroid);
            ICollection<Vector2> scaledPoints = rotatedPoints.Scale(SizeScale, centroid);
            ICollection<Vector2> translatedPoints = scaledPoints.Translate(VolumePositionDeltaSum);
            return [.. translatedPoints];
        }


        protected override void Execute()
        {
            if (success_callback != null)
            {
                Vector2[] TranslatedOriginalControlPoints = CalculateTranslatedMosaicControlPoints();
                Vector2[] MosaicControlPoints = null;

                try
                {
                    MosaicControlPoints = mapping.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedVolumePosition.ToString(), "Command");
                    return;
                }

                success_callback(TranslatedOriginalControlPoints, MosaicControlPoints, LineWidthScale * OriginalLineWidth);
            }

            base.Execute();
        }

    }

    internal abstract class TranslateCurveLocationCommand : RotateTranslateScaleCommand, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        protected CurveView curveView;
        protected Vector2[] OriginalVolumeControlPoints;
        protected double OriginalLineWidth;


        public delegate void OnCommandSuccess(Vector2[] VolumeControlPoints, Vector2[] MosaicControlPoints, double LineWidth);
        protected OnCommandSuccess success_callback;

        protected abstract Vector2[] CalculateTranslatedMosaicControlPoints();
        protected abstract double CalculateFinalLineWidth();

        string[] IHelpStrings.HelpStrings
        {
            get
            {
                List<string> s = [.. TranslateCurveLocationCommand.DefaultMouseHelpStrings];
                s.AddRange(RotateTranslateScaleCommand.DefaultMouseHelpStrings);
                s.AddRange(TranslateScaleCommandBase.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return [.. s];
            }
        }

        public new ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        public new static string[] DefaultMouseHelpStrings =
        [
            "CTRL+Click another curve: Copy control points",
            "Middle Button click: Reset to original size",
            "Hold Right click and drag: Rotate",
            "Mouse Wheel: Change annotation size",
            "SHIFT + Scroll wheel: Scale annotation size slowly"
        ];

        protected override Vector2 VolumeRotationOrigin => curveView.ControlPoints.Average();

        public TranslateCurveLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Vector2 VolumePosition,
                                        Vector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition)
        {
            //this.OriginalVolumePosition = mapping.SectionToVolume(MosaicPosition);
            OriginalLineWidth = LineWidth;
            OriginalVolumeControlPoints = mapping.SectionToVolume(OriginalMosaicControlPoints);
            curveView = CreateView(OriginalVolumeControlPoints, color);
            this.success_callback = success_callback;
        }

        protected abstract CurveView CreateView(Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color);

        protected override void OnTranslationChanged() => curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);

        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                Vector2 WorldPosition = oldWorldPosition;
                List<HitTestResult> listHitResults = Overlay.GetAnnotations(WorldPosition);
                List<HitTestResult> listCurves = [.. listHitResults.Where(h => h.Z == Parent.Section.Number && h.obj as LocationOpenCurveView != null)];

                if (listCurves.Count == 0)
                {
                    return;
                }

                listCurves.OrderBy(c => c.Distance);

                LocationOpenCurveView curveToCopy = listCurves.First().obj as LocationOpenCurveView;
                OriginalVolumeControlPoints = curveToCopy.VolumeControlPoints;
                Vector2 translatedPosition = TranslatedVolumePosition;
                OriginalVolumePosition = OriginalVolumeControlPoints.Average();
                VolumePositionDeltaSum = new Vector2(0, 0);
                CreateView(OriginalVolumeControlPoints, curveView.Color);
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

    internal class TranslateCircleLocationCommand : TranslateScaleCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        private CircleView circleView;
        private readonly Circle OriginalCircle;

        public override double AnnotationRadius => OriginalCircle.Radius;

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        public string[] HelpStrings
        {
            get
            {
                List<string> s = [.. TranslateScaleCommandBase.DefaultMouseHelpStrings];
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                return [.. s];
            }
        }

        public delegate void OnCommandSuccess(Vector2 VolumePosition, Vector2 MosaicPosition, double NewRadius);

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
            Vector2 annotation_start_position,
            Microsoft.Xna.Framework.Color color,
            OnCommandSuccess success_callback) : this(parent, volume_circle, color, success_callback)
        {
            ScaleOrigin = annotation_start_position;
        }

        private void CreateView(Vector2 Position, double Radius, Microsoft.Xna.Framework.Color color) => circleView = new CircleView(new Circle(Position, Radius * RadiusScale), color);

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
                success_callback(TranslatedVolumePosition, TranslatedMosaicPosition, circleView.Radius);
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
        public static void DefaultSuccessCallback(LocationObj loc, Vector2 WorldPosition, Vector2 MosaicPosition)
        {
            DefaultSuccessNoSaveCallback(loc, WorldPosition, MosaicPosition);
            _ = AnnotationOverlay.SaveLocationsWithMessageBoxOnError();
        }

        public static void DefaultSuccessNoSaveCallback(LocationObj loc, Vector2 WorldPosition, Vector2 MosaicPosition)
        {
            loc.MosaicShape = loc.MosaicShape.MoveTo(MosaicPosition);
            loc.VolumeShape = loc.VolumeShape.MoveTo(WorldPosition);
        }
    }

}