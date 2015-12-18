using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAnnotationModel;
using Geometry;
using WebAnnotation.View;
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

        public delegate void OnCommandSuccess(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        protected override GridVector2 TranslatedPosition
        {
            get
            {
                return OriginalPosition + (DeltaSum);
            } 
        }

        public TranslateCurveLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        LocationObj selectedObj,
                                        OnCommandSuccess success_callback) : base(parent, selectedObj)
        {
            OriginalPosition = selectedObj.VolumeShape.Centroid();
            OriginalControlPoints = selectedObj.VolumeShape.ToPoints();
            CreateView(OriginalControlPoints, selectedObj.Parent.Type.Color.ToXNAColor(192.0f), IsClosedCurve(selectedObj));
            this.success_callback = success_callback;
        }

        private static bool IsClosedCurve(LocationObj loc)
        {
            return loc.TypeCode == LocationType.CLOSEDCURVE;
        }

        private void CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color, bool IsClosed)
        {
            curveView = new CurveView(ControlPoints.ToList(), color, false);
            curveView.TryCloseCurve = IsClosed;
        }

        protected override void UpdateViewPosition(GridVector2 PositionDelta)
        {
            DeltaSum += PositionDelta;
            curveView.ControlPoints = curveView.ControlPoints.Select(p => p + PositionDelta).ToList();
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2[] TranslatedOriginalControlPoints = OriginalControlPoints.Select(p => p + DeltaSum).ToArray();
                GridVector2[] MosaicControlPoints = null;

                try
                {
                    MosaicControlPoints = Parent.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedPosition.ToString(), "Command");
                    return;
                }

                this.success_callback(Loc, TranslatedOriginalControlPoints, MosaicControlPoints);
            }

            base.Execute();
        }

        public static void DefaultSuccessCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints)
        {
            DefaultSuccessNoSaveCallback(loc, VolumeControlPoints, MosaicControlPoints);
            Store.Locations.Save();
        }

        public static void DefaultSuccessNoSaveCallback(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints)
        {
            loc.MosaicShape = SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.MosaicShape.STGeometryType(), MosaicControlPoints);
            loc.VolumeShape = SqlGeometryUtils.GeometryExtensions.ToGeometry(loc.VolumeShape.STGeometryType(), VolumeControlPoints);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            curveView.Draw(graphicsDevice, Parent.LumaOverlayLineManager, scene, basicEffect);
            //CurveView.Draw(graphicsDevice, scene, basicEffect, Parent.annotationOverlayEffect, new CircleView[] { this.circleView });
        }
    }

    class TranslateCircleLocationCommand : TranslateLocationCommand
    {
        CircleView circleView;
        Microsoft.Xna.Framework.Color color;
        GridVector2 OriginalPosition;

        public delegate void OnCommandSuccess(LocationObj loc, GridVector2 VolumePosition, GridVector2 MosaicPosition);
        OnCommandSuccess success_callback;

        protected override GridVector2 TranslatedPosition
        {
            get
            {
                return circleView.VolumePosition;
            }
        }

        public TranslateCircleLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        LocationObj selectedObj,
                                        OnCommandSuccess success_callback) : base(parent, selectedObj)
        {
            CreateView(selectedObj.VolumePosition, selectedObj.Radius, selectedObj.Parent.Type.Color.ToXNAColor(192.0f));
            OriginalPosition = selectedObj.VolumePosition;
            this.success_callback = success_callback;
        }

        private void CreateView(GridVector2 Position, double Radius, Microsoft.Xna.Framework.Color color)
        {
            circleView = new CircleView();
            circleView.Circle = new GridCircle(Position, Radius);
            circleView.BackgroundColor = color;
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2 MosaicPosition;

                try
                {
                    MosaicPosition = Parent.VolumeToSection(TranslatedPosition);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedPosition.ToString(), "Command");
                    return;
                }

                this.success_callback(Loc, this.TranslatedPosition, MosaicPosition);
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
        protected LocationObj Loc; 
         
        /// <summary>
        /// Translated position in volume space
        /// </summary>
        protected abstract GridVector2 TranslatedPosition
        {
             get;
        }

        public TranslateLocationCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        LocationObj selectedObj) : base(parent)
        {
            Loc = selectedObj; 
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