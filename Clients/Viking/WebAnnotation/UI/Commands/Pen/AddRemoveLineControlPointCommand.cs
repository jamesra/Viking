using Geometry;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WebAnnotation.UI.Commands
{
    internal class AddLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Vector2[] OriginalMosaicControlPoints,
AddLineControlPointCommand.OnCommandSuccess success_callback) : AnnotationCommandBase(parent)
    {
        private readonly Vector2[] OriginalControlPoints = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(OriginalMosaicControlPoints);
        private Vector2[] NewControlPoints;
        private int iNewControlPoint = -1;

        public delegate void OnCommandSuccess(Vector2[] VolumeControlPoints, Vector2[] MosaicControlPoints);

        private readonly OnCommandSuccess success_callback = success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping = parent.Section.ActiveSectionToVolumeTransform;

        public static Vector2[] AddControlPoint(Vector2[] OriginalControlPoints, Vector2 NewControlPointPosition, out int iNewControlPoint)
        {
            iNewControlPoint = -1;
            LineSegment[] lineSegs = LineSegment.SegmentsFromPoints(OriginalControlPoints);

            //Find the line segment the NewControlPoint intersects
            int iNearestSegment = lineSegs.NearestSegment(NewControlPointPosition, out double MinDistance);
            LineSegment[] updatedSegments = lineSegs.Insert(NewControlPointPosition, iNearestSegment);

            return updatedSegments.Vertices();
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Vector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            NewControlPoints = AddLineControlPointCommand.AddControlPoint(OriginalControlPoints, NewControlPointPosition, out iNewControlPoint);
            base.OnMouseMove(sender, e);
            Parent.BeginInvoke((Action)delegate () { Execute(); });
        }

        protected override void Execute()
        {
            Vector2[] MosaicControlPoints;
            try
            {
                MosaicControlPoints = mapping.VolumeToSection(NewControlPoints);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + NewControlPoints.ToString(), "Command");
                return;
            }

            success_callback(NewControlPoints, MosaicControlPoints);

            base.Execute();
        }
    }

    internal class RemoveLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Vector2[] OriginalMosaicControlPoints,
                                    bool IsClosed,
RemoveLineControlPointCommand.OnCommandSuccess success_callback) : AnnotationCommandBase(parent)
    {
        private readonly Vector2[] OriginalControlPoints = parent.Section.ActiveSectionToVolumeTransform.SectionToVolume(OriginalMosaicControlPoints);
        private Vector2[] NewControlPoints;
        private readonly bool IsClosedShape = IsClosed;

        public delegate void OnCommandSuccess(Vector2[] VolumeControlPoints, Vector2[] MosaicControlPoints);

        private readonly OnCommandSuccess success_callback = success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping = parent.Section.ActiveSectionToVolumeTransform;

        public static Vector2[] RemoveControlPoint(Vector2[] OriginalControlPoints, Vector2 RemovedControlPointPosition, bool IsClosedShape)
        {
            int iNearestPoint = OriginalControlPoints.NearestPoint(RemovedControlPointPosition, out double MinDistance);

            Vector2[] newControlPoints = new Vector2[OriginalControlPoints.Length - 1];

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
            Vector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            NewControlPoints = RemoveLineControlPointCommand.RemoveControlPoint(OriginalControlPoints, NewControlPointPosition, IsClosedShape);
            base.OnMouseMove(sender, e);
            Parent.BeginInvoke((Action)delegate () { Execute(); });
        }

        protected override void Execute()
        {
            Vector2[] MosaicControlPoints;
            try
            {
                MosaicControlPoints = mapping.VolumeToSection(NewControlPoints);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map world point on Execute: " + NewControlPoints.ToString(), "Command");
                return;
            }

            success_callback(NewControlPoints, MosaicControlPoints);

            base.Execute();
        }
    }

}
