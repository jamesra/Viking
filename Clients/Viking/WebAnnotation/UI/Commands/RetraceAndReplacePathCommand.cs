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

        GridPolygon VolumePolygonPlusOrigin;
        PointIndex OriginIndex;

        PositionColorMeshModel PrevWalkMesh = null;
        PositionColorMeshModel NextWalkMesh = null;

        /// <summary>
        /// The polygon contour we are retracing, could be interior or exterior
        /// </summary>
        GridPolygon retraced_poly;
        PointIndex OriginPoint;

        public GridPolygon OutputMosaicPolygon;
        public GridPolygon OutputVolumePolygon;
        public GridPolygon SmoothedVolumePolygon;

        private GridPolygon NextWalkPolygon = null;
        private GridPolygon PrevWalkPolygon = null;

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

            this.OriginIndex = AddOriginToPolygon(this.OriginalVolumePolygon, origin, out this.VolumePolygonPlusOrigin);
            SmoothedVolumePolygon = VolumePolygonPlusOrigin.Smooth(Global.NumClosedCurveInterpolationPoints);

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

            this.OriginIndex = AddOriginToPolygon(this.OriginalVolumePolygon, origin, out this.VolumePolygonPlusOrigin);
            SmoothedVolumePolygon = VolumePolygonPlusOrigin.Smooth(Global.NumClosedCurveInterpolationPoints);

        }

        /// <summary>
        /// Identify which polygon we are retracing, add a vertex to the polygon at the origin of the command
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="point"></param>
        /// <param name="updated_poly"></param>
        /// <param name="iVerex"></param>
        /// <returns></returns>
        protected static PointIndex AddOriginToPolygon(GridPolygon original_polygon, GridVector2 point, out GridPolygon updated_poly)
        {
            original_polygon.NearestPolygonSegment(point, out updated_poly);
             
            //Find the Origin of the path's intersection point and add it too the Exterior Points
            List<GridLineSegment> ExteriorSegments = updated_poly.ExteriorSegments.ToList();
            int iInsertionPoint = ExteriorSegments.NearestSegment(point, out double MinDistance);
            GridLineSegment A_To_B = ExteriorSegments[iInsertionPoint];

            //Find out which verticies the endpoints are
            original_polygon.NearestVertex(A_To_B.A, out PointIndex AIndex);
            original_polygon.NearestVertex(A_To_B.B, out PointIndex BIndex);

            ExteriorSegments.RemoveAt(iInsertionPoint);
            GridLineSegment A_To_Origin = new GridLineSegment(A_To_B.A, point);
            GridLineSegment Origin_To_B = new GridLineSegment(point, A_To_B.B);
            ExteriorSegments.InsertRange(iInsertionPoint, new GridLineSegment[] { A_To_Origin, Origin_To_B });

            GridPolygon poly_with_origin = new GridPolygon(ExteriorSegments.Select(l => l.A).ToArray().EnsureClosedRing());

            if(AIndex.IsInner == false)
            {
                updated_poly = poly_with_origin;
            }
            else
            {
                updated_poly = (GridPolygon)original_polygon.Clone();
                updated_poly.ReplaceInteriorRing(AIndex.iInnerPoly.Value, poly_with_origin);
            }

            updated_poly.NearestVertex(point, out PointIndex origin_index);
            return origin_index;
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
            SortedDictionary<double, PointIndex> IntersectingVerts = VolumePolygonPlusOrigin.IntersectingSegments(PenInput.LastSegment);
            if(IntersectingVerts.Count > 0)
            {
                PointIndex FirstIntersect = IntersectingVerts.Values.First();

                if(this.OriginIndex.AreOnSameRing(FirstIntersect))
                {  
                    GridVector2 SegmentIntersectPoint;
                    {
                        //Add the intersection point of where we crossed the boundary
                        GridLineSegment intersected_segment = new GridLineSegment(FirstIntersect.Point(VolumePolygonPlusOrigin), FirstIntersect.Next.Point(VolumePolygonPlusOrigin));
                        intersected_segment.Intersects(PenInput.LastSegment, out SegmentIntersectPoint);
                    }

                    PenInput.Pop();

                    //Yay!
                    {
                        //Walk the ring using Next to find perimeter on one side, the walk using prev to find perimeter on the other
                        List<GridVector2> NextWalkPoly = new List<GridVector2>();
                        PointIndex current = this.OriginIndex;
                        do
                        {
                            NextWalkPoly.Add(current.Point(VolumePolygonPlusOrigin));
                            current = current.Next;
                        }
                        while (current != FirstIntersect);

                        NextWalkPoly.Add(current.Point(VolumePolygonPlusOrigin));

                        //Add the intersection point of where we crossed the boundary
                        NextWalkPoly.Add(SegmentIntersectPoint);

                        //Add the PenInput.Path 
                        NextWalkPoly.AddRange(PenInput.Path);

                        NextWalkPolygon = new GridPolygon(NextWalkPoly.EnsureClosedRing());
                        NextWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(NextWalkPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Green.ConvertToHSL(0.5f));
                    }

                    {
                        //Walk the ring using Next to find perimeter on one side, the walk using prev to find perimeter on the other
                        List<GridVector2> PrevWalkPoly = new List<GridVector2>();
                        PointIndex current = this.OriginIndex;
                        do
                        {
                            PrevWalkPoly.Add(current.Point(VolumePolygonPlusOrigin));
                            current = current.Previous;
                        }
                        while (current != FirstIntersect.Next);

                        PrevWalkPoly.Add(current.Point(VolumePolygonPlusOrigin));

                        PrevWalkPoly.Add(SegmentIntersectPoint);

                        //Add the PenInput.Path 
                        PrevWalkPoly.AddRange(PenInput.Path);
                        PrevWalkPoly.Reverse();
                        PrevWalkPolygon = new GridPolygon(PrevWalkPoly.EnsureClosedRing());
                        PrevWalkMesh = TriangleNetExtensions.CreateMeshForPolygon2D(PrevWalkPolygon.Smooth(Global.NumClosedCurveInterpolationPoints), Microsoft.Xna.Framework.Color.Red.ConvertToHSL(0.5f));
                    }
                }
                else
                {
                    //Bad user!
                }
                /*
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
                */
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

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            
            base.OnDraw(graphicsDevice, scene, basicEffect);

            if (PrevWalkMesh != null && NextWalkMesh != null)
                MeshView<Microsoft.Xna.Framework.Graphics.VertexPositionColor>.Draw(graphicsDevice, scene, new PositionColorMeshModel[] { PrevWalkMesh, NextWalkMesh });


            //GridVector2? SelfIntersection = ProposedControlPointSelfIntersection(this.oldWorldPosition);

            //if (SelfIntersection.HasValue || CanControlPointBePlaced(this.oldWorldPosition))
            //{
            //    bool pushed_point = true;

            //    if (SelfIntersection.HasValue)
            //        Push(SelfIntersection.Value);
            //    else if (!OverlapsLastVertex(this.oldWorldPosition))
            //        Push(this.oldWorldPosition);
            //    else
            //        pushed_point = false;

            //    CurveView curveView = new CurveView(vert_stack.ToArray(), this.LineColor, !this.IsOpen, Global.NumCurveInterpolationPoints(!this.IsOpen), lineWidth: this.LineWidth, lineStyle: Style, controlPointRadius: this.ControlPointRadius);
            //    curveView.Color.SetAlpha(this.ShapeIsValid() ? 1 : 0.25f);
            //    CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView });
            //    //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

            //    if (pushed_point)
            //        Pop();

            //    base.OnDraw(graphicsDevice, scene, basicEffect);
            //}
            //else
            //{
            //    if (this.Verticies.Length > 1)
            //    {
            //        CurveView curveView = new CurveView(this.Verticies.ToArray(), this.LineColor, !this.IsOpen, Global.NumCurveInterpolationPoints(!this.IsOpen), lineWidth: this.LineWidth, lineStyle: Style, controlPointRadius: this.ControlPointRadius);
            //        curveView.Color.SetAlpha(this.ShapeIsValid() ? 1 : 0.25f);
            //        CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView });
            //    }
            //    else
            //    {
            //        CircleView view = new CircleView(new GridCircle(this.Verticies.First(), this.LineWidth / 2.0), this.LineColor);
            //        CircleView.Draw(graphicsDevice, scene, basicEffect, this.Parent.AnnotationOverlayEffect, new CircleView[] { view });
            //    }
            //}
        }
    }
}