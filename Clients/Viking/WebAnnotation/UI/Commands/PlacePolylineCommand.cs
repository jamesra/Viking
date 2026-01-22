using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using VikingXNAGraphics;
using VikingXNAWinForms;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Base class for commands that have the user draw a line to annotate
    /// </summary>
    internal abstract class LineGeometryCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                 Microsoft.Xna.Framework.Color color,
                                 double LineWidth,
LineGeometryCommandBase.OnCommandSuccess success_callback) : Viking.UI.Commands.Command(parent)
    {
        public virtual double LineWidth
        {
            get;
        } = LineWidth;

        protected Microsoft.Xna.Framework.Color LineColor = color;

        public virtual LineStyle Style
        {
            get;
        }

        /// <summary>
        /// The color passed to our constructor, used to restore graphics color in case we change colors for an invalid state.
        /// </summary>
        protected Microsoft.Xna.Framework.Color OriginalColor = color;

        public delegate void OnCommandSuccess(object sender, GridVector2[] control_points);
        protected OnCommandSuccess success_callback = success_callback;

        public LineGeometryCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                     System.Drawing.Color color,
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : this(parent,
                  color.ToXNAColor(),
                   LineWidth,
                   success_callback)
        {
        }


        protected virtual void Execute(GridVector2[] updated_verticies)
        {
            if (success_callback != null)
            {
                success_callback(this, updated_verticies);
            }

            base.Execute();
        }
    }

    /// <summary>
    /// Handles callback, drawing, and vertex/color/width properties.
    /// This is the base class for building geometry using manually placed control points
    /// </summary>
    internal abstract class ControlPointCommandBase : LineGeometryCommandBase
    {
        public virtual double ControlPointRadius => LineWidth / 2.0;

        public abstract GridVector2[] Verticies
        {
            get;
            protected set;
        }

        public ControlPointCommandBase(Viking.UI.Controls.SectionViewerControl parent,
                                     Microsoft.Xna.Framework.Color color,
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
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


        protected bool OverlapsFirstVertex(GridVector2 position) => GridVector2.Distance(Verticies.First(), position) <= ControlPointRadius;

        protected bool OverlapsLastVertex(GridVector2 position) => GridVector2.Distance(Verticies.Last(), position) <= ControlPointRadius;

        protected bool OverlapsAnyVertex(GridVector2 position) => Verticies.Any(lv => GridVector2.Distance(lv, position) <= ControlPointRadius);

        protected int? IndexOfOverlappedVertex(GridVector2 position)
        {
            for (int i = 0; i < Verticies.Count(); i++)
            {
                bool overlaps = GridVector2.Distance(Verticies[i], position) <= ControlPointRadius;
                if (overlaps)
                {
                    return new int?(i);
                }
            }

            return new int?();
        }

        protected override void Execute() => Execute(Verticies);

        /// <summary>
        /// Return the intersection point with a value if the provided line intersects any segment of our polyline.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        protected abstract GridVector2? IntersectsSelf(GridLineSegment lineSeg);
    }

    internal abstract class PolyLineCommandBase : ControlPointCommandBase
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
        protected override GridVector2? IntersectsSelf(GridLineSegment lineSeg) => Verticies.IntersectionPoint(lineSeg);
    }

    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary>
    internal class PlacePolylineCommand : PolyLineCommandBase
    {
        private readonly Stack<GridVector2> vert_stack = new();

        /// <summary>
        /// Returns the stack with the bottomost entry first in the array
        /// </summary>
        public override GridVector2[] Verticies
        {
            get => [.. ((IEnumerable<GridVector2>)[.. vert_stack]).Reverse()];
            protected set
            {
                vert_stack.Clear();
                foreach (GridVector2 v in value)
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
                   new Microsoft.Xna.Framework.Color(color.R,
                                                    color.G,
                                                    color.B,
                                                    0.5f),
                   origin,
                   LineWidth,
                   success_callback)
        {
        }

        protected override bool CanControlPointBeGrabbed(GridVector2 WorldPos) => OverlapsAnyVertex(WorldPos);

        protected override bool CanCommandComplete(GridVector2 WorldPosition) => OverlapsLastVertex(WorldPosition);

        protected override bool CanControlPointBePlaced(GridVector2 WorldPosition) => !OverlapsAnyVertex(WorldPosition);

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

                    Parent.CommandQueue.InjectCommand(new AdjustPolylineCommand(Parent,
                                                                                        LineColor,
                                                                                        Verticies,
                                                                                        LineWidth,
                                                                                        iOverlapped.Value,
                                                                                        false,
                                                                                        new OnCommandSuccess((ControlPointCommandBase, line_verticies) =>
                                                                                            {
                                                                                                Verticies = line_verticies;
                                                                                                //Update oldWorldPosition to keep the line we draw to our cursor from jumping on the first draw when we are reactivated and user hasn't used the mouse yet
                                                                                                oldWorldPosition = line_verticies[iOverlapped.Value];
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
                    Execute();
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
            else if (e.Button == MouseButtons.Left)
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
                if (CanControlPointBePlaced(WorldPos))
                {
                    vert_stack.Push(WorldPos);
                    Execute();
                    return;
                }
            }

            base.OnMouseDown(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (oldWorldPosition != Verticies.Last())
            {
                GridVector2? SelfIntersection = IntersectsSelf(new GridLineSegment(oldWorldPosition, Verticies.Last()));

                vert_stack.Push(oldWorldPosition);

                CurveView curveView = new([.. vert_stack], LineColor, false, Global.NumOpenCurveInterpolationPoints, lineWidth: LineWidth, controlPointRadius: LineWidth / 2.0);

                CurveView.Draw(graphicsDevice, scene, Parent.LumaOverlayCurveManager, basicEffect, Parent.AnnotationOverlayEffect, 0, [curveView]);
                //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

                vert_stack.Pop();

                base.OnDraw(graphicsDevice, scene, basicEffect);
            }
            else
            {
                GlobalPrimitives.DrawPolyline(Parent.LumaOverlayLineManager, basicEffect, [.. Verticies], LineWidth, LineColor);
            }
        }
    }

    /// <summary>
    /// Hold Left button down and drag a vertex to move a vertex
    /// Release left button to place the vertex and exit the command
    /// </summary>
    internal class AdjustPolylineCommand : PolyLineCommandBase
    {
        private readonly int DraggedVertexIndex;
        private GridVector2[] vert_list;

        public bool IsClosed;

        public override GridVector2[] Verticies
        {
            get => vert_list;
            protected set => vert_list = value;

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
            DraggedVertexIndex = DraggedVertex;

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
                   new Microsoft.Xna.Framework.Color(color.R,
                                                    color.G,
                                                    color.B,
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
                if (i == DraggedVertexIndex)
                {
                    continue;
                }

                if (GridVector2.Distance(WorldPosition, Verticies[i]) <= ControlPointRadius)
                {
                    return true;
                }
            }

            return false;
        }

        protected override bool CanCommandComplete(GridVector2 WorldPosition) => !OverlapsNonDraggedVertex(WorldPosition);

        protected override bool CanControlPointBePlaced(GridVector2 WorldPosition) => !OverlapsNonDraggedVertex(WorldPosition);

        protected override bool CanControlPointBeGrabbed(GridVector2 WorldPos) => throw new NotImplementedException();

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button.Left())
            {

                vert_list[DraggedVertexIndex] = WorldPos;
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
                    Verticies[DraggedVertexIndex] = WorldPos;
                    Execute();
                }
                return;
            }

            base.OnMouseUp(sender, e);
        }


        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            if (Verticies.Length > 1)
            {

                CurveView curveView = new(Verticies, LineColor,
                    IsClosed, IsClosed ? Global.NumClosedCurveInterpolationPoints : Global.NumOpenCurveInterpolationPoints, null,
                    LineWidth, ControlPointRadius,
                    Style);

                CurveView.Draw(graphicsDevice, scene,
                               Parent.LumaOverlayCurveManager, basicEffect,
                               Parent.AnnotationOverlayEffect, DateTime.UtcNow.Millisecond / 1000.0f,
                               [curveView]);
            }
            else
            {
                CircleView circleView = new(new GridCircle(Verticies[0], LineWidth / 2.0), LineColor);
                CircleView.Draw(graphicsDevice, scene, OverlayStyle.Luma, new CircleView[] { circleView });
            }

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }

}
