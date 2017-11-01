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
using System.Collections.ObjectModel;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    class PlaceClosedCurveCommand : PlaceCurveCommand
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
               return Global.DefaultClosedLineWidth / 2.0;
            }
        }


        public PlaceClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth, 
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, false, success_callback)
        {
        }

        public PlaceClosedCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
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
        protected override bool CanControlPointBePlaced(GridVector2 WorldPos)
        {
            return (!OverlapsAnyVertex(WorldPos) );
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
            catch(ArgumentException e)
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
                GridLineSegment[] proposed_back_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last() , worldPos));
                GridLineSegment[] proposed_front_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(worldPos, controlPoints[0]));
                GridLineSegment[] existing_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

                proposed_front_curve_segments = proposed_front_curve_segments.ShortenLastVertex();
                existing_curve_segments = existing_curve_segments.ShortenLastVertex();

                GridVector2[] intersections = proposed_front_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }

                intersections = proposed_back_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                if (intersections.Length > 0)
                {
                    retval = intersections.First();
                    return retval;
                }
            }

            return retval;
        } 
    }

    class PlaceOpenCurveCommand : PlaceCurveCommand
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

        public PlaceOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
                                        double LineWidth,
                                        OnCommandSuccess success_callback)
            : base(parent, color, origin, LineWidth, true, success_callback)
        {
        }

        public PlaceOpenCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        System.Drawing.Color color,
                                        GridVector2 origin,
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
        protected override bool CanControlPointBePlaced(GridVector2 WorldPos)
        {
            return !OverlapsAnyVertex(WorldPos) && !ProposedSegmentSelfIntersects(WorldPos);
        }
         
        /// <summary>
        /// Can the command be completed by clicking this point?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected override bool CanCommandComplete(GridVector2 WorldPos)
        {
            return OverlapsLastVertex(WorldPos) && this.NumVerticies >= 2 && !ProposedSegmentSelfIntersects(WorldPos);
        }

        protected override bool ShapeIsValid()
        {
            if (NumVerticies < 2 || curve_verticies == null)
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
                try
                {
                    CurveViewControlPoints curveVerticies = AppendControlPointToCurve(worldPos);
                    GridVector2[] controlPoints = Verticies;
                    GridLineSegment[] proposed_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints.Last(), worldPos));
                    GridLineSegment[] existing_curve_segments = GridLineSegment.SegmentsFromPoints(curveVerticies.CurvePointsBetweenControlPoints(controlPoints[0], controlPoints.Last()));

                    existing_curve_segments = existing_curve_segments.ShortenLastVertex();

                    GridVector2[] intersections = proposed_curve_segments.Select(pcs => existing_curve_segments.IntersectionPoint(pcs)).Where(p => p.HasValue).Select(p => p.Value).ToArray();
                    if (intersections.Length > 0)
                        retval = intersections.First();
                }
                
                catch(ArgumentException)
                {
                    return new GridVector2?();
                }                
            }

            return retval;
        }
    }

    

    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary> 
    abstract class PlaceCurveCommand : ControlPointCommandBase, Viking.Common.IHelpStrings, Viking.Common.IObservableHelpStrings
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

        protected Stack<GridVector2> vert_stack = new Stack<GridVector2>();

        protected void PushVertex(GridVector2 p)
        {
            vert_stack.Push(p);
            curve_verticies = new CurveViewControlPoints(Verticies, NumCurveInterpolations, !this.IsOpen);
        }

        protected GridVector2 PopVertex(GridVector2 p)
        {
            GridVector2 output = vert_stack.Pop();
            curve_verticies = new CurveViewControlPoints(Verticies, NumCurveInterpolations, !this.IsOpen);
            return output;
        }

        

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
            get { return vert_stack.ToArray().Reverse().ToArray(); }
            protected set
            {
                vert_stack.Clear();
                foreach (GridVector2 v in value)
                {
                    vert_stack.Push(v);
                }
            }
        }

        public int NumVerticies
        {
           get { return this.vert_stack.Count; }
        }

        

        public PlaceCurveCommand(Viking.UI.Controls.SectionViewerControl parent,
                                        Microsoft.Xna.Framework.Color color,
                                        GridVector2 origin,
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


        protected CurveViewControlPoints AppendControlPointToCurve(GridVector2 worldPos)
        {
            List<GridVector2> listControlPoints = new List<GridVector2>(this.Verticies);
            listControlPoints.Add(worldPos);
            return new CurveViewControlPoints(listControlPoints, this.NumCurveInterpolations, !IsOpen);
        } 

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected bool ProposedSegmentSelfIntersects(GridVector2 worldPos)
        {
            GridVector2? intersection = ProposedControlPointSelfIntersection(worldPos);
            return intersection.HasValue;
        }
         
        protected override GridVector2? IntersectsSelf(GridLineSegment lineSeg)
        {
            return this.curve_verticies.CurvePoints.IntersectionPoint(lineSeg);
        }


        protected override bool CanControlPointBeGrabbed(GridVector2 WorldPos)
        {
            return OverlapsAnyVertex(WorldPos);
        }

        
        

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

        protected override void OnMouseDown(object sender, MouseEventArgs e)
        {
            if(e.Button.Left())
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanCommandComplete(WorldPos))
                {
                    this.Execute();
                    return;
                }
                else if(CanControlPointBePlaced(WorldPos))
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
                        Viking.UI.Commands.Command.InjectCommand(new AdjustPolylineCommand(this.Parent,
                                                                                            this.LineColor,
                                                                                            this.Verticies,
                                                                                            this.LineWidth,
                                                                                            iOverlapped.Value,
                                                                                            !this.IsOpen,
                                                                                            new OnCommandSuccess((line_verticies) =>
                                                                                            {
                                                                                                this.Verticies = line_verticies;
                                                                                            //Update oldWorldPosition to keep the line we draw to our cursor from jumping on the first draw when we are reactivated and user hasn't used the mouse yet
                                                                                            this.oldWorldPosition = line_verticies[iOverlapped.Value];
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

        protected abstract GridVector2? ProposedControlPointSelfIntersection(GridVector2 worldPos);

        protected abstract bool ShapeIsValid();

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            GridVector2? SelfIntersection = ProposedControlPointSelfIntersection(this.oldWorldPosition);

            if(SelfIntersection.HasValue || CanControlPointBePlaced(this.oldWorldPosition))
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
                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, new CurveView[] { curveView } );
                //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

                if(pushed_point)
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
        }
    }
}
