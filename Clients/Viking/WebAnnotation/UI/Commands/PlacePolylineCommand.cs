using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Windows.Forms;
using WebAnnotation.View;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Handles callback, drawing, and vertex/color/width properties
    /// </summary>
    abstract class ControlPointCommandBase : Viking.UI.Commands.Command
    {
        public virtual double LineWidth
        {
            get;
        } 

        protected Microsoft.Xna.Framework.Color LineColor;

        public virtual double ControlPointRadius
        {
            get
            {
                return LineWidth / 2.0;
            }
        }

        public virtual LineStyle Style
        {
            get; 
        }

        public abstract GridVector2[] Verticies
        {
            get;
            protected set;
        }

        public delegate void OnCommandSuccess(GridVector2[] control_points);
        protected OnCommandSuccess success_callback;

        public ControlPointCommandBase(Viking.UI.Controls.SectionViewerControl parent, 
                                     Microsoft.Xna.Framework.Color color, 
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : base(parent)
        {
            this.LineColor = color;
            this.LineWidth = LineWidth;
            this.success_callback = success_callback;
        }

        public ControlPointCommandBase(Viking.UI.Controls.SectionViewerControl parent, 
                                     System.Drawing.Color color, 
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : this(parent, 
                  color.ToXNAColor(),
                   LineWidth,
                   success_callback)
        { 
        }

        /// <summary>
        /// Can a control point be placed at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected abstract bool CanControlPointBePlaced(GridVector2 WorldPos);

        /// <summary>
        /// Can a control point be placed at this position?
        /// </summary>
        /// <param name="WorldPos"></param>
        /// <returns></returns>
        protected abstract bool CanControlPointBeGrabbed(GridVector2 WorldPos);

        /// <summary>
        /// Can the command complete if the mouse is clicked at this position?
        /// </summary>
        /// <param name="WorldPosition"></param>
        /// <returns></returns>
        protected abstract bool CanCommandComplete(GridVector2 WorldPosition);

        protected override void Execute()
        {
            if (this.success_callback != null)
                this.success_callback(this.Verticies);

            base.Execute();
        }

        protected bool OverlapsFirstVertex(GridVector2 position)
        {
            return GridVector2.Distance(Verticies.First(), position) <= ControlPointRadius;
        }

        protected bool OverlapsLastVertex(GridVector2 position)
        {
            return GridVector2.Distance(Verticies.Last(), position) <= ControlPointRadius;
        }

        protected bool OverlapsAnyVertex(GridVector2 position)
        {
            return Verticies.Any(lv => GridVector2.Distance(lv, position) <= ControlPointRadius);
        }

        protected int?IndexOfOverlappedVertex(GridVector2 position)
        {
            for(int i = 0; i < this.Verticies.Count(); i++)
            {
                bool overlaps = GridVector2.Distance(this.Verticies[i], position) <= ControlPointRadius;
                if (overlaps)
                    return new int?(i);
            }

            return new int?();
        }

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        protected abstract GridVector2? IntersectsSelf(GridLineSegment lineSeg);
    }

    abstract class PolyLineCommandBase : ControlPointCommandBase
    {
        public PolyLineCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                     Microsoft.Xna.Framework.Color color,
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        { 
        }

        public PolyLineCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                     System.Drawing.Color color,
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
             : base(parent, color, LineWidth, success_callback) 
        {
        }

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        protected override GridVector2? IntersectsSelf(GridLineSegment lineSeg)
        {
            return this.Verticies.IntersectionPoint(lineSeg);
        }
    }

    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary>
    class PlacePolylineCommand : PolyLineCommandBase
    {
        private Stack<GridVector2> vert_stack = new Stack<GridVector2>();

        /// <summary>
        /// Returns the stack with the bottomost entry first in the array
        /// </summary>
        public override GridVector2[] Verticies
        {
            get { return vert_stack.ToArray().Reverse().ToArray(); }
            protected set {
                  vert_stack.Clear();
                  foreach(GridVector2 v in value)
                  {
                      vert_stack.Push(v);
                  }
            }
        }

        public PlacePolylineCommand(Viking.UI.Controls.SectionViewerControl parent,
                                     Microsoft.Xna.Framework.Color color,
                                     GridVector2 origin,
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            parent.Cursor = Cursors.Cross;
            vert_stack.Push(origin);
        }

        public PlacePolylineCommand(Viking.UI.Controls.SectionViewerControl parent, 
                                     System.Drawing.Color color,
                                     GridVector2 origin,  
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : this(parent,
                   new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    0.5f),
                   origin,
                   LineWidth, 
                   success_callback)
        { 
        }

        protected override bool CanControlPointBeGrabbed(GridVector2 WorldPos)
        {
            return OverlapsAnyVertex(WorldPos);
        }

        protected override bool CanCommandComplete(GridVector2 WorldPosition)
        {
            return OverlapsLastVertex(WorldPosition);
        }

        protected override bool CanControlPointBePlaced(GridVector2 WorldPosition)
        {
            return !OverlapsAnyVertex(WorldPosition);
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button.None())
            {
                Parent.Cursor = CanControlPointBeGrabbed(WorldPos) ? Cursors.Hand : Cursors.Cross;
            }
           else if (e.Button.Left())
            {
                if (CanControlPointBeGrabbed(WorldPos))
                {
                    //Drag the vertex under the cursor
                    int? iOverlapped = IndexOfOverlappedVertex(WorldPos);
                   
                    Viking.UI.Commands.Command.InjectCommand(new AdjustPolylineCommand(this.Parent,
                                                                                        this.LineColor,
                                                                                        this.Verticies,
                                                                                        this.LineWidth,
                                                                                        iOverlapped.Value,
                                                                                        false,
                                                                                        new OnCommandSuccess((line_verticies) =>
                                                                                            {
                                                                                                this.Verticies = line_verticies;
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

                if (CanCommandComplete(WorldPos))
                {
                    //If we click a point twice the command is completed.
                    this.Execute();
                    return;
                }
                else if (CanControlPointBePlaced(WorldPos))
                {
                    vert_stack.Push(WorldPos);
                    Parent.Invalidate();
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
            else if(e.Button == MouseButtons.Left)
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanControlPointBePlaced(WorldPos))
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
            if (this.oldWorldPosition != Verticies.Last())
            {
                GridVector2? SelfIntersection = IntersectsSelf(new GridLineSegment(this.oldWorldPosition, Verticies.Last()));

                vert_stack.Push(this.oldWorldPosition);

                CurveView curveView = new CurveView(vert_stack.ToArray(), this.LineColor, false, lineWidth: this.LineWidth);

                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.annotationOverlayEffect, 0, new CurveView[] { curveView });
                //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

                this.vert_stack.Pop();

                base.OnDraw(graphicsDevice, scene, basicEffect);
            }
            else
            {
                GlobalPrimitives.DrawPolyline(Parent.LumaOverlayLineManager, basicEffect, this.Verticies.ToList(), this.LineWidth, this.LineColor);
            }
        }
    }

    /// <summary>
    /// Hold Left button down and drag a vertex to move a vertex
    /// Release left button to place the vertex and exit the command
    /// </summary>
    class AdjustPolylineCommand : PolyLineCommandBase
    {
        int DraggedVertexIndex;

        GridVector2[] vert_list;

        public bool IsClosed;

        public override GridVector2[] Verticies
        {
            get { return vert_list; }
            protected set { vert_list = value; }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="color"></param>
        /// <param name="verticies"></param>
        /// <param name="LineWidth"></param>
        /// <param name="DraggedVertex">The vertex this command is modifying</param>
        /// <param name="success_callback"></param>
        public AdjustPolylineCommand(Viking.UI.Controls.SectionViewerControl parent,
                                     Microsoft.Xna.Framework.Color color,
                                     GridVector2[] verticies,
                                     double LineWidth,
                                     int DraggedVertex,
                                     bool IsClosed,
                                     OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            this.IsClosed = IsClosed;
            vert_list = verticies;
            this.DraggedVertexIndex = DraggedVertex;

            parent.Cursor = Cursors.Hand;
        }

        public AdjustPolylineCommand(Viking.UI.Controls.SectionViewerControl parent,
                                     System.Drawing.Color color,
                                     GridVector2[] verticies,
                                     double LineWidth,
                                     int DraggedVertex,
                                     bool IsClosed,
                                     OnCommandSuccess success_callback)
            : this(parent, 
                   new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    0.5f),
                   verticies, 
                   LineWidth,
                   DraggedVertex,
                   IsClosed,
                   success_callback)
        {
            Parent.Cursor = Cursors.Hand;
        }

        private bool OverlapsNonDraggedVertex(GridVector2 WorldPosition)
        {
            for (int i = 0; i < Verticies.Length; i++)
            {
                if (i == this.DraggedVertexIndex)
                    continue;

                if (GridVector2.Distance(WorldPosition, Verticies[i]) <= this.ControlPointRadius)
                    return true;
            }

            return false;
        }

        protected override bool CanCommandComplete(GridVector2 WorldPosition)
        {
            return !OverlapsNonDraggedVertex(WorldPosition);
        }

        protected override bool CanControlPointBePlaced(GridVector2 WorldPosition)
        {
            return !OverlapsNonDraggedVertex(WorldPosition);
        }

        protected override bool CanControlPointBeGrabbed(GridVector2 WorldPos)
        {
            throw new NotImplementedException();
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button.Left())
            {
                
                this.vert_list[this.DraggedVertexIndex] = WorldPos;
                Parent.Invalidate(); 
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button.Left())
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanCommandComplete(WorldPos))
                {
                    //If we release the left mouse button the command is completed                   
                    Verticies[this.DraggedVertexIndex] = WorldPos;
                    this.Execute(); 
                }
                return;
            }

            base.OnMouseUp(sender, e);
        }
         

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (Verticies.Length > 1)
            {

                CurveView curveView = new CurveView(Verticies, this.LineColor,
                                                    this.IsClosed, null, LineWidth,
                                                    ControlPointRadius, this.Style,
                                                    this.IsClosed ? Global.NumClosedCurveInterpolationPoints : Global.NumOpenCurveInterpolationPoints);

                CurveView.Draw(graphicsDevice, scene,
                               Parent.LumaOverlayCurveManager, basicEffect,
                               Parent.annotationOverlayEffect, (float)DateTime.UtcNow.Millisecond / 1000.0f,
                               new CurveView[] { curveView });
            }
            else
            {
                CircleView circleView = new CircleView(new GridCircle(Verticies[0], this.LineWidth / 2.0), this.LineColor);
                CircleView.Draw(graphicsDevice, scene, basicEffect, Parent.annotationOverlayEffect, new CircleView[] { circleView });
            }
           
            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }

}
