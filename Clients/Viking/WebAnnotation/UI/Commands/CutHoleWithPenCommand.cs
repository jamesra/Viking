using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using System.Windows.Forms;
using WebAnnotation.View;
using VikingXNAGraphics;
using SqlGeometryUtils;
using VikingXNAWinForms;
using Viking.VolumeModel;

namespace WebAnnotation.UI.Commands
{
    class CutHoleWithPenCommand : PlaceCurveWithPenCommand
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

        public static int _NextID = 0;
        public int ID;

        private void AssignID()
        {
            this.ID = _NextID;
            _NextID = _NextID + 1;
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
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
            AssignID();
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
            //SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

            ExteriorSegments = OriginalVolumePolygon.ExteriorSegments.ToList();

            //PenInput.Push(origin);
            leftPolygonAtPathLength = 0;

        }

        public CutHoleWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color.ToXNAColor(), origin, LineWidth, false, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);


        }
        private int leftPolygonAtPathLength;
        protected override void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (PenInput.Path.Count < leftPolygonAtPathLength)
                LineColor = Microsoft.Xna.Framework.Color.White;

            //Update the curve
            this.curve_verticies = new CurveViewControlPoints(PenInput.Path, NumInterpolations: 0, TryToClose: false);

            //Find a possible intersection point
            GridVector2? IntersectionPoint = PenInput.Segments.IntersectionPoint(PenInput.LastSegment, true, out GridLineSegment? IntersectedSegment);

            GridVector2? PolygonIntersectionPoint = OriginalVolumePolygon.AllSegments.IntersectionPoint(PenInput.LastSegment, false);
            if (PolygonIntersectionPoint.HasValue && LineColor != Microsoft.Xna.Framework.Color.Red)
            {
                LineColor = Microsoft.Xna.Framework.Color.Red;
                leftPolygonAtPathLength = PenInput.Path.Count - 1;
            }

            //If the intersection exists
            if (IntersectionPoint.HasValue && LineColor != Microsoft.Xna.Framework.Color.Red)
            {
                GridVector2 IntersectedSegmentEndpoint = IntersectedSegment.Value.A;
                int intersection_index = PenInput.Path.FindIndex(val => val == IntersectedSegmentEndpoint);

                List<GridVector2> cropped_path = new List<GridVector2>(PenInput.Path);
                cropped_path.RemoveRange(intersection_index, PenInput.Path.Count - intersection_index);
                cropped_path.Add(IntersectionPoint.Value);
                cropped_path.RemoveAt(0);
                //Remove the endpoint that was just added which intersected our path and replace it with the intersection point
                cropped_path[0] = IntersectionPoint.Value;

                PenInput.SimplifiedPath = cropped_path.DouglasPeuckerReduction(Global.PenSimplifyThreshold);
                
                this.Execute(PenInput.SimplifiedPath.ToArray());
            }

            this.Parent.Invalidate();
        }


        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {

        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {

        }

        /// <summary>
        /// Can a control point be placed or the command completed by clicking the mouse at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanControlPointBePlaced(GridVector2 WorldPos)
        {
            return (!OverlapsAnyVertex(WorldPos));
        }

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete(GridVector2 WorldPos)
        {
            return (OverlapsLastVertex(WorldPos) || OverlapsFirstVertex(WorldPos)) && ShapeIsValid();
        }

        protected override bool ShapeIsValid()
        {
            if (this.Verticies.Length < 3 || curve_verticies == null || this.curve_verticies.ControlPoints.Length < 3)
                return false;

            try
            {
                return this.curve_verticies.ControlPoints.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException e)
            {
                return false;
            }
        }
    }
}