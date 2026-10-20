using Geometry;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    internal class AddPolygonVertexCommand : AnnotationCommandBase
    {
        private readonly Polygon OriginalMosaicPolygon;
        private readonly Polygon OriginalVolumePolygon;
        private Polygon UpdatedVolumePolygon;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(Polygon MosaicPolygon, Polygon VolumePolygon);

        private readonly OnCommandSuccess success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public AddPolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon mosaic_polygon,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);

            this.success_callback = success_callback;
        }

        public static Polygon AddControlPoint(Polygon polygon, Vector2 NewControlPointPosition)
        {
            /*
            Polygon intersectingPolygon;
            polygon.NearestPolygonSegment(NewControlPointPosition, out intersectingPolygon);
            intersectingPolygon.AddVertex(NewControlPointPosition);
            */

            //return polygon.Clone() as Polygon;
            Polygon newPoly = (Polygon)polygon.Clone();
            newPoly.AddVertex(NewControlPointPosition);
            return newPoly;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Vector2 NewControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            UpdatedVolumePolygon = AddPolygonVertexCommand.AddControlPoint(OriginalVolumePolygon, NewControlPointPosition);
            base.OnMouseMove(sender, e);
            Parent.BeginInvoke((Action)delegate () { Execute(); });
        }

        /// <summary>
        /// Passes the polygon with the new vertex to the success callback and ends the command.
        /// When part of it lies outside the section's mapping, the callback is skipped and the location keeps its shape.
        /// </summary>
        protected override void Execute()
        {
            try
            {
                TryApplyInSection(mapping, UpdatedVolumePolygon, mosaic_polygon => success_callback(mosaic_polygon, UpdatedVolumePolygon), nameof(AddPolygonVertexCommand));
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            base.Execute();
        }
    }

    internal class RemovePolygonVertexCommand : AnnotationCommandBase
    {
        private readonly Polygon OriginalMosaicPolygon;
        private readonly Polygon OriginalVolumePolygon;
        private Polygon UpdatedVolumePolygon;

        public delegate void OnCommandSuccess(Polygon MosaicPolygon, Polygon VolumePolygon);

        private readonly OnCommandSuccess success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        public RemovePolygonVertexCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon mosaic_polygon,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            this.success_callback = success_callback;

            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
        }

        public static Polygon RemoveControlPoint(Polygon polygon, Vector2 RemovedControlPointPosition)
        {
            polygon.PointIntersectsAnyPolygonSegment(RemovedControlPointPosition, Global.DefaultClosedLineWidth, out Polygon intersectingPolygon);
            if (intersectingPolygon is null)
            {
                return null;
            }

            if (intersectingPolygon.ExteriorRing.Length <= 4) //Closed rings in polygons mean 3 point poly's have 4 points
            {
                return null;
            }

            intersectingPolygon.RemoveVertex(RemovedControlPointPosition);

            return polygon.Clone() as Polygon;
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Vector2 OldControlPointPosition = Parent.ScreenToWorld(e.X, e.Y);
            UpdatedVolumePolygon = RemovePolygonVertexCommand.RemoveControlPoint(OriginalVolumePolygon, OldControlPointPosition);
            base.OnMouseMove(sender, e);
            Parent.BeginInvoke((Action)delegate () { Execute(); });
        }

        /// <summary>
        /// Passes the polygon without the removed vertex to the success callback and ends the command.
        /// When no vertex could be removed, or part of the polygon lies outside the section's mapping,
        /// the callback is skipped and the location keeps its shape.
        /// </summary>
        protected override void Execute()
        {
            if (UpdatedVolumePolygon is null)
            {
                base.Execute();
                return;
            }

            try
            {
                TryApplyInSection(mapping, UpdatedVolumePolygon, mosaic_polygon => success_callback(mosaic_polygon, UpdatedVolumePolygon), nameof(RemovePolygonVertexCommand));
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            base.Execute();
        }
    }

}
