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

namespace WebAnnotation.UI.Commands
{
    class RetraceAndReplacePathCommand : PlaceCurveWithPenCommand
    {
        public override LineStyle Style
        {
            get
            {
                return LineStyle.HalfTube;
            }
        }

        public override uint NumCurveInterpolations
        {
            get
            {
                return Global.NumClosedCurveInterpolationPoints;
            }
        }

        public override double LineWidth
        {
            get
            {
                return this.curve_verticies == null ? Global.DefaultClosedLineWidth : this.curve_verticies.ControlPoints.MinDistanceBetweenPoints();
            }
        }

        public override double ControlPointRadius
        {
            get
            {
                return Global.DefaultClosedLineWidth / 2.0; //Change possibly
            }
        }

        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }

        public RetraceAndReplacePathCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }

        PenInputHelper pen = new PenInputHelper();

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {

            //If current pen is not touching pad
            if (e.Button.Left() == false)
            {
                if (Verticies.Length >= 3)
                {
                    GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                    if (CanControlPointBePlaced(WorldPos))
                    {
                        PushVertex(WorldPos);
                        if (CanCommandComplete(WorldPos))
                        {

                            this.Execute();
                        }
                    }
                }
                //PenInputHelper.Clear();
                return;
            }

            //If pen is not touching pad, don't draw
            if (this.oldMouse?.Button.Left() == false)
            {
                return;
            }

            //Checks to make sure the polygon has a starting vertex
            if (this.Verticies.Length == 0)
            {
                return;
            }



            //Completes the polygon
            if (OverlapsFirstVertex(pen.cursor_position) && this.Verticies.Length > 2)
            {
                //OverlapsAnyVertex(pen.cursor_position);
                if (CanCommandComplete(pen.cursor_position))
                {
                    this.Execute();
                    return;
                }
            }

            // Console.WriteLine(distanceToLast.ToString());


            int placeVertex = pen.GetNextVertex(e, Parent, Verticies);
            if (CanControlPointBePlaced(pen.cursor_position))
            {
                if (placeVertex == -1)
                {
                    PopVertex();
                    PushVertex(pen.cursor_position);
                }
                else if (placeVertex == 1)
                {
                    PushVertex(pen.cursor_position);
                }
            }


            base.OnMouseMove(sender, e);
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
    }
}