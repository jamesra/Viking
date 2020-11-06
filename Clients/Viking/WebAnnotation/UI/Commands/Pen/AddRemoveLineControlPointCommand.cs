using Geometry;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WebAnnotation.UI.Commands
{
    class AddLineControlPointCommand : AnnotationCommandBase
    {
        GridVector2[] OriginalControlPoints;
        GridVector2[] NewControlPoints;
        private int iNewControlPoint = -1;

        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public AddLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            this.OriginalControlPoints = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(OriginalMosaicControlPoints);

            this.success_callback = success_callback;

            mapping = parent.Section.ActiveSectionToVolumeTransform;
        }

        public static GridVector2[] AddControlPoint(GridVector2[] OriginalControlPoints, GridVector2 NewControlPointPosition, out int iNewControlPoint)
        {
            iNewControlPoint = -1;
            GridLineSegment[] lineSegs = GridLineSegment.SegmentsFromPoints(OriginalControlPoints);

            //Find the line segment the NewControlPoint intersects
            double MinDistance;
            int iNearestSegment = lineSegs.NearestSegment(NewControlPointPosition, out MinDistance);
            GridLineSegment[] updatedSegments = lineSegs.Insert(NewControlPointPosition, iNearestSegment);

            return updatedSegments.Verticies();
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
        bool IsClosedShape;

        public delegate void OnCommandSuccess(GridVector2[] VolumeControlPoints, GridVector2[] MosaicControlPoints);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public RemoveLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridVector2[] OriginalMosaicControlPoints,
                                        bool IsClosed,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            IsClosedShape = IsClosed;
            this.OriginalControlPoints = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(OriginalMosaicControlPoints);

            this.success_callback = success_callback;

            mapping = parent.Section.ActiveSectionToVolumeTransform;
        }

        public static GridVector2[] RemoveControlPoint(GridVector2[] OriginalControlPoints, GridVector2 RemovedControlPointPosition, bool IsClosedShape)
        {
            double MinDistance;
            int iNearestPoint = OriginalControlPoints.NearestPoint(RemovedControlPointPosition, out MinDistance);

            GridVector2[] newControlPoints = new GridVector2[OriginalControlPoints.Length - 1];

            Array.Copy(OriginalControlPoints, newControlPoints, iNearestPoint);
            Array.Copy(OriginalControlPoints, iNearestPoint + 1, newControlPoints, iNearestPoint, OriginalControlPoints.Length - (iNearestPoint + 1));
            /*
            for (int iOldPoint=0; iOldPoint < iNearestPoint; iOldPoint++)
            {
                newControlPoints[iOldPoint] = OriginalControlPoints[iOldPoint];
            }

            for (int iOldPoint = iNearestPoint+1; iOldPoint < OriginalControlPoints.Length; iOldPoint++)
            {
                newControlPoints[iOldPoint-1] = OriginalControlPoints[iOldPoint];
            }

            //The first point in a closed shape is equal to the last point.  If we remove the first point we must update the last point to match the new first point.
            if(IsClosedShape && iNearestPoint == 0)
            {
                newControlPoints[newControlPoints.Length - 1] = newControlPoints[0];
            }
            */
            return newControlPoints;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.NewControlPoints = RemoveLineControlPointCommand.RemoveControlPoint(OriginalControlPoints, NewControlPointPosition, this.IsClosedShape);
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
