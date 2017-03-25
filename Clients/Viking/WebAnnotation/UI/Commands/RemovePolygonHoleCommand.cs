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
using Viking.VolumeModel;
using System.Windows.Forms;
using System.Diagnostics;

namespace WebAnnotation.UI.Commands
{
    class RemovePolygonHoleCommand : AnnotationCommandBase
    {
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;

        GridPolygon UpdatedMosaicPolygon;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);
        OnCommandSuccess success_callback;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

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
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.UpdatedMosaicPolygon = mosaic_polygon.Clone() as GridPolygon;
            this.success_callback = success_callback;

            //Launch the remove action
            parent.BeginInvoke( new Action(() => RemoveInteriorHole(UpdatedMosaicPolygon, hole_mosaic_position)));
        }

        /// <summary>
        /// Remove the hole that contains the point
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="holePosition"></param>
        /// <returns></returns>
        public void RemoveInteriorHole(GridPolygon polygon, GridVector2 holePosition)
        {
            if(polygon.TryRemoveInteriorRing(holePosition))
            {
                this.Execute();
            }
            else
            {
                //Could not remove the interior polygon, so do nothing
                this.Deactivated = true;
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

            this.success_callback(UpdatedMosaicPolygon, UpdatedVolumePolygon);

            base.Execute();
        }
    }
}
