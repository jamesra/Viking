using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.VolumeModel;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    class CutHoleWithPenCommand : PlaceClosedCurveWithPenCommand
    {
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;

        List<GridLineSegment> ExteriorSegments;
        public override uint NumCurveInterpolations
        {
            get
            {
                return Global.NumClosedCurveInterpolationPoints;
            }
        }
           
        Viking.VolumeModel.IVolumeToSectionTransform mapping;

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
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
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
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
        }

        protected override bool IsProposedClosedLoopValid(IReadOnlyCollection<GridVector2> proposed_curve)
        {
            GridPolygon proposed_hole = new GridPolygon(proposed_curve.ToArray().EnsureClosedRing());
            return false == GridPolygon.SegmentsIntersect(this.OriginalVolumePolygon, proposed_hole);
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
            if (this.PenInput.Points.Count < 3 || this.PenInput.HasSelfIntersection == false)
                return false;

            //We cannot intersect any existing feature of the polygon
            if (this.PenInput.Segments.Any(s => OriginalVolumePolygon.Intersects(s)))
                return false;

            try
            {
                return this.PenInput.Loop.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException e)
            {
                return false;
            }
        }
    }
}