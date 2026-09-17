using Geometry;
using System;
using System.Diagnostics;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    internal class RemovePolygonHoleCommand : AnnotationCommandBase
    {
        private readonly Polygon OriginalMosaicPolygon;
        private readonly Polygon OriginalVolumePolygon;
        private readonly Polygon UpdatedMosaicPolygon;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(Polygon MosaicPolygon, Polygon VolumePolygon);

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
                                        Polygon mosaic_polygon,
                                        Vector2 hole_mosaic_position,
                                        OnCommandSuccess success_callback) : base(parent)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            UpdatedMosaicPolygon = mosaic_polygon.Clone() as Polygon;
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
        public void RemoveInteriorHole(Polygon polygon, Vector2 holePosition)
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
            Polygon UpdatedVolumePolygon;
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
