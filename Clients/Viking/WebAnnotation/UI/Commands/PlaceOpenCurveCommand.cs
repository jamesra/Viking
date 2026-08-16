using Geometry;
using SqlGeometryUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    internal class PlaceClosedCurveCommand : PlaceCurveCommand
    {
        public override LineStyle Style => LineStyle.HalfTube;

        public override uint NumCurveInterpolations => Global.NumClosedCurveInterpolationPoints;

        public override double LineWidth => curve_verticies is null ? Global.DefaultClosedLineWidth : curve_verticies.ControlPoints.MinDistanceBetweenAnyPoints();

        public override double ControlPointRadius => Global.DefaultClosedLineWidth / 2.0;

        public float PointIntervalOnDrag => 100;

        public PlaceClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        Vector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }

        public PlaceClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        Vector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }



        /// <summary>
        /// Can a control point be placed or the command completed by clicking the mouse at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanControlPointBePlaced(Vector2 WorldPos) => (!OverlapsAnyVertex(WorldPos));

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete(Vector2 WorldPos) => (OverlapsLastVertex(WorldPos) || OverlapsFirstVertex(WorldPos)) && ShapeIsValid();

        protected override bool ShapeIsValid()
        {
            if (Vertices.Length < 3 || curve_verticies is null || curve_verticies.ControlPoints.Length < 3)
            {
                return false;
            }

            try
            {
                return curve_verticies.ControlPoints.ToPolygon().STIsValid().IsTrue;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected override Vector2? ProposedControlPointSelfIntersection(Vector2 worldPos)
        {
            Vector2? retval = new Vector2?();

            if (NumVerticies < 3)
            {
                return retval;
            }

            if (worldPos != vert_stack.Peek())
            {
                CurveViewControlPoints curveVerticies = AppendControlPointToCurve(worldPos);
                Vector2[] controlPoints = Vertices;
                LineSegment[] proposed_back_curve_segments = LineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), worldPos));
                LineSegment[] proposed_front_curve_segments = LineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(worldPos, controlPoints[0]));
                LineSegment[] existing_curve_segments = LineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

                proposed_front_curve_segments = proposed_front_curve_segments.ShortenLastVertex();
                existing_curve_segments = existing_curve_segments.ShortenLastVertex();

                Vector2[] intersections = [.. proposed_front_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value)];
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }

                intersections = [.. proposed_back_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value)];
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }
            }

            return retval;
        }
    }

    internal class PlaceOpenCurveCommand : PlaceCurveCommand
    {
        public override LineStyle Style => LineStyle.Tubular;

        public override uint NumCurveInterpolations => Geometry.Global.NumOpenCurveInterpolationPoints;

        public PlaceOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        Vector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, true, success_callback)
        {
        }

        public PlaceOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        Vector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, true, success_callback)
        {
        }

        /// <summary>
        /// Can a control point be placed or the command completed by clicking the mouse at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanControlPointBePlaced(Vector2 WorldPos) => !OverlapsAnyVertex(WorldPos) && !ProposedSegmentSelfIntersects(WorldPos);

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete(Vector2 WorldPos) => OverlapsLastVertex(WorldPos) && NumVerticies >= 2 && !ProposedSegmentSelfIntersects(WorldPos);

        protected override bool ShapeIsValid()
        {
            if (NumVerticies < 2 || curve_verticies is null)
            {
                return false;
            }

            return curve_verticies.ControlPoints.ToSqlGeometry().STIsValid().IsTrue;
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected static bool ProposedSegmentSelfIntersects(CurveViewControlPoints curve_verticies, LineSegment newSegment)
        {
            if (curve_verticies.ControlPoints.Length < 4)
            {
                return false;
            }

            LineSegment[] existingSegments = LineSegment.SegmentsFromPoints(
                    [.. curve_verticies.CurvePoints.Where((p, i) => i < curve_verticies.CurvePoints.Length - 2)]);

            return newSegment.Intersects(existingSegments);
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected override Vector2? ProposedControlPointSelfIntersection(Vector2 worldPos)
        {
            Vector2? retval = new Vector2?();

            if (NumVerticies < 3)
            {
                return retval;
            }

            if (worldPos != vert_stack.Peek())
            {
                try
                {
                    CurveViewControlPoints curveVerticies = AppendControlPointToCurve(worldPos);
                    Vector2[] controlPoints = Vertices;
                    LineSegment[] proposed_curve_segments = LineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), worldPos));
                    LineSegment[] existing_curve_segments = LineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

                    existing_curve_segments = existing_curve_segments.ShortenLastVertex();

                    Vector2[] intersections = [.. proposed_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value)];
                    if (intersections.Length > 0)
                    {
                        retval = intersections.First();
                    }
                }

                catch (ArgumentException)
                {
                    return new Vector2?();
                }
            }

            return retval;
        }
    }



    /// <summary>
    /// This is the base class for building geometry by tracing a shape on the screen with a pen or mouse.
    /// 
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary> 
    internal abstract class PlaceCurveCommand : ControlPointCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        public abstract uint NumCurveInterpolations
        {
            get;
        }

        public ObservableCollection<string> ObservableHelpStrings => new(HelpStrings);


        public string[] HelpStrings
        {
            get
            {
                List<string> s = [.. PlaceCurveCommand.DefaultMouseHelpStrings, .. PlaceCurveCommand.DefaultKeyHelpStrings];

                return [.. s];
            }

        }


        public new static string[] DefaultMouseHelpStrings = [
            "Double Left Click: Place final control point, save and exit command",
            "Double Right Click: Pop last control point",
            "Left Click and Drag Control Point: Move existing control point",
            "Left Click last control point: Save and exit command",
            "No cursor: Command cannot be completed at this location due to invalid geometry. Typically crossed lines."
            ];

        public new static string[] DefaultKeyHelpStrings = [
            "Escape Key: Cancel command",
            "Page up/down key: Change Magnification",
            "Arrow key: Move view",
            "Home key: Round magnification to whole number"
            ];

        protected Stack<Vector2> vert_stack = new();

        protected void PushVertex(Vector2 p)
        {
            vert_stack.Push(p);
            curve_verticies = vert_stack.Count > 2 || IsOpen ? new CurveViewControlPoints(Vertices, NumCurveInterpolations, !IsOpen) : null;
        }

        protected Vector2 PopVertex()
        {
            Vector2 output = vert_stack.Pop();
            curve_verticies = vert_stack.Count > 2 || IsOpen ? new CurveViewControlPoints(Vertices, NumCurveInterpolations, !IsOpen) : null;
            return output;
        }



        /// <summary>
        /// Vertices placed along the curve
        /// </summary>
        protected CurveViewControlPoints curve_verticies;
        private readonly bool IsOpen = true; //False if the curves last point is connected to its first
        /// <summary>
        /// Returns the stack with the bottomost entry first in the array
        /// </summary>
        public override Vector2[] Vertices
        {
            get => [.. ((IEnumerable<Vector2>)[.. vert_stack]).Reverse()];
            protected set
            {
                vert_stack.Clear();
                foreach (Vector2 v in value)
                {
                    vert_stack.Push(v);
                }
            }
        }

        public int NumVerticies => vert_stack.Count;



        public PlaceCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        Vector2 origin,
                                        double LineWidth,
                                        bool IsOpen,
                                        OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            parent.Cursor = Cursors.Cross;
            vert_stack.Push(origin);
            this.success_callback = success_callback;
            this.IsOpen = IsOpen;
        }

        public PlaceCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        Vector2 origin,
                                        double LineWidth,
                                        bool IsOpen,
                                        OnCommandSuccess success_callback)
            : this(parent,
                    color.ToXNAColor(),
                    origin,
                    LineWidth,
                    IsOpen,
                    success_callback)
        {
        }


        protected CurveViewControlPoints AppendControlPointToCurve(Vector2 worldPos)
        {
            List<Vector2> listControlPoints = [.. Vertices, worldPos];
            return new CurveViewControlPoints(listControlPoints, NumCurveInterpolations, !IsOpen);
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected bool ProposedSegmentSelfIntersects(Vector2 worldPos)
        {
            Vector2? intersection = ProposedControlPointSelfIntersection(worldPos);
            return intersection.HasValue;
        }

        protected override Vector2? IntersectsSelf(LineSegment lineSeg) => curve_verticies.CurvePoints.IntersectionPoint(lineSeg);


        protected override bool CanControlPointBeGrabbed(Vector2 WorldPos) => OverlapsAnyVertex(WorldPos);




        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            Vector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button == MouseButtons.None)
            {
                Parent.Cursor = CanControlPointBePlaced(WorldPos) ? Cursors.Hand : Cursors.No;

                Parent.Cursor = CanCommandComplete(WorldPos) ? Cursors.Arrow : Parent.Cursor;
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button.Left())
            {
                Vector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanCommandComplete(WorldPos))
                {
                    Execute();
                    return;
                }
                else if (CanControlPointBePlaced(WorldPos))
                {
                    PushVertex(WorldPos);
                    Parent.Invalidate();
                    return;
                }
                else if (OverlapsAnyVertex(WorldPos))
                {
                    //Drag the vertex under the cursor
                    int? iOverlapped = IndexOfOverlappedVertex(WorldPos);
                    if (iOverlapped.HasValue)
                    {
                        Parent.CommandQueue.InjectCommand(new AdjustPolylineCommand(Parent,
                                                                                            LineColor,
                                                                                            Vertices,
                                                                                            LineWidth,
                                                                                            iOverlapped.Value,
                                                                                            !IsOpen,
                                                                                            new OnCommandSuccess((command, line_verticies) =>
                                                                                            {
                                                                                                Vertices = line_verticies;
                                                                                                //Update oldWorldPosition to keep the line we draw to our cursor from jumping on the first draw when we are reactivated and user hasn't used the mouse yet
                                                                                                oldWorldPosition = line_verticies[iOverlapped.Value];
                                                                                            })));
                        return;
                    }
                }
            }
            base.OnMouseDown(sender, e);
        }

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button.Right())
            {
                if (vert_stack.Count > 1)
                {
                    vert_stack.Pop();
                    Parent.Invalidate();
                    return;
                }
            }
            else if (e.Button.Left())
            {
                Vector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanControlPointBePlaced(WorldPos))
                {
                    PushVertex(WorldPos);
                    if (CanCommandComplete(WorldPos))
                    {
                        Execute();
                        return;
                    }
                }
            }

            base.OnMouseDown(sender, e);
        }

        protected abstract Vector2? ProposedControlPointSelfIntersection(Vector2 worldPos);

        protected abstract bool ShapeIsValid();

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            Vector2? SelfIntersection = ProposedControlPointSelfIntersection(oldWorldPosition);

            if ((SelfIntersection.HasValue || CanControlPointBePlaced(oldWorldPosition)))
            {
                bool pushed_point = true;

                if (SelfIntersection.HasValue)
                {
                    vert_stack.Push(SelfIntersection.Value);
                }
                else if (!OverlapsLastVertex(oldWorldPosition))
                {
                    vert_stack.Push(oldWorldPosition);
                }
                else
                {
                    pushed_point = false;
                }

                if (vert_stack.Count > 2)
                {
                    CurveView curveView = new([.. vert_stack], LineColor, !IsOpen, Global.NumCurveInterpolationPoints(!IsOpen), lineWidth: LineWidth, lineStyle: Style, controlPointRadius: ControlPointRadius);
                    curveView.Color.SetAlpha(ShapeIsValid() ? 1 : 0.25f);
                    CurveView.Draw(graphicsDevice, scene, OverlayStyle.Luma, 0, [curveView]);
                    //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);
                }
                else
                {
                    LineView lineView = new(new LineSegment(vert_stack.First(), vert_stack.Last()), Global.NumCurveInterpolationPoints(!IsOpen), LineColor, lineStyle: Style);
                    lineView.Color.SetAlpha(ShapeIsValid() ? 1 : 0.25f);
                    LineView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, [lineView]);
                }

                if (pushed_point)
                {
                    vert_stack.Pop();
                }

                base.OnDraw(graphicsDevice, scene, basicEffect);
            }
            else
            {
                if (Vertices.Length > 2)
                {
                    CurveView curveView = new([.. Vertices], LineColor, !IsOpen, Global.NumCurveInterpolationPoints(!IsOpen), lineWidth: LineWidth, lineStyle: Style, controlPointRadius: ControlPointRadius);
                    curveView.Color.SetAlpha(ShapeIsValid() ? 1 : 0.25f);
                    CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, [curveView]);
                }
                if (Vertices.Length == 2)
                {
                    LineView lineView = new(new LineSegment(vert_stack.First(), vert_stack.Last()), Global.NumCurveInterpolationPoints(!IsOpen), LineColor, lineStyle: Style);
                    lineView.Color.SetAlpha(ShapeIsValid() ? 1 : 0.25f);
                    LineView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, [lineView]);
                }
                else
                {
                    CircleView view = new(new Circle(Vertices.First(), LineWidth / 2.0), LineColor);
                    CircleView.Draw(graphicsDevice, scene, OverlayStyle.Luma, new CircleView[] { view });
                }
            }
        }
    }
}
