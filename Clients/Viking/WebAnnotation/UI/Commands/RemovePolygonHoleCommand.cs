using Geometry;
using System;
using System.Diagnostics;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    internal class RemovePolygonHoleCommand : AnnotationCommandBase
    {
        private readonly GridPolygon OriginalMosaicPolygon;
        private readonly GridPolygon OriginalVolumePolygon;
        private readonly GridPolygon UpdatedMosaicPolygon;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);

        private readonly OnCommandSuccess success_callback;
        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="mosaic_polygon"></param>
        /// <param name="hole_position">Point in polygon where user asked to remove hole</param>
        /// <param name="success_callback"></param>
        public RemovePolygonHoleCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        GridVector2 hole_mosaic_position,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            UpdatedMosaicPolygon = mosaic_polygon.Clone() as GridPolygon;
            this.success_callback = success_callback;

            //Launch the remove action
            parent.BeginInvoke(new Action(() => RemoveInteriorHole(UpdatedMosaicPolygon, hole_mosaic_position)));
        }

        /// <summary>
        /// Remove the hole that contains the point
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="holePosition"></param>
        /// <returns></returns>
        public void RemoveInteriorHole(GridPolygon polygon, GridVector2 holePosition)
        {
            if (polygon.TryRemoveInteriorRing(holePosition))
            {
                Execute();
            }
            else
            {
                //Could not remove the interior polygon, so do nothing
                Deactivated = true;
            }
        }

        protected override void Execute()
        {
            GridPolygon UpdatedVolumePolygon;
            try
            {
                UpdatedVolumePolygon = mapping.TryMapShapeSectionToVolume(UpdatedMosaicPolygon);
            }
            catch (ArgumentOutOfRangeException)
            {
                Trace.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                return;
            }

            success_callback(UpdatedMosaicPolygon, UpdatedVolumePolygon);

            base.Execute();
        }
    }
}
