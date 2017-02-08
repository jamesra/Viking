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

        protected override void UpdateScale(double input)
        {
            SizeScale = input;
            curveView = CreateView(CalculateTranslatedMosaicControlPoints(),
                                          curveView.Color); 
        }
        

        protected override double CalculateFinalLineWidth()
        {
            return Global.DefaultClosedLineWidth;
        }

        public TranslateClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2 MosaicPosition,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback) : base(parent, MosaicPosition, OriginalMosaicControlPoints, color, LineWidth, success_callback)
        { }
         

        protected override CurveView CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color)
        {
            return new CurveView(ControlPoints.ToList(), color, true, lineWidth: this.OriginalControlPoints.MinDistanceBetweenPoints() * this.SizeScale, controlPointRadius:  Global.DefaultClosedLineWidth / 2.0,  lineStyle: LineStyle.HalfTube, numInterpolations: Global.NumClosedCurveInterpolationPoints);
        }

        protected override GridVector2[] CalculateTranslatedMosaicControlPoints()
        {
            GridVector2 centroid = OriginalControlPoints.Centroid();
            ICollection<GridVector2> rotatedPoints = OriginalControlPoints.Rotate(this.Angle, centroid);
            ICollection<GridVector2> scaledPoints = rotatedPoints.Scale(this.SizeScale, centroid);
            ICollection<GridVector2> translatedPoints = scaledPoints.Translate(this.DeltaSum);
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
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedPosition.ToString(), "Command");
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
            }
        }

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

        protected override void UpdateScale(double input)
        {
            SizeScale = input;
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
                                        GridVector2 MosaicPosition,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        OnCommandSuccess success_callback) : base(parent, MosaicPosition, OriginalMosaicControlPoints, color, LineWidth, success_callback)
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
            return new CurveView(ControlPoints.ToList(), color, false, lineWidth: CalculateFinalLineWidth(), lineStyle: LineStyle.Tubular);
        }

        protected override GridVector2[] CalculateTranslatedMosaicControlPoints()
        {
            GridVector2 centroid = OriginalControlPoints.Centroid();
            ICollection<GridVector2> rotatedPoints = OriginalControlPoints.Rotate(this.Angle, centroid);
            ICollection<GridVector2> scaledPoints = rotatedPoints.Scale(this.SizeScale, centroid);
            ICollection<GridVector2> translatedPoints = scaledPoints.Translate(this.DeltaSum);
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
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedPosition.ToString(), "Command");
                    return;
                }

                this.success_callback(TranslatedOriginalControlPoints, MosaicControlPoints, this.LineWidthScale * this.OriginalLineWidth);
            }

            base.Execute();
        }

    }


    abstract class TranslateCurveLocationCommand : TranslateLocationCommand, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        protected CurveView curveView;
        protected GridVector2[] OriginalControlPoints;
        protected GridVector2 OriginalPosition;
        protected GridVector2 DeltaSum = new GridVector2(0, 0);
        protected double OriginalLineWidth;

       
        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints, double LineWidth);
        protected OnCommandSuccess success_callback;

        protected Viking.VolumeModel.IVolumeToSectionTransform mapping;
         
        private double _Angle = 0;

        private double _sizeScale = 1.0;

        protected double Angle
        {
            get { return _Angle; }
            set {
                _Angle = value;
                this.curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);
            }
        }
            

        protected override GridVector2 TranslatedPosition
        {
            get
            {
                return OriginalPosition + (DeltaSum);
            }
        }

        public virtual string[] HelpStrings
        {
            get
            { 
                List<string> s = new List<string>(TranslateCurveLocationCommand.DefaultMouseHelpStrings);
                s.AddRange(TranslateLocationCommand.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                s.Sort();
                return s.ToArray();
            }
        }

        public ObservableCollection<string> ObservableHelpStrings
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

        public TranslateCurveLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2 MosaicPosition, 
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth, 
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalPosition = mapping.SectionToVolume(MosaicPosition);
            this.OriginalLineWidth = LineWidth;
            this.OriginalControlPoints = mapping.SectionToVolume(OriginalMosaicControlPoints);
            this.curveView = CreateView(OriginalControlPoints, color);
            this.success_callback = success_callback;
        }

        protected abstract CurveView CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color);

        protected override void UpdateViewPosition(GridVector2 PositionDelta)
        {
            DeltaSum += PositionDelta;
            this.curveView = CreateView(CalculateTranslatedMosaicControlPoints(), curveView.Color);
        }

        protected double _RotationOrigin;
         
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
                this.OriginalControlPoints = curveToCopy.VolumeControlPoints;
                GridVector2 translatedPosition = this.TranslatedPosition;
                this.OriginalPosition = OriginalControlPoints.Centroid();
                this.DeltaSum = new GridVector2(0, 0);
                CreateView(OriginalControlPoints, curveView.Color);
            }
            else
            {
                base.OnKeyDown(sender, e);
            }
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            //Reset size scale if the middle mouse button is pushed
            if (e.Button.Middle())
            {
                this.SizeScale = 1.0;
                return;
            }
            else if(e.Button.Right())
            {
                GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);
                GridVector2 Center = this.TranslatedPosition;
                this._RotationOrigin = GridVector2.Angle(Center, WorldPosition);
            }
            else
            {
                base.OnMouseDown(sender, e);
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        { 
            if (e.Button.Right())
            {
                GridVector2 worldPosition = Parent.ScreenToWorld(e.X, e.Y);
                GridVector2 origin = this.TranslatedPosition;
                GridVector2 centroid = this.OriginalControlPoints.Centroid();

                if (origin == worldPosition)
                    return;


                //double AngleToCommandStart = GridVector2.Angle(centroid, origin);
                this.Angle = GridVector2.Angle(origin, worldPosition) - _RotationOrigin;

                //Save as old mouse position so location doesn't jump when we release the right mouse button
                SaveAsOldMousePosition(e);
            }
            else
            {
                base.OnMouseMove(sender, e);
            }
        }
        
        protected abstract GridVector2[] CalculateTranslatedMosaicControlPoints();

        protected abstract double CalculateFinalLineWidth();


        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.annotationOverlayEffect, 0, new CurveView[] { this.curveView });
        }
    }

    class TranslateCircleLocationCommand : TranslateLocationCommand, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
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
                List<string> s = new List<string>(TranslateLocationCommand.DefaultMouseHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                return s.ToArray();
            }
        }

        public delegate void OnCommandSuccess(GridVector2 VolumePosition, GridVector2 MosaicPosition, double NewRadius);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        private double _sizeScale = 1.0;
        protected override double SizeScale
        {
            get
            {
                return _sizeScale;
            }
            set
            {
                _sizeScale = value * OriginalCircle.Radius < 1.0f ? 1 / OriginalCircle.Radius : value;                
            }
        }

        protected override GridVector2 TranslatedPosition
        {
            get
            {
                return circleView.VolumePosition;
            }
        }

        public TranslateCircleLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridCircle mosaic_circle,
                                        Microsoft.Xna.Framework.Color color,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            GridVector2 volumePosition = mapping.SectionToVolume(mosaic_circle.Center);
            CreateView(volumePosition, mosaic_circle.Radius, color);
            OriginalCircle = new GridCircle(volumePosition, mosaic_circle.Radius);
            this.success_callback = success_callback;
        }

        private void CreateView(GridVector2 Position, double Radius, Microsoft.Xna.Framework.Color color)
        {
            circleView = new CircleView(new GridCircle(Position, Radius * this._sizeScale), color);
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
                GridVector2 MosaicPosition;

                bool mappedToMosaic = mapping.TryVolumeToSection(TranslatedPosition, out MosaicPosition);
                if(!mappedToMosaic)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedPosition.ToString(), "Command");
                    return;
                }

                this.success_callback(this.TranslatedPosition, MosaicPosition, this.circleView.Radius);
            } 

            base.Execute();
        }

        protected override void UpdateScale(double input)
        {
            this.SizeScale = input;
            CreateView(circleView.VolumePosition, OriginalCircle.Radius * _sizeScale, circleView.Color);
        }

        protected override void UpdateViewPosition(GridVector2 PositionDelta)
        {
            circleView.Circle = new GridCircle(circleView.Circle.Center + PositionDelta, this.circleView.Radius);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            //TODO: Translate the LocationCanvasView before it is drawn
            CircleView.Draw(graphicsDevice, scene, basicEffect, Parent.annotationOverlayEffect, new CircleView[] { this.circleView });
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

    abstract class TranslateLocationCommand : AnnotationCommandBase
    {
        public new static string[] DefaultMouseHelpStrings = new String[] {
           "Hold Left+Click Drag to move",
           "Release Left button to place",
           "Scroll wheel: Scale annotation size",
           "SHIFT + Scroll wheel: Scale annotation size slowly"
        };
         

        protected virtual double SizeScale
        {
            get;
            set;
        }
        
        /// <summary>
        /// Translated position in volume space
        /// </summary>
        protected abstract GridVector2 TranslatedPosition
        {
             get;
        }

        public TranslateLocationCommand(Viking.UI.Controls.SectionViewerControl parent) : base(parent)
        {
        }

        protected abstract void UpdateScale(double input);

        protected abstract void UpdateViewPosition(GridVector2 PositionDelta);
        
        public override void OnDeactivate()
        {
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        protected double GetScalarForScrollWheelDelta(int scroll_delta_sum)
        {
            if (Math.Abs(scroll_delta_sum) < 120)
                return 1.0;

            int adjusted_scroll_distance = Math.Abs(scroll_delta_sum) - 120;

            //OK, so lets figure out how far we need to scrool 
            const double Scroll_distance_to_double_size = 900.0;

            double num_doublings = (double)adjusted_scroll_distance / (double)Scroll_distance_to_double_size;

            double scalar = Math.Pow(1.25, num_doublings);

            if (scroll_delta_sum < 0)
                scalar = 1 / scalar;

            Trace.WriteLine(string.Format("{0} {1} {2}", adjusted_scroll_distance, num_doublings, scalar));

            return scalar;
        }

        private int scroll_delta_sum = 0;
        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            Trace.WriteLine(e.Delta.ToString());

            if (Control.ModifierKeys.ShiftPressed())
                scroll_delta_sum += (int)(e.Delta / 5.0);
            else
                scroll_delta_sum += e.Delta;

            double scalar = GetScalarForScrollWheelDelta(scroll_delta_sum); 

            //Trace.WriteLine(scalar.ToString());
            UpdateScale(scalar);
            Parent.Invalidate();
        }


        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Redraw if we are dragging a location
            if (this.oldMouse != null)
            {
                if (e.Button.LeftOnly())
                {
                    GridVector2 LastWorldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                    UpdateViewPosition(NewPosition - LastWorldPosition);
                    //circleView.Circle = new GridCircle(this.TranslatedPosition, circleView.Radius);
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }


        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            base.OnMouseUp(sender, e);
            if (e.Button.Left())
                this.Execute();            
        }

        
    }
}