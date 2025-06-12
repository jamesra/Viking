using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    internal class CutHoleWithPenCommand : PlaceClosedCurveWithPenCommand
    {
        private readonly GridPolygon OriginalMosaicPolygon;
        private readonly GridPolygon OriginalVolumePolygon;
        private readonly List<GridLineSegment> ExteriorSegments;
        public override uint NumCurveInterpolations => Global.NumClosedCurveInterpolationPoints;

        private readonly Viking.VolumeModel.IVolumeToSectionTransform mapping;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>

        public CutHoleWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
            //SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

            ExteriorSegments = OriginalVolumePolygon.ExteriorSegments.ToList();

            //PenInput.Push(origin);
        }

        public CutHoleWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color.ToXNAColor(), origin, LineWidth, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            OriginalMosaicPolygon = mosaic_polygon;
            OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
        }

        protected override bool IsProposedClosedLoopValid(IReadOnlyCollection<GridVector2> proposed_curve)
        {
            GridPolygon proposed_hole = new GridPolygon(proposed_curve.ToArray().EnsureClosedRing());
            return false == GridPolygon.SegmentsIntersect(OriginalVolumePolygon, proposed_hole);
        }


        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {

        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {

        }

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete()
        {
            return PenInput.HasSelfIntersection && ShapeIsValid();
        }

        protected override bool ShapeIsValid()
        {
            if (PenInput.Points.Count < 3 || PenInput.HasSelfIntersection == false)
            {
                return false;
            }

            //We cannot intersect any existing feature of the polygon
            if (PenInput.Segments.Any(s => OriginalVolumePolygon.Intersects(s)))
            {
                return false;
            }

            try
            {
                return PenInput.Loop.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}