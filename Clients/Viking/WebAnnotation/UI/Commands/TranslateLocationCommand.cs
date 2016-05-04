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

namespace WebAnnotation.UI.Commands
{
    class TranslateCurveLocationCommand : TranslateLocationCommand
    {
        CurveView curveView;
        GridVector2[] OriginalControlPoints;
        GridVector2 OriginalPosition;
        GridVector2 DeltaSum = new GridVector2(0, 0);

        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        protected override GridVector2 TranslatedPosition
        {
            get
            {
                return OriginalPosition + (DeltaSum);
            } 
        }

        public TranslateCurveLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2 MosaicPosition, 
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        bool IsClosedCurve,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalPosition = mapping.SectionToVolume(MosaicPosition);
            this.OriginalControlPoints = mapping.SectionToVolume(OriginalMosaicControlPoints);
            CreateView(OriginalControlPoints, color.ConvertToHSL(0.5f), LineWidth, IsClosedCurve);
            this.success_callback = success_callback;
        }

        private void CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color, double LineWidth, bool IsClosed)
        {
            curveView = new CurveView(ControlPoints.ToList(), color, false, lineWidth: LineWidth);
            curveView.TryCloseCurve = IsClosed;
        }

        protected override void UpdateViewPosition(GridVector2 PositionDelta)
        {
            DeltaSum += PositionDelta;
            curveView.ControlPoints = curveView.ControlPoints.Select(p => p + PositionDelta).ToArray();
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2[] TranslatedOriginalControlPoints = OriginalControlPoints.Select(p => p + DeltaSum).ToArray();
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

                this.success_callback(TranslatedOriginalControlPoints, MosaicControlPoints);
            }

            base.Execute();
        }


        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.annotationOverlayEffect, 0, new CurveView[] { this.curveView });
        }
    }

    class TranslateCircleLocationCommand : TranslateLocationCommand
    {
        CircleView circleView;
        GridCircle OriginalCircle;

        public delegate void OnCommandSuccess(GridVector2 VolumePosition, GridVector2 MosaicPosition, double NewRadius);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        private double _sizeScale = 1.0;
        protected double SizeScale
        {
            get
            {
                return _sizeScale;
            }
            set
            {
                _sizeScale = value;
                CreateView(circleView.VolumePosition, OriginalCircle.Radius * _sizeScale, circleView.Color);
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
            if (e.Button == MouseButtons.Middle)
            {
                this.SizeScale = 1.0;
                return;
            }

            base.OnMouseDown(sender, e);
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e)
        {
            float multiplier = ((float)e.Delta / 120.0f);

            if (multiplier < 0)
                SizeScale *= 0.9900990099009901;
            else if(multiplier > 0)
                SizeScale *= 1.01f;
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

        protected abstract void UpdateViewPosition(GridVector2 PositionDelta);

       

        

        public override void OnDeactivate()
        {
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }


        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            //Redraw if we are dragging a location
            if (this.oldMouse != null)
            {
                if (this.oldMouse.Button == MouseButtons.Left)
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
            this.Execute();            
        }

        
    }
}