using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands 
{
    class AdjustCurveControlPointCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        //LocationObj Loc;
        CurveView curveView;
        GridVector2[] OriginalControlPoints;
        private int iAdjustedControlPoint = -1; 

        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public string[] HelpStrings
        {
            get
            {
                return new string[] { "Release Left Mouse Button to place control point" };
            }
        }

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }

        public AdjustCurveControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        bool IsClosedCurve,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            this.OriginalControlPoints = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(OriginalMosaicControlPoints);
            CreateView(OriginalControlPoints, color.ConvertToHSL(0.5f), LineWidth, IsClosedCurve);
            this.success_callback = success_callback;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
        }
        
        private void CreateView(GridVector2[] ControlPoints, Microsoft.Xna.Framework.Color color, double LineWidth, bool IsClosed)
        {
            curveView = new CurveView(ControlPoints.ToList(), color, false, lineWidth: LineWidth);
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
                if (oldMouse.Button.Left())
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
            if (e.Button.Left())
            {
                GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                PopulateControlPointIndexIfNeeded(NewPosition);

                this.Execute();
            }

            base.OnMouseUp(sender,e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.annotationOverlayEffect, 0, new CurveView[] { this.curveView });
        }

        protected override void Execute()
        {
            if (this.success_callback != null)
            {
                GridVector2[] TranslatedOriginalControlPoints;
                GridVector2[] MosaicControlPoints = null;

                if (curveView.TryCloseCurve)
                {
                    List<GridVector2> LoopedPointsList = new List<GridVector2>(curveView.ControlPoints);
                    if(curveView.ControlPoints.First() != curveView.ControlPoints.Last())
                        LoopedPointsList.Add(LoopedPointsList.First());
                    TranslatedOriginalControlPoints = LoopedPointsList.ToArray();
                }
                else
                {
                    TranslatedOriginalControlPoints = curveView.ControlPoints.ToArray();
                }

                try
                {
                    MosaicControlPoints = mapping.VolumeToSection(TranslatedOriginalControlPoints);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + TranslatedOriginalControlPoints.ToString(), "Command");
                    return;
                }
                
                this.success_callback(TranslatedOriginalControlPoints, MosaicControlPoints);
            }

            base.Execute();
        }
        
    }
}
