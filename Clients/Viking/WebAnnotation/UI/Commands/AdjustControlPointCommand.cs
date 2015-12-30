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
    class AdjustCurveControlPointCommand : AnnotationCommandBase
    {
        LocationObj Loc;
        CurveView curveView;
        GridVector2[] OriginalControlPoints;
        private int iAdjustedControlPoint = -1;

        public delegate void OnCommandSuccess(LocationObj loc, GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        public AdjustCurveControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        LocationObj selectedObj,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            Loc = selectedObj;
            OriginalControlPoints = selectedObj.VolumeShape.ToPoints();
            CreateView(OriginalControlPoints, selectedObj.Parent.Type.Color.ToXNAColor().ConvertToHSL(0.5f), IsClosedCurve(selectedObj));
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

        public override void OnDeactivate()
        {
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        protected virtual void UpdatePosition(GridVector2 PositionDelta)
        {
            curveView.SetPoint(this.iAdjustedControlPoint, curveView.ControlPoints[iAdjustedControlPoint] + PositionDelta);
        }

        protected void PopulateControlPointIndexIfNeeded(GridVector2 WorldPosition)
        {
            if(iAdjustedControlPoint < 0)
            {
                double[] DistanceArray = this.curveView.ControlPoints.Select(p => GridVector2.Distance(p, WorldPosition)).ToArray();
                iAdjustedControlPoint = Array.IndexOf(DistanceArray, DistanceArray.Min());
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            PopulateControlPointIndexIfNeeded(NewPosition);

            //Redraw if we are dragging a location
            if (this.oldMouse != null)
            {
                if (this.oldMouse.Button == MouseButtons.Left)
                {
                    GridVector2 LastWorldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    UpdatePosition(NewPosition - LastWorldPosition);
                    //circleView.Circle = new GridCircle(this.TranslatedPosition, circleView.Radius);
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            PopulateControlPointIndexIfNeeded(NewPosition);

            base.OnMouseUp(sender, e);
            this.Execute();
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice,
                                    VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, basicEffect, Parent.annotationOverlayEffect, new CurveView[] { this.curveView });
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2[] TranslatedOriginalControlPoints = curveView.ControlPoints.ToArray();
                GridVector2[] MosaicControlPoints = null;

                try
                {
                    MosaicControlPoints = Parent.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedOriginalControlPoints.ToString(), "Command");
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
    }
}
