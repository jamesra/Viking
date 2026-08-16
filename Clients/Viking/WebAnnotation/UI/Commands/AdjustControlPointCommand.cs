using Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    internal class AdjustCurveControlPointCommand : AnnotationCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        //LocationObj Loc;
        private CurveView curveView;
        private readonly Vector2[] OriginalControlPoints;
        private int iAdjustedControlPoint = -1;

        public delegate void OnCommandSuccess(Vector2[] VolumeControlPoints, Vector2[] MosaicControlPoints);

        private readonly OnCommandSuccess success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public string[] HelpStrings => ["Release Left Mouse Button to place control point"];

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);

        public AdjustCurveControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Vector2[] OriginalMosaicControlPoints,
                                        Microsoft.Xna.Framework.Color color,
                                        double LineWidth,
                                        bool IsClosedCurve,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            OriginalControlPoints = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(OriginalMosaicControlPoints);
            CreateView(OriginalControlPoints, color.ConvertToHCL(0.5f), LineWidth, IsClosedCurve);
            this.success_callback = success_callback;
            mapping = parent.Section.ActiveSectionToVolumeTransform;
        }

        private void CreateView(Vector2[] ControlPoints, Microsoft.Xna.Framework.Color color, double LineWidth, bool IsClosed)
        {
            curveView = new CurveView([.. ControlPoints], color, IsClosed,
                                      Global.NumCurveInterpolationPoints(IsClosed),
                                      lineWidth: LineWidth);
        }

        protected virtual void UpdatePosition(Vector2 PositionDelta) => curveView.SetPoint(iAdjustedControlPoint, curveView.ControlPoints[iAdjustedControlPoint] + PositionDelta);

        protected void PopulateControlPointIndexIfNeeded(Vector2 WorldPosition)
        {
            if (iAdjustedControlPoint < 0)
            {
                double[] DistanceArray = [.. curveView.ControlPoints.Select(p => Vector2.Distance(p, WorldPosition))];
                iAdjustedControlPoint = Array.IndexOf(DistanceArray, DistanceArray.Min());
            }
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Vector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
            PopulateControlPointIndexIfNeeded(NewPosition);

            //Redraw if we are dragging a location
            if (oldMouse != null)
            {
                if (oldMouse.Button.Left())
                {
                    Vector2 LastWorldPosition = Parent.ScreenToWorld(oldMouse.X, oldMouse.Y);
                    UpdatePosition(NewPosition - LastWorldPosition);
                    //circleView.Circle = new Circle(this.TranslatedPosition, circleView.Radius);
                    Parent.Invalidate();
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button.Left())
            {
                Vector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);
                PopulateControlPointIndexIfNeeded(NewPosition);

                Execute();
            }

            base.OnMouseUp(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene,
                                    Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect) => CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, [curveView]);

        protected override void Execute()
        {
            if (success_callback != null)
            {
                Vector2[] TranslatedOriginalControlPoints;
                Vector2[] MosaicControlPoints = null;

                if (curveView.TryCloseCurve)
                {
                    List<Vector2> LoopedPointsList = [.. curveView.ControlPoints];
                    if (curveView.ControlPoints.First() != curveView.ControlPoints.Last())
                    {
                        LoopedPointsList.Add(LoopedPointsList.First());
                    }

                    TranslatedOriginalControlPoints = [.. LoopedPointsList];
                }
                else
                {
                    TranslatedOriginalControlPoints = [.. curveView.ControlPoints];
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

                success_callback(TranslatedOriginalControlPoints, MosaicControlPoints);
            }

            base.Execute();
        }

    }
}
