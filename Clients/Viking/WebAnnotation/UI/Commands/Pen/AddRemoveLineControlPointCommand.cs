using Geometry;
using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Adds a control point to a line or curve where the cursor is, on the control points the forward owner draws
    /// (<c>LocationVolumeGeometry.ControlPoints</c>), and hands the new volume control points to the callback, which maps
    /// them back to mosaic through the inverse owner.
    /// </summary>
    internal class AddLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Vector2[] OriginalVolumeControlPoints,
AddLineControlPointCommand.OnCommandSuccess success_callback) : AnnotationCommandBase(parent)
    {
        private readonly Vector2[] OriginalControlPoints = OriginalVolumeControlPoints;
        private Vector2[] NewControlPoints;
        private int iNewControlPoint = -1;

        /// <summary>The control points with the new one, in volume space.</summary>
        public delegate void OnCommandSuccess(Vector2[] VolumeControlPoints);

        private readonly OnCommandSuccess success_callback = success_callback;

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
            success_callback(NewControlPoints);

            base.Execute();
        }
    }

    /// <summary>
    /// Removes the control point of a line or curve nearest the cursor, on the control points the forward owner draws, and
    /// hands the remaining volume control points to the callback, which maps them back to mosaic through the inverse owner.
    /// </summary>
    internal class RemoveLineControlPointCommand(Viking.UI.Controls.SectionViewerControl parent,
                                    Vector2[] OriginalVolumeControlPoints,
                                    bool IsClosed,
RemoveLineControlPointCommand.OnCommandSuccess success_callback) : AnnotationCommandBase(parent)
    {
        private readonly Vector2[] OriginalControlPoints = OriginalVolumeControlPoints;
        private Vector2[] NewControlPoints;
        private readonly bool IsClosedShape = IsClosed;

        /// <summary>The control points without the removed one, in volume space.</summary>
        public delegate void OnCommandSuccess(Vector2[] VolumeControlPoints);

        private readonly OnCommandSuccess success_callback = success_callback;

        public static Vector2[] RemoveControlPoint(Vector2[] OriginalControlPoints, Vector2 RemovedControlPointPosition, bool IsClosedShape)
        {
            int iNearestPoint = OriginalControlPoints.NearestPoint(RemovedControlPointPosition, out double MinDistance);

            Vector2[] newControlPoints = new Vector2[OriginalControlPoints.Length - 1];

            Array.Copy(OriginalControlPoints, newControlPoints, iNearestPoint);
            Array.Copy(OriginalControlPoints, iNearestPoint + 1, newControlPoints, iNearestPoint, OriginalControlPoints.Length - (iNearestPoint + 1));

            // Closed rings store the first vertex again as the last. Removing the first must copy the new first onto the last.
            if (IsClosedShape && iNearestPoint == 0 && newControlPoints.Length > 0)
                newControlPoints[newControlPoints.Length - 1] = newControlPoints[0];

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
            success_callback(NewControlPoints);

            base.Execute();
        }
    }

}
