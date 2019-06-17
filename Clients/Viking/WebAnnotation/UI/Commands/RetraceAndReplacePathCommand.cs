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

            PenInput = new PenInputHelper(parent);
            PenInput.Push(origin);
            PenInput.OnPathChanged += this.OnPathChanged;
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

            PenInput = new PenInputHelper(parent);
            PenInput.Push(origin);
            PenInput.OnPathChanged += this.OnPathChanged;
        }

        protected void OnPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            this.Parent.Invalidate();

            //TODO: Check if the pen intersects with another edge of the polygon
            bool CarveComplete = false;
            // if CarveComplete
            //    newPolyVerts = Replace(...)
            //    Execute
            //
            //
            //
        }


        //Will return orignal array with values from A to B removed and add array is inserted where the values were removed.
        //Both arrays must contain points A and B.
        protected GridVector2[] Replace(GridVector2 A, GridVector2 B, GridVector2[] original, GridVector2[] add)
        {
            int indexA = Array.IndexOf(original, A);
            int indexB = Array.IndexOf(original, B);
            for(int i = indexA; i <= indexB; i++)
            {
                original.SetValue(null, i);
            }
            original = original.Where(val => !val.Equals(null)).ToArray();
            add.CopyTo(original, indexA);
            return original;
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



        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected override GridVector2? ProposedControlPointSelfIntersection(GridVector2 worldPos)
        {
            GridVector2? retval = new GridVector2?();

            if (NumVerticies < 3)
                return retval;

            if (worldPos != vert_stack.Peek())
            {
                CurveViewControlPoints curveVerticies = AppendControlPointToCurve(worldPos);
                GridVector2[] controlPoints = Verticies;
                GridLineSegment[] proposed_back_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), worldPos));
                GridLineSegment[] proposed_front_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(worldPos, controlPoints[0]));
                GridLineSegment[] existing_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

                proposed_front_curve_segments = proposed_front_curve_segments.ShortenLastVertex();
                existing_curve_segments = existing_curve_segments.ShortenLastVertex();

                GridVector2[] intersections = proposed_front_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }

                intersections = proposed_back_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }
            }

            return retval;
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