using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using Geometry;
using WebAnnotation.View;
using Viking.VolumeModel;
using SqlGeometryUtils;
using VikingXNAGraphics;
using System.Windows.Forms;
using System.Diagnostics;
using WebAnnotation;
using System.Collections.ObjectModel;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    class TranslateClosedCurveCommand : TranslateCurveLocationCommand
    {
        private double _sizeScale = 1.0;
        protected override double SizeScale
        {
            get
            {
                return _sizeScale;
            }
            set
            {
                _sizeScale = value; 
            }
        }

        protected double LineWidth
        {
            get
            {
                return curveView.ControlPoints.ToPolygon().CalculateInscribedCircle(curveView.ControlPoints).Radius;
            }
        }


        protected override void OnAngleChanged()
        {
            curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);
        }

        protected override void OnSizeScaleChanged()
        {
            curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color); 
        }

        protected override void OnTranslationChanged()
        {
            curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);
        }
         
        protected override double CalculateFinalLineWidth()
        {
            return Global.DefaultClosedLineWidth;
        }

        public TranslateClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2 VolumePosition,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition, OriginalMosaicControlPoints, color, LineWidth, success_callback)
        { }
         

        protected override CurveView CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color)
        {
            return new CurveView(ControlPoints.ToList(), color, true, numInterpolations: Global.NumClosedCurveInterpolationPoints, lineWidth: this.OriginalVolumeControlPoints.MinDistanceBetweenPoints() * this.SizeScale, controlPointRadius: Global.DefaultClosedLineWidth / 2.0, lineStyle: LineStyle.HalfTube);
        }

        protected override GridVector2[] CalculateTranslatedMosaicControlPoints()
        {
            //GridVector2 centroid = OriginalVolumeControlPoints.Centroid();
            ICollection<GridVector2> rotatedPoints = OriginalVolumeControlPoints.Rotate(this.Angle, this.VolumeRotationOrigin);
            ICollection<GridVector2> scaledPoints = rotatedPoints.Scale(this.SizeScale, this.VolumeRotationOrigin);
            ICollection<GridVector2> translatedPoints = scaledPoints.Translate(this.VolumePositionDeltaSum);
            return translatedPoints.ToArray();
        } 

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2[] TranslatedOriginalControlPoints = CalculateTranslatedMosaicControlPoints();
                GridVector2[] MosaicControlPoints = null;

                try
                {
                    MosaicControlPoints = mapping.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedVolumePosition.ToString(), "Command");
                    return;
                }

                GridCircle circle = TranslatedOriginalControlPoints.ToPolygon().CalculateInscribedCircle(TranslatedOriginalControlPoints);
                this.success_callback(TranslatedOriginalControlPoints, MosaicControlPoints, circle.Radius * 2);
            }

            base.Execute();
        }

    }

    class TranslateOpenCurveCommand : TranslateCurveLocationCommand, Viking.Common.IHelpStrings
    {
        private double _lineWidthScale = 1.0;
        protected double LineWidthScale
        {
            get
            {
                return _lineWidthScale;
            }
            set
            {
                _lineWidthScale = value;
                _lineWidthScale = _lineWidthScale * OriginalLineWidth < 1.0 ? 1.0 / OriginalLineWidth : value;
                curveView.LineWidth = CalculateFinalLineWidth();
            }
        }
        
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

        protected override double CalculateFinalLineWidth()
        {
            return OriginalLineWidth * _lineWidthScale;
        }

        public override string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>(base.HelpStrings);
                s.AddRange(TranslateOpenCurveCommand.DefaultMouseHelpStrings);
                s.Sort();
                return s.ToArray();
            }
        }

        public new static string[] DefaultMouseHelpStrings = new string[]
        {
            "Mouse Wheel + SHIFT: Change line width",
        };

        public TranslateOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2 VolumePosition,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition, OriginalMosaicControlPoints, color, LineWidth, success_callback)
        { }

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

        protected override CurveView CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color)
        {
            return new CurveView(ControlPoints.ToList(), color, false, Global.NumOpenCurveInterpolationPoints, lineWidth: CalculateFinalLineWidth(), lineStyle: LineStyle.Tubular);
        }

        protected override GridVector2[] CalculateTranslatedMosaicControlPoints()
        {
            GridVector2 centroid = OriginalVolumeControlPoints.Average();
            ICollection<GridVector2> rotatedPoints = OriginalVolumeControlPoints.Rotate(this.Angle, centroid);
            ICollection<GridVector2> scaledPoints = rotatedPoints.Scale(this.SizeScale, centroid);
            ICollection<GridVector2> translatedPoints = scaledPoints.Translate(this.VolumePositionDeltaSum);
            return translatedPoints.ToArray();
        }


        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2[] TranslatedOriginalControlPoints = CalculateTranslatedMosaicControlPoints();
                GridVector2[] MosaicControlPoints = null;

                try
                {
                    MosaicControlPoints = mapping.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedVolumePosition.ToString(), "Command");
                    return;
                }

                this.success_callback(TranslatedOriginalControlPoints, MosaicControlPoints, this.LineWidthScale * this.OriginalLineWidth);
            }

            base.Execute();
        }

    }


    abstract class TranslateCurveLocationCommand : RotateTranslateScaleCommand, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        protected CurveView curveView;
        protected GridVector2[] OriginalVolumeControlPoints;
        protected double OriginalLineWidth;

       
        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints, double LineWidth);
        protected OnCommandSuccess success_callback;

        protected abstract GridVector2[] CalculateTranslatedMosaicControlPoints();
        protected abstract double CalculateFinalLineWidth();

        public virtual string[] HelpStrings
        {
            get
            { 
                List<string> s = new List<string>(TranslateCurveLocationCommand.DefaultMouseHelpStrings);
                s.AddRange(RotateTranslateScaleCommand.DefaultMouseHelpStrings);
                s.AddRange(TranslateScaleCommandBase.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return s.ToArray();
            }
        }

        public new ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }

        public new static string[] DefaultMouseHelpStrings = new string[]
        {
            "CTRL+Click another curve: Copy control points",
            "Middle Button click: Reset to original size",
            "Hold Right click and drag: Rotate",
            "Mouse Wheel: Change annotation size",
            "SHIFT + Scroll wheel: Scale annotation size slowly"
        };
         
        protected override GridVector2 VolumeRotationOrigin
        {
            get
            {
                return curveView.ControlPoints.Average();
            }
        }

        public TranslateCurveLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2 VolumePosition, 
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth, 
                                        OnCommandSuccess success_callback) : base(parent, VolumePosition)
        {
            //this.OriginalVolumePosition = mapping.SectionToVolume(MosaicPosition);
            this.OriginalLineWidth = LineWidth;
            this.OriginalVolumeControlPoints = mapping.SectionToVolume(OriginalMosaicControlPoints);
            this.curveView = CreateView(OriginalVolumeControlPoints, color);
            this.success_callback = success_callback;
        }

        protected abstract CurveView CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color);

        protected override void OnTranslationChanged()
        { 
            this.curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);
        }
                 
        protected override void OnKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Control)
            {
                GridVector2 WorldPosition = this.oldWorldPosition;
                List<HitTestResult> listHitResults = Overlay.GetAnnotationsAtPosition(WorldPosition);
                List<HitTestResult> listCurves = listHitResults.Where(h => h.Z == Parent.Section.Number && h.obj as LocationOpenCurveView != null).ToList();

                if (listCurves.Count == 0)
                    return;

                listCurves.OrderBy(c => c.Distance);

                LocationOpenCurveView curveToCopy = listCurves.First().obj as LocationOpenCurveView;
                this.OriginalVolumeControlPoints = curveToCopy.VolumeControlPoints;
                GridVector2 translatedPosition = this.TranslatedVolumePosition;
                this.OriginalVolumePosition = OriginalVolumeControlPoints.Average();
                this.VolumePositionDeltaSum = new GridVector2(0, 0);
                CreateView(OriginalVolumeControlPoints, curveView.Color);
            }
            else
            {
                base.OnKeyDown(sender, e);
            }
        } 

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { this.curveView });
        }
    }

    class TranslateCircleLocationCommand : TranslateScaleCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        CircleView circleView;
        GridCircle OriginalCircle;

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }

        public  string[] HelpStrings
        {
            get
            { 
                List<string> s = new List<string>(TranslateScaleCommandBase.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                return s.ToArray();
            }
        }

        public delegate void OnCommandSuccess(GridVector2 VolumePosition, GridVector2 MosaicPosition, double NewRadius);
        OnCommandSuccess success_callback;
        
        protected double RadiusScale
        {
            get
            {
                return base.SizeScale * OriginalCircle.Radius < 1.0f ? 1 / OriginalCircle.Radius : base.SizeScale;
            }
        }

        protected override void OnSizeScaleChanged()
        {
            CreateView(this.TranslatedVolumePosition, OriginalCircle.Radius * this.RadiusScale, circleView.Color);
        }
         
        public TranslateCircleLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridCircle volume_circle,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent, volume_circle.Center)
        {
            OriginalCircle = new GridCircle(this.OriginalVolumePosition, volume_circle.Radius);
            CreateView(this.OriginalVolumePosition, volume_circle.Radius, color);
            this.success_callback = success_callback;
        }

        private void CreateView(GridVector2 Position, double Radius, Microsoft.Xna.Framework.Color color)
        {
            circleView = new CircleView(new GridCircle(Position, Radius * this.RadiusScale), color);
        }
        
        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Reset size scale if the middle mouse button is pushed
            if (e.Button.Middle())
            {
                this.SizeScale = 1.0;
                return;
            }
            else
            {
                base.OnMouseDown(sender, e);
            }
        } 

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                this.success_callback(this.TranslatedVolumePosition, this.TranslatedMosaicPosition, this.circleView.Radius);
            } 

            base.Execute();
        }

        protected override void OnTranslationChanged()
        {
            UpdateView();
        }

        protected void UpdateView()
        {
            circleView.Circle = new GridCircle(this.TranslatedVolumePosition, this.circleView.Radius);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            //TODO: Translate the LocationCanvasView before it is drawn
            CircleView.Draw(graphicsDevice, scene, basicEffect, Parent.AnnotationOverlayEffect, new CircleView[] { this.circleView });
            //LocationObjRenderer.DrawBackgrounds(items, graphicsDevice, basicEffect, Parent.annotationOverlayEffect, Parent.LumaOverlayLineManager, scene, Parent.Section.Number);            
        }
        public static void DefaultSuccessCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            DefaultSuccessNoSaveCallback(loc, WorldPosition, MosaicPosition);
            Store.Locations.Save();
        }

        public static void DefaultSuccessNoSaveCallback(LocationObj loc, GridVector2 WorldPosition, GridVector2 MosaicPosition)
        {
            loc.MosaicShape = loc.MosaicShape.MoveTo(MosaicPosition);
            loc.VolumeShape = loc.VolumeShape.MoveTo(WorldPosition);
        }
    }

}