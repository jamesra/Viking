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
    class AddLineControlPointCommand : AnnotationCommandBase
    { 
        GridVector2[] OriginalControlPoints;
        GridVector2[] NewControlPoints;
        private int iNewControlPoint = -1;

        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionMapper mapping;

        public AddLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2[] OriginalControlPoints,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            this.OriginalControlPoints = OriginalControlPoints;
            
            this.success_callback = success_callback;

            mapping = parent.Section.ActiveMapping;
        }

        public static GridVector2[] AddControlPoint(GridVector2[] OriginalControlPoints, GridVector2 NewControlPointPosition, out int iNewControlPoint)
        {
            iNewControlPoint = -1;
            GridLineSegment[] lineSegs = GridLineSegment.SegmentsFromPoints(OriginalControlPoints);
            //Find the line segment the NewControlPoint intersects
            double[] distancesToNewPoint = lineSegs.Select(l => l.DistanceToPoint(NewControlPointPosition)).ToArray();
            double MinDistance = distancesToNewPoint.Min();
            int iNearestSegment = distancesToNewPoint.TakeWhile(d => d != MinDistance).Count();
            GridVector2[] newControlPoints = new GridVector2[OriginalControlPoints.Length + 1];

            for(int iLine = 0; iLine < lineSegs.Length; iLine++)
            {
                GridLineSegment segment = lineSegs[iLine];
                if(iLine < iNearestSegment)
                {
                    newControlPoints[iLine] = segment.A;
                }
                else if(iLine == iNearestSegment)
                {
                    newControlPoints[iLine] = segment.A;
                    newControlPoints[iLine + 1] = NewControlPointPosition;
                    iNewControlPoint = iLine + 1; 
                    newControlPoints[iLine + 2] = segment.B;
                }
                else
                {
                    newControlPoints[iLine + 2] = segment.B;
                }
            }

            return newControlPoints;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.NewControlPoints = AddLineControlPointCommand.AddControlPoint(OriginalControlPoints, NewControlPointPosition, out iNewControlPoint);
            base.OnMouseMove(sender, e);
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        } 

        protected override void Execute()
        {
            GridVector2[] MosaicControlPoints;
            try
            {
                MosaicControlPoints = mapping.VolumeToSection(NewControlPoints);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + NewControlPoints.ToString(), "Command");
                return;
            }

            this.success_callback(NewControlPoints, MosaicControlPoints);

            base.Execute();
        }
    } 

    class RemoveLineControlPointCommand : AnnotationCommandBase
    {
        GridVector2[] OriginalControlPoints;
        GridVector2[] NewControlPoints; 

        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionMapper mapping;

        public RemoveLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2[] OriginalControlPoints,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            this.OriginalControlPoints = OriginalControlPoints;

            this.success_callback = success_callback;

            mapping = parent.Section.ActiveMapping;
        }

        public static GridVector2[] RemoveControlPoint(GridVector2[] OriginalControlPoints, GridVector2 NewControlPointPosition)
        {
            double[] distancesToRemovalPoint = OriginalControlPoints.Select(p => GridVector2.Distance(p, NewControlPointPosition)).ToArray();
            double MinDistance = distancesToRemovalPoint.Min();
            int iNearestPoint = distancesToRemovalPoint.TakeWhile(d => d != MinDistance).Count();
            GridVector2[] newControlPoints = new GridVector2[OriginalControlPoints.Length - 1];

            for(int iOldPoint=0; iOldPoint < iNearestPoint; iOldPoint++)
            {
                newControlPoints[iOldPoint] = OriginalControlPoints[iOldPoint];
            }

            for (int iOldPoint = iNearestPoint+1; iOldPoint < OriginalControlPoints.Length; iOldPoint++)
            {
                newControlPoints[iOldPoint-1] = OriginalControlPoints[iOldPoint];
            }

            return newControlPoints;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.NewControlPoints = RemoveLineControlPointCommand.RemoveControlPoint(OriginalControlPoints, NewControlPointPosition);
            base.OnMouseMove(sender, e);
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            GridVector2[] MosaicControlPoints;
            try
            {
                MosaicControlPoints = mapping.VolumeToSection(NewControlPoints);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + NewControlPoints.ToString(), "Command");
                return;
            }

            this.success_callback(NewControlPoints, MosaicControlPoints);

            base.Execute();
        }
    }
    
}
