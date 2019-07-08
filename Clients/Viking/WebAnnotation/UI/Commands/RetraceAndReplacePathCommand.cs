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

        public GridPolygon OutputMosaicPolygon;
        public GridPolygon OutputVolumePolygon;


        public override  uint NumCurveInterpolations
        {
            get
            {
                return Global.NumClosedCurveInterpolationPoints;
            }
        }
         
        PenInputHelper PenInput;

        Viking.VolumeModel.IVolumeToSectionTransform mapping;

        /// <summary>
        /// Returns unsmoothed mosaic and volume polygons with the new point
        /// </summary>
        /// <param name="MosaicPolygon"></param>
        /// <param name="VolumePolygon"></param>
        //public delegate void OnCommandSuccess(GridPolygon MosaicPolygon, GridPolygon VolumePolygon);
        //OnCommandSuccess success_callback;


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
            PenInput.OnPathChanged += OnPenPathChanged;
            PenInput.OnPathCompleted += OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged += OnPenProposedNextSegmentChanged;

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

        }

        protected override void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //Update the curve
            this.curve_verticies = new CurveViewControlPoints(Verticies, NumInterpolations: 0, TryToClose: false);

            //Find a possible intersection point
            GridVector2? IntersectionPoint = PenInput.Segments.IntersectionPoint(PenInput.LastSegment, true, out GridLineSegment? IntersectedSegment);
            OriginalMosaicPolygon.AddVertex(IntersectionPoint.Value);
            //If the intersection exists
            if (IntersectionPoint.HasValue)
            {
                GridVector2 IntersectedSegmentEndpoint = IntersectedSegment.Value.A;
                int intersection_index = PenInput.Path.FindIndex(val => val == IntersectedSegmentEndpoint);

                List<GridVector2> cropped_path = new List<GridVector2>(PenInput.Path);

                cropped_path.RemoveRange(intersection_index, PenInput.Path.Count - intersection_index);

                cropped_path.Add(IntersectionPoint.Value);
                cropped_path.RemoveAt(0); 
                //Remove the endpoint that was just added which intersected our path and replace it with the intersection point
                cropped_path[0] = IntersectionPoint.Value;

                //PenInput.Pop();
                //PenInput.Push(IntersectionPoint.Value);

                PenInput.SimplifiedPath = cropped_path.DouglasPeuckerReduction(15);
                //PenInput.Push(IntersectionPoint.Value);3

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


        //Will return orignal array with values from A to B removed and add array is inserted where the values were removed.
        //Both arrays must contain points A and B.
        protected GridVector2[] Replace(GridVector2 A, GridVector2 B, GridVector2[] original, GridVector2[] add)
        {
            List<GridVector2> polygon = original.ToList();

            int i = polygon.IndexOf(A);
            do
            {
                polygon.RemoveAt(i);
            }
            while (!polygon[i].Equals(B));
            polygon.InsertRange(i, add);
            return polygon.ToArray();
        }

        private bool IsNull(Object val)
        {
            if(Object.ReferenceEquals(val, null))
            {
                return true;
            }
            return false;
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

        public static GridVector2[] AddControlPoint(GridVector2[] OriginalControlPoints, GridVector2 NewControlPointPosition, out int iNewControlPoint)
        {
            iNewControlPoint = -1;
            GridLineSegment[] lineSegs = GridLineSegment.SegmentsFromPoints(OriginalControlPoints);

            //Find the line segment the NewControlPoint intersects
            double MinDistance;
            int iNearestSegment = lineSegs.NearestSegment(NewControlPointPosition, out MinDistance);
            GridLineSegment[] updatedSegments = lineSegs.Insert(NewControlPointPosition, iNearestSegment);

            return updatedSegments.Verticies();
        }
        
        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (PenInput.Path.Count > 1)
            {
                CurveView curveView = new CurveView(PenInput.Path, this.LineColor, false, Global.NumCurveInterpolationPoints(false), lineWidth: this.LineWidth, lineStyle: this.Style, controlPointRadius: this.ControlPointRadius);
                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView });
            }

            /*
            GridVector2? SelfIntersection = ProposedControlPointSelfIntersection(this.oldWorldPosition);

            
            if (SelfIntersection.HasValue || CanControlPointBePlaced(this.oldWorldPosition))
            {
                bool pushed_point = true;

                if (SelfIntersection.HasValue)
                    vert_stack.Push(SelfIntersection.Value);
                else if (!OverlapsLastVertex(this.oldWorldPosition))
                    vert_stack.Push(this.oldWorldPosition);
                else
                    pushed_point = false;

                CurveView curveView = new CurveView(vert_stack.ToArray(), this.LineColor, !this.IsOpen, Global.NumCurveInterpolationPoints(!this.IsOpen), lineWidth: this.LineWidth, lineStyle: Style, controlPointRadius: this.ControlPointRadius);
                curveView.Color.SetAlpha(this.ShapeIsValid() ? 1 : 0.25f);
                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView });
                //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

                if (pushed_point)
                    this.vert_stack.Pop();

                base.OnDraw(graphicsDevice, scene, basicEffect);
            }
            else
            {
                if (this.Verticies.Length > 1)
                {
                    CurveView curveView = new CurveView(this.Verticies.ToArray(), this.LineColor, !this.IsOpen, Global.NumCurveInterpolationPoints(!this.IsOpen), lineWidth: this.LineWidth, lineStyle: Style, controlPointRadius: this.ControlPointRadius);
                    curveView.Color.SetAlpha(this.ShapeIsValid() ? 1 : 0.25f);
                    CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView });
                }
                else
                {
                    CircleView view = new CircleView(new GridCircle(this.Verticies.First(), this.LineWidth / 2.0), this.LineColor);
                    CircleView.Draw(graphicsDevice, scene, basicEffect, this.Parent.AnnotationOverlayEffect, new CircleView[] { view });
                }
            }
            */
        }
    }
}