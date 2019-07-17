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
    class RetraceAndReplacePathCommand : PlaceCurveWithPenCommand
    {
        GridPolygon OriginalMosaicPolygon;
        GridPolygon OriginalVolumePolygon;

        List<GridLineSegment> ExteriorSegments;
        List<GridVector2> ExteriorPoints;

        public GridPolygon OutputMosaicPolygon;
        public GridPolygon OutputVolumePolygon;
        public GridPolygon SmoothedVolumePolygon;

        public override  uint NumCurveInterpolations
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


        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        GridPolygon mosaic_polygon,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
            mapping = parent.Section.ActiveSectionToVolumeTransform;
            this.OriginalMosaicPolygon = mosaic_polygon;
            this.OriginalVolumePolygon = mapping.TryMapShapeSectionToVolume(mosaic_polygon);
            //SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);
            ExteriorPoints = OriginalVolumePolygon.ExteriorRing.ToList();
            ExteriorSegments = OriginalVolumePolygon.ExteriorSegments.ToList();
            //PenInput.Push(origin);

        }

        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
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
            SmoothedVolumePolygon = OriginalVolumePolygon.Smooth(Global.NumClosedCurveInterpolationPoints);

        }

        protected override void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if(PenInput == null || PenInput.Path.Count < 3)
            {
                return;
            }
            
            //Update the curve
            this.curve_verticies = new CurveViewControlPoints(Verticies, NumInterpolations: 0, TryToClose: false);

            //Find a possible intersection point
            GridVector2? IntersectionPoint = ExteriorSegments.IntersectionPoint(PenInput.LastSegment, false, out GridLineSegment? IntersectedSegment);
            //If the intersection exists
            if (IntersectionPoint.HasValue)
            {
                //Find the Origin of the path's intersection point and add it too the Exterior Points
                GridVector2 OriginIndexAPoint = ExteriorSegments[ExteriorSegments.NearestSegment(PenInput.Path[PenInput.Path.Count - 1], out double MinDistance)].B;
                int OriginIndexA = ExteriorPoints.FindIndex(val => val == OriginIndexAPoint);
                ExteriorPoints.Insert(OriginIndexA, PenInput.Path[PenInput.Path.Count - 1]);

                //Add the second intersect endpoint of our drawn path into the polygon
                int indexA = ExteriorPoints.FindIndex(val => val == IntersectedSegment.Value.B);
                ExteriorPoints.Insert(indexA, IntersectionPoint.Value);

                //Add in logic for all other path points. Includes all values except first and last.
                int RetraceEndpointA = ExteriorPoints.FindIndex(val => val == PenInput.Path[PenInput.Path.Count - 1]);
                GridVector2 PointA = PenInput.Path[PenInput.Path.Count - 1];
                int RetraceEndpointB = ExteriorPoints.FindIndex(val => val == IntersectionPoint.Value);
                GridVector2 PointB = IntersectionPoint.Value;
                PenInput.Path.RemoveAt(0);
                PenInput.Path.RemoveAt(PenInput.Path.Count - 1);

                //If the crossed out section contains our origin we need to relocate the origin somewhere else

                double distanceAB = DistanceBetweenPointsOnPolygon(RetraceEndpointA, RetraceEndpointB, ExteriorPoints);
                double distanceBA = DistanceBetweenPointsOnPolygon(RetraceEndpointB, RetraceEndpointA, ExteriorPoints);
                int distance = Math.Abs(RetraceEndpointA - RetraceEndpointB);

                if (distanceAB < distanceBA)
                {
                    //If the crossed out section contains our origin we need to relocate the origin somewhere else
                    RotatePolygon(RetraceEndpointA - 1);
                    RetraceEndpointA = 0;

                    ExteriorPoints.RemoveRange(RetraceEndpointA + 1, distance);
                    PenInput.Path.Reverse();
                    ExteriorPoints.InsertRange(RetraceEndpointA + 1, PenInput.Path);
                }
                else if(distanceBA < distanceAB)
                {
                    //If the crossed out section contains our origin we need to relocate the origin somewhere else
                    RotatePolygon(RetraceEndpointB - 1);
                    RetraceEndpointB = 0;

                    ExteriorPoints.RemoveRange(RetraceEndpointB + 1, distance);
                    ExteriorPoints.InsertRange(RetraceEndpointB + 1, PenInput.Path);
                }

                //Rebuild the new polygon
                ExteriorPoints = ExteriorPoints.DouglasPeuckerReduction(15);
                OutputVolumePolygon = new GridPolygon(ExteriorPoints);
                List<GridVector2[]> interiorRings = OriginalVolumePolygon.InteriorRings.ToList();

                foreach(GridVector2[] interiorRing in interiorRings)
                {
                    OutputVolumePolygon.AddInteriorRing(interiorRing);
                }

                try
                {
                    OutputMosaicPolygon = mapping.TryMapShapeVolumeToSection(OutputVolumePolygon);
                }
                catch (ArgumentOutOfRangeException)
                {
                    Console.WriteLine("TranslateLocationCommand: Could not map polygon to section on Execute", "Command");
                    return;
                }

                this.Execute();
            }

            this.Parent.Invalidate();
        }

        private void RotatePolygon(int valuesToRotate)
        {
            ExteriorPoints.RemoveAt(0);
            for (int i = 0; i < valuesToRotate; i++)
            {
                ExteriorPoints.Add(ExteriorPoints[0]);
                ExteriorPoints.RemoveAt(0);
            }
            ExteriorPoints.Add(ExteriorPoints[0]);
        }

        private double DistanceBetweenPointsOnPolygon(int indexA, int indexB, List<GridVector2> points)
        {
            double distance = 0;
            for (int i = indexA; i != indexB; i++)
            {
                if(i == points.Count - 1)
                {
                    distance += GridVector2.Distance(points[i], points[0]);
                    i = 0;
                }
                distance += GridVector2.Distance(points[i], points[i + 1]);
            }
            return distance;
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