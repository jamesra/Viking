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
using System.Collections.Specialized;

namespace WebAnnotation.UI.Commands
{
    /*
    class PlaceInteriorHoleWithPenCommand : PlaceClosedCurveWithPenCommand
    {
        public GridPolygon ExistingShape;

        public PlaceInteriorHoleWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }

        public PlaceInteriorHoleWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }

        protected override bool ShapeIsValid()
        {
            bool baseValid = base.ShapeIsValid();
            if (false == baseValid)
                return false;


        }
    }
    */

    class PlaceClosedCurveWithPenCommand : PlaceCurveWithPenCommand
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

        public float PointIntervalOnDrag
        {
            get
            {
                return 90;
            }
        }

        public float PenAngleThreshold
        {
            get
            {
                return .3f;
            }
        }

        public PlaceClosedCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {

        }

        public PlaceClosedCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }




        protected override void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //No Intersections are possible
            if (PenInput.Path.Count <= 2)
                return;

            //Update the curve
            this.curve_verticies = new CurveViewControlPoints(PenInput.Path, NumInterpolations: 0, TryToClose: false);

            //Find a possible intersection point
            GridVector2? IntersectionPoint = PenInput.Segments.IntersectionPoint(PenInput.LastSegment, true, out GridLineSegment? IntersectedSegment);

            

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

                PenInput.SimplifiedPath = cropped_path.DouglasPeuckerReduction(Global.PenSimplifyThreshold);
                //PenInput.Push(IntersectionPoint.Value);3

                //if (this.ShapeIsValid())
                    this.Execute(PenInput.SimplifiedPath.ToArray());

            }


            this.Parent.Invalidate();
        }


        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {
            
        }


        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        { 
            if (Verticies.Length >= 3)
            {
                //this.Execute();
            }
            return;
            
            /*
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
            */

            //Completes the polygon
            /*
            if (OverlapsFirstVertex(PenInput.Peek()) && this.PenInput.Path.Count > 2)
            {
                if (CanCommandComplete(PenInput.Peek()))
                {
                    this.Execute();
                    return;
                }
            }*/

            // Console.WriteLine(distanceToLast.ToString());

            
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

    class PlaceOpenCurveWithPenCommand : PlaceCurveWithPenCommand
    {
        public override LineStyle Style
        {
            get
            {
                return LineStyle.Tubular;
            }
        }

        public override uint NumCurveInterpolations
        {
            get
            {
                return Global.NumOpenCurveInterpolationPoints;
            }
        }

        public PlaceOpenCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, true, success_callback)
        {
        }

        public PlaceOpenCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, true, success_callback)
        {
        }
        
        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            PenInput.Path.Clear();
            PenInput.Push(Parent.ScreenToWorld(e.X, e.Y));
            base.OnMouseDown(sender, e);
        }

        protected override void OnPenPathChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if(PenInput.Path.Count < 2)
            {
                return;
            }

            //Update the Curve
            this.curve_verticies = new CurveViewControlPoints(Verticies, NumInterpolations: 0, TryToClose: false);

            this.Parent.Invalidate();
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if(PenInput.Path.Count < 2)
            {
                return;
            }
            //Simplify the curve
            PenInput.Path = PenInput.Path.DouglasPeuckerReduction(Global.PenSimplifyThreshold);
            Execute();
            base.OnMouseUp(sender, e);
        }

        protected override void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment)
        {
            
        }

        protected override void OnPenPathComplete(object sender, GridVector2[] Path)
        {
        }


        /// <summary>
        /// Can a control point be placed or the command completed by clicking the mouse at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanControlPointBePlaced(GridVector2 WorldPos)
        {
            return !OverlapsAnyVertex(WorldPos);// && !ProposedSegmentSelfIntersects(WorldPos);
        }

        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete(GridVector2 WorldPos)
        {
            //return OverlapsLastVertex(WorldPos) && this.PenInput.Path.Count >= 2 && !ProposedSegmentSelfIntersects(WorldPos);
            return true;
        }
        

        protected override bool ShapeIsValid()
        {
            if (this.PenInput.Path.Count < 2 || curve_verticies == null)
                return false;

            return this.curve_verticies.ControlPoints.ToPolyLine().STIsValid().IsTrue;
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected static bool ProposedSegmentSelfIntersects(CurveViewControlPoints curve_verticies, GridLineSegment newSegment)
        {
            if (curve_verticies.ControlPoints.Length < 4)
                return false;

            GridLineSegment[] existingSegments = GridLineSegment.SegmentsFromPoints(
                    curve_verticies.CurvePoints.Where((p, i) => i < curve_verticies.CurvePoints.Length - 2)
                    .ToArray());

            return newSegment.Intersects(existingSegments);
        }

        ///// <summary>
        ///// Return true if a line to the world position from the last vertex will intersect our curve
        ///// </summary>
        ///// <param name="worldPos"></param>
        ///// <returns></returns>
        //protected override GridVector2? ProposedControlPointSelfIntersection(GridVector2 worldPos)
        //{
        //    GridVector2? retval = new GridVector2?();

        //    if (this.PenInput.Path.Count < 3)
        //        return retval;

        //    if (worldPos != Peek())
        //    {
        //        try
        //        {
        //            CurveViewControlPoints curveVerticies = AppendControlPointToCurve(worldPos);
        //            GridVector2[] controlPoints = Verticies;
        //            GridLineSegment[] proposed_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), worldPos));
        //            GridLineSegment[] existing_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

        //            existing_curve_segments = existing_curve_segments.ShortenLastVertex();

        //            GridVector2[] intersections = proposed_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs, false)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
        //            if (intersections.Length > 0)
        //                retval = intersections.First();
        //        }

        //        catch (ArgumentException)
        //        {
        //            return new GridVector2?();
        //        }
        //    }

        //    return retval;
        //}
    }



    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary> 
    abstract class PlaceCurveWithPenCommand : ControlPointCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
    {
        public abstract uint NumCurveInterpolations
        {
            get;
        }

        public ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new ObservableCollection<string>(this.HelpStrings);
            }
        }


        public string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>();

                s.AddRange(PlaceCurveCommand.DefaultMouseHelpStrings);
                s.AddRange(PlaceCurveCommand.DefaultKeyHelpStrings);

                return s.ToArray();
            }

        }


        public new static string[] DefaultMouseHelpStrings = new String[] {
            "Double Left Click: Place final control point, save and exit command",
            "Double Right Click: Pop last control point",
            "Left Click and Drag Control Point: Move existing control point",
            "Left Click last control point: Save and exit command",
            "No cursor: Command cannot be completed at this location due to invalid geometry. Typically crossed lines."
            };

        public new static string[] DefaultKeyHelpStrings = new String[] {
            "Escape Key: Cancel command",
            "Page up/down key: Change Magnification",
            "Arrow key: Move view",
            "Home key: Round magnification to whole number"
            };

        //protected List<GridVector2> vert_stack = new List<GridVector2>();
        protected PenInputHelper PenInput;

        /*
        protected void PushVertex(GridVector2 p)
        {
            Push(p);
            curve_verticies = new CurveViewControlPoints(Verticies, NumCurveInterpolations, !this.IsOpen);
        }

        protected GridVector2 PopVertex()
        {
            GridVector2 output = Pop();
            curve_verticies = new CurveViewControlPoints(Verticies, NumCurveInterpolations, !this.IsOpen);
            return output;
        }
        */


        /// <summary>
        /// Verticies placed along the curve
        /// </summary>
        protected CurveViewControlPoints curve_verticies;

        bool IsOpen = true; //False if the curves last point is connected to its first
                            /// <summary>
                            /// Returns the stack with the bottomost entry first in the array
                            /// </summary>

        public override GridVector2[] Verticies
        {
            get { return PenInput.Path.ToArray(); }
            protected set
            {
                throw new NotImplementedException("PenInput helper should be handling set");
            }
        }
        

        //public int NumVerticies
        //{
        //    get { return this.vert_stack.Count; }
        //}



        public PlaceCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        bool IsOpen,
                                        OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            parent.Cursor = Cursors.Cross;
            PenInput = new PenInputHelper(parent);
            PenInput.Push(origin);
            //Ensure any pen subscriptions are released in the OnDeactivate call
            System.Diagnostics.Trace.WriteLine(string.Format("PlaceCurveWithPenCommand {0} Subscribed to events", this.ID));
            PenInput.OnPathChanged += this.OnPenPathChanged;
            PenInput.OnPathCompleted += this.OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged += this.OnPenProposedNextSegmentChanged;
            this.success_callback = success_callback;
            this.IsOpen = IsOpen;
        }

        public PlaceCurveWithPenCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
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
        
        protected override void OnDeactivate()
        {
            System.Diagnostics.Trace.WriteLine(string.Format("PlaceCurveWithPenCommand {0} Unubscribed to events", this.ID));
            PenInput.OnPathChanged -= this.OnPenPathChanged;
            PenInput.OnPathCompleted -= this.OnPenPathComplete;
            PenInput.OnProposedNextSegmentChanged -= this.OnPenProposedNextSegmentChanged;
            this.PenInput.UnsubscribeEvents();
            this.PenInput = null;
            base.OnDeactivate();
        }

        /*
        public void Push(GridVector2 p)
        {
            vert_stack.Insert(0, p);
            PenInput.Path.Insert(0, p);
        }

        public GridVector2 Pop()
        {
            GridVector2 p = vert_stack.First();
            vert_stack.RemoveAt(0);
            PenInput.Path.RemoveAt(0);
            return p;
        }

        public GridVector2 Peek()
        {
            return vert_stack.First();
        }
        
        protected CurveViewControlPoints AppendControlPointToCurve(GridVector2 worldPos)
        {
            List<GridVector2> listControlPoints = new List<GridVector2>(this.Verticies);
            listControlPoints.Add(worldPos);
            return new CurveViewControlPoints(listControlPoints, this.NumCurveInterpolations, !IsOpen);
        }
        */
        ///// <summary>
        ///// Return true if a line to the world position from the last vertex will intersect our curve
        ///// </summary>
        ///// <param name="worldPos"></param>
        ///// <returns></returns>
        //protected bool ProposedSegmentSelfIntersects(GridVector2 worldPos)
        //{
        //    GridVector2? intersection = ProposedControlPointSelfIntersection(worldPos);
        //    return intersection.HasValue;
        //}

        protected override GridVector2? IntersectsSelf(GridLineSegment lineSeg)
        {
            return this.curve_verticies.CurvePoints.IntersectionPoint(lineSeg);
        }


        protected override bool CanControlPointBeGrabbed(GridVector2 WorldPos)
        {
            return OverlapsAnyVertex(WorldPos);
        }

        abstract protected void OnPenPathChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e);

        abstract protected void OnPenPathComplete(object sender, GridVector2[] Path);

        abstract protected void OnPenProposedNextSegmentChanged(object sender, GridLineSegment? segment);

        /*
        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button == MouseButtons.None)
            {
                Parent.Cursor = CanControlPointBePlaced(WorldPos) ? Cursors.Hand : Cursors.No;

                Parent.Cursor = CanCommandComplete(WorldPos) ? Cursors.Arrow : Parent.Cursor;
            }

            base.OnMouseMove(sender, e);
        }
        */
        /*
        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button.Right())
            {
                if (vert_stack.Count > 1)
                {
                    Pop();
                    Parent.Invalidate();
                    return;
                }
            }
            else if (e.Button.Left())
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanControlPointBePlaced(WorldPos))
                {
                    PushVertex(WorldPos);
                    if (CanCommandComplete(WorldPos))
                    {
                        this.Execute();
                        return;
                    }
                }
            }

            base.OnMouseDown(sender, e);
        }
        */

        //protected abstract GridVector2? ProposedControlPointSelfIntersection(GridVector2 worldPos);

        protected abstract bool ShapeIsValid();

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (PenInput.Path.Count > 1)
            {
                CurveView curveView = new CurveView(PenInput.Path, this.LineColor, false, numInterpolations: 0, lineWidth: this.LineWidth, lineStyle: LineStyle.Standard, controlPointRadius: null);
                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView });

                if (PenInput.ProposedNextSegment.HasValue)
                {
                    LineView unofficialPath = new LineView(PenInput.ProposedNextSegment.Value, width: this.LineWidth, color: this.LineColor, lineStyle: LineStyle.Standard, UseHSLColor: true);
                    LineView.Draw(graphicsDevice, scene, Parent.LumaOverlayLineManager, new LineView[] { unofficialPath });
                }
            }
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
