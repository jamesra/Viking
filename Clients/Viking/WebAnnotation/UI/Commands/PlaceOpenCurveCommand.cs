using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using System.Windows.Forms;
using WebAnnotation.View;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary> 
    class PlaceCurveCommand : PolylineCommandBase
    {
        Stack<GridVector2> vert_stack = new Stack<GridVector2>();

        bool IsOpen = true; //False if the curves last point is connected to its first
        /// <summary>
        /// Returns the stack with the bottomost entry first in the array
        /// </summary>
        public override GridVector2[] LineVerticies
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

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected bool ProposedSegmentSelfIntersects(GridVector2 worldPos)
        {
            if (NumVerticies < 3)
                return false;

            GridLineSegment newSegment = new GridLineSegment(worldPos, vert_stack.Peek());
            GridLineSegment[] existingSegments = GridLineSegment.SegmentsFromPoints(this.LineVerticies.TakeWhile((p, i) => i < NumVerticies - 1).ToArray());
            foreach(GridLineSegment existingSegment in existingSegments)
            {
                GridVector2 intersection;
                if(newSegment.Intersects(existingSegment, out intersection))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Return true if a line to the world position from the last vertex will intersect our curve
        /// </summary>
        /// <param name="worldPos"></param>
        /// <returns></returns>
        protected GridVector2? ProposedSegmentSelfIntersection(GridVector2 worldPos)
        {
            GridVector2? retval = new GridVector2?();

            if (NumVerticies < 3)
                return retval;

            GridLineSegment newSegment = new GridLineSegment(worldPos, vert_stack.Peek());
            GridLineSegment[] existingSegments = GridLineSegment.SegmentsFromPoints(this.LineVerticies.TakeWhile((p, i) => i < NumVerticies - 1).ToArray());
            foreach (GridLineSegment existingSegment in existingSegments)
            {
                GridVector2 intersection;
                if (newSegment.Intersects(existingSegment, out intersection))
                {
                    retval = intersection;
                    return intersection;
                }
            }

            return retval;
        }


        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button == MouseButtons.None)
            {
                if (OverlapsAnyVertex(WorldPos))
                {
                    if (OverlapsLastVertex(WorldPos))
                    {
                        if (this.NumVerticies < 2)
                            Parent.Cursor = Cursors.No;   //Not allowed to create single vertex line/curve curve
                        else
                            Parent.Cursor = Cursors.Hand; //Completion cursor
                    }
                    else
                    {
                        Parent.Cursor = Cursors.No;
                    }
                }
                else
                {
                    Parent.Cursor = ProposedSegmentSelfIntersects(WorldPos) ? Cursors.No : Cursors.Cross;
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                //Drag the vertex under the cursor
                int? iOverlapped = IndexOfOverlappedVertex(WorldPos);
                if (iOverlapped.HasValue)
                {
                    Viking.UI.Commands.Command.InjectCommand(new AdjustPolylineCommand(this.Parent,
                                                                                        this.LineColor,
                                                                                        this.LineVerticies,
                                                                                        this.LineWidth,
                                                                                        iOverlapped.Value,
                                                                                        new OnCommandSuccess((line_verticies) =>
                                                                                        {
                                                                                            this.LineVerticies = line_verticies;
                                                                                            //Update oldWorldPosition to keep the line we draw to our cursor from jumping on the first draw when we are reactivated and user hasn't used the mouse yet
                                                                                            this.oldWorldPosition = line_verticies[iOverlapped.Value];
                                                                                        })));
                    return;
                }
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //    TimeSpan Elapsed = new TimeSpan(DateTime.Now.Ticks - CreationTime.Ticks);
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                if (OverlapsLastVertex(WorldPos))
                {
                    //If we click a point twice the command is completed.
                    this.Execute();
                    return;
                }
                else
                {
                    if (!ProposedSegmentSelfIntersects(WorldPos))
                    {
                        vert_stack.Push(WorldPos);
                        Parent.Invalidate();
                    }
                }
            }

            base.OnMouseDown(sender, e);
        }

        protected override void OnMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (vert_stack.Count > 1)
                {
                    vert_stack.Pop();
                    Parent.Invalidate();
                    return;
                }
            }
            else if (e.Button == MouseButtons.Left)
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (!OverlapsLastVertex(WorldPos))
                {
                    vert_stack.Push(WorldPos);
                    this.Execute();
                    return;
                }
            }

            base.OnMouseDown(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {  
            if (!this.OverlapsLastVertex(this.oldWorldPosition))
            {
                GridVector2? SelfIntersection = ProposedSegmentSelfIntersection(this.oldWorldPosition);

                if (SelfIntersection.HasValue)
                    vert_stack.Push(SelfIntersection.Value);
                else
                    vert_stack.Push(this.oldWorldPosition);

                CurveView curveView = new CurveView(vert_stack.ToArray(), this.LineColor, this.IsOpen, lineWidth: this.LineWidth);

                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.annotationOverlayEffect, 0, new CurveView[] { curveView } );
                //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

                this.vert_stack.Pop();

                base.OnDraw(graphicsDevice, scene, basicEffect);
            }
            else
            {
                if (this.LineVerticies.Length > 1)
                {
                    CurveView curveView = new CurveView(this.LineVerticies.ToArray(), this.LineColor, this.IsOpen, lineWidth: this.LineWidth);
                    CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.annotationOverlayEffect, 0, new CurveView[] { curveView });
                }
                else
                {
                    CircleView view = new CircleView(new GridCircle(this.LineVerticies.First(), this.LineWidth / 2.0), this.LineColor);
                    CircleView.Draw(graphicsDevice, scene, basicEffect, this.Parent.annotationOverlayEffect, new CircleView[] { view });
                }

            }

            
        }
    }
}
