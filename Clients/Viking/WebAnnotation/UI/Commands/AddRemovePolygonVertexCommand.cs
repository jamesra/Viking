using Geometry;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    class AddPolygonVertexCommand : AnnotationCommandBase
    {
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;

        GridPolygon UpdatedVolumePolygon;

        private int iNewControlPoint = -1;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public AddPolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);

            this.success_callback = success_callback;
        }

        public static GridPolygon AddControlPoint(GridPolygon polygon, GridVector2 NewControlPointPosition)
        {
            /*
            GridPolygon intersectingPolygon;
            polygon.NearestPolygonSegment(NewControlPointPosition, out intersectingPolygon);
            intersectingPolygon.AddVertex(NewControlPointPosition);
            */

            //return polygon.Clone() as GridPolygon;
            GridPolygon newPoly = (GridPolygon)polygon.Clone();
            newPoly.AddVertex(NewControlPointPosition);
            return newPoly;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.UpdatedVolumePolygon = AddPolygonVertexCommand.AddControlPoint(OriginalVolumePolygon, NewControlPointPosition);
            base.OnMouseMove(sender, e);
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            GridPolygon mosaic_polygon;
            try
            {
                mosaic_polygon = mapping.TryMapShapeVolumeToSection(UpdatedVolumePolygon);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            this.success_callback(mosaic_polygon, UpdatedVolumePolygon);

            base.Execute();
        }
    }

    class RemovePolygonVertexCommand : AnnotationCommandBase
    {
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;
        GridPolygon UpdatedVolumePolygon;

        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);
        OnCommandSuccess success_callback;
        readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public RemovePolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            this.success_callback = success_callback;

            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
        }

        public static GridPolygon RemoveControlPoint(GridPolygon polygon, GridVector2 RemovedControlPointPosition)
        {
            GridPolygon intersectingPolygon;
            polygon.PointIntersectsAnyPolygonSegment(RemovedControlPointPosition, Global.DefaultClosedLineWidth, out intersectingPolygon);
            if (intersectingPolygon == null)
                return null;

            if (intersectingPolygon.ExteriorRing.Length <= 4) //Closed rings in polygons mean 3 point poly's have 4 points
                return null;

            intersectingPolygon.RemoveVertex(RemovedControlPointPosition);

            return polygon.Clone() as GridPolygon;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 OldControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            this.UpdatedVolumePolygon = RemovePolygonVertexCommand.RemoveControlPoint(OriginalVolumePolygon, OldControlPointPosition);
            base.OnMouseMove(sender, e);
            this.Parent.BeginInvoke((Action)delegate () { this.Execute(); });
        }

        protected override void Execute()
        {
            GridPolygon mosaic_polygon;
            if (UpdatedVolumePolygon == null)
            {
                base.Execute();
                return;
            }

            try
            {
                mosaic_polygon = mapping.TryMapShapeVolumeToSection(UpdatedVolumePolygon);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            this.success_callback(mosaic_polygon, UpdatedVolumePolygon);

            base.Execute();
        }
    }

}
