using Geometry;
using System;
using System.Diagnostics;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    internal class RemovePolygonHoleCommand : AnnotationCommandBase
    {
        private readonly Polygon OriginalMosaicPolygon;
        private readonly Polygon UpdatedMosaicPolygon;

        /// <summary>
        /// The mosaic polygon without the hole (mosaic-first: the hole is removed from the stored polygon, so no point is
        /// mapped back); the callback saves it with <c>SetShapeFromGeometryInSection</c>.
        /// </summary>
        public delegate void OnCommandSuccess(Polygon MosaicPolygon);

        private readonly OnCommandSuccess success_callback;
        private readonly Polygon? VolumePolygon;
        private readonly Vector2 VolumeClick;
        private readonly Vector2 MosaicClick;

        /// <summary>
        /// Queue hole removal after the command is assigned as CurrentCommand.
        /// BeginInvoke avoids executing (and completing) during construction.
        /// </summary>
        /// <param name="hole_mosaic_position">Click mapped to mosaic space; may miss if hover used volume/smoothed geometry.</param>
        /// <param name="volume_polygon">The location's control polygon as the forward owner draws it, when it maps.</param>
        /// <param name="hole_volume_position">Click in volume space, same coordinates as hover.</param>
        public RemovePolygonHoleCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Polygon mosaic_polygon,
                                        Vector2 hole_mosaic_position,
                                        OnCommandSuccess success_callback,
                                        Polygon? volume_polygon = null,
                                        Vector2 hole_volume_position = default) : base(parent)
        {
            OriginalMosaicPolygon = mosaic_polygon;
            UpdatedMosaicPolygon = (Polygon)mosaic_polygon.Clone();
            this.success_callback = success_callback;
            VolumePolygon = volume_polygon;
            VolumeClick = hole_volume_position;
            MosaicClick = hole_mosaic_position;

            parent.BeginInvoke(new Action(RemoveInteriorHole));
        }

        /// <summary>
        /// Remove the hole the cursor advertised, then save via <see cref="Execute"/>.
        /// </summary>
        public void RemoveInteriorHole()
        {
            if (PolygonHoleFill.TryRemoveHoleAtClick(
                    UpdatedMosaicPolygon,
                    MosaicClick,
                    VolumePolygon,
                    VolumeClick,
                    Global.NumClosedCurveInterpolationPointsForDisplay))
            {
                Execute();
            }
            else
            {
                Deactivated = true;
            }
        }

        protected override void Execute()
        {
            success_callback(UpdatedMosaicPolygon);

            base.Execute();
        }
    }
}
