using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Windows.Forms;
using WebAnnotation.View;
using Viking.Common;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Handles callback, drawing, and vertex/color/width properties
    /// </summary>
    abstract class PolylineCommandBase : Viking.UI.Commands.Command
    {
        public double LineWidth = 1;
        protected Microsoft.Xna.Framework.Color LineColor;

        public abstract GridVector2[] LineVerticies
        {
            get;
            protected set;
        }

        public delegate void OnCommandSuccess(GridVector2[] control_points);
        protected OnCommandSuccess success_callback;

        public PolylineCommandBase(Viking.UI.Controls.SectionViewerControl parent, 
                                     Microsoft.Xna.Framework.Color color, 
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : base(parent)
        {
            this.LineColor = color;
            this.LineWidth = LineWidth;
            this.success_callback = success_callback;
        }

        public PolylineCommandBase(Viking.UI.Controls.SectionViewerControl parent, 
                                     System.Drawing.Color color, 
                                     double LineWidth,
                                     OnCommandSuccess success_callback)
            : this(parent, 
                   new Microsoft.Xna.Framework.Color((int)color.R,
                                                          (int)color.G,
                                                          (int)color.B,
                                                          0.5f),
                   LineWidth,
                   success_callback)
        { 
        }
         
        protected override void Execute()
        {
            if (this.success_callback != null)
                this.success_callback(this.LineVerticies);

            base.Execute();
        }

        protected bool OverlapsLastVertex(GridVector2 position)
        {
            return GridVector2.Distance(LineVerticies.Last(), position) <= LineWidth;
        }

        protected bool OverlapsAnyVertex(GridVector2 position)
        {
            foreach (GridVector2 v in this.LineVerticies)
            {
                bool overlaps = GridVector2.Distance(v, position) <= LineWidth;
                if (overlaps)
                    return true;
            }

            return false;
        }

        protected int?IndexOfOverlappedVertex(GridVector2 position)
        {
            for(int i = 0; i < this.LineVerticies.Count(); i++)
            {
                bool overlaps = GridVector2.Distance(this.LineVerticies[i], position) <= LineWidth;
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
        protected GridVector2? IntersectsSelf(GridLineSegment lineSeg)
        {
            GridVector2 intersection;
            for(int i=1; i < this.LineVerticies.Length; i++)
            {
                GridLineSegment existingLine = new GridLineSegment(this.LineVerticies[i - 1], this.LineVerticies[i]);
                if(existingLine.Intersects(lineSeg, out intersection))
                {
                    return new GridVector2?(intersection);
                }
            }

            return new GridVector2?();
        }


    }

    /// <summary>
    /// Left-click once to create a new vertex in the poly line
    /// Left-click an existing vertex to complete polyline creation
    /// Double left-click to complete polyline creation
    /// Right-click to remove the last polyline vertex
    /// </summary>
    class PlacePolylineCommand : PolylineCommandBase
    {
        private Stack<GridVector2> vert_stack = new Stack<GridVector2>();

        /// <summary>
        /// Returns the stack with the bottomost entry first in the array
        /// </summary>
        public override GridVector2[] LineVerticies
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


        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            if (e.Button == MouseButtons.None)
            {
                if (OverlapsAnyVertex(WorldPos))
                {
                    Parent.Cursor = Cursors.Hand;
                }
                else
                {
                    Parent.Cursor = Cursors.Cross;
                }
            }
           else if (e.Button == MouseButtons.Left)
            {
                //Drag the vertex under the cursor
                int? iOverlapped = IndexOfOverlappedVertex(WorldPos);
               if(iOverlapped.HasValue)
               {
                   Viking.UI.Commands.Command.InjectCommand(new AdjustPolylineCommand(this.Parent,
                                                                                       this.LineColor,
                                                                                       this.LineVerticies,
                                                                                       this.LineWidth,
                                                                                       iOverlapped.Value,
                                                                                       new OnCommandSuccess((line_verticies) => 
                                                                                        {this.LineVerticies = line_verticies;
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

                    GridVector2? SelfIntersection = IntersectsSelf(new GridLineSegment(WorldPos, LineVerticies.Last()));
                     
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
            if (this.oldWorldPosition != LineVerticies.Last())
            {
                GridVector2? SelfIntersection = IntersectsSelf(new GridLineSegment(this.oldWorldPosition, LineVerticies.Last()));

                vert_stack.Push(this.oldWorldPosition);

                CurveView.Draw(graphicsDevice, Parent.LumaOverlayLineManager, basicEffect, vert_stack.ToList(), 5, false, this.LineColor.ConvertToHSL(), this.LineWidth); 
                //GlobalPrimitives.DrawPolyline(Parent.LineManager, basicEffect, DrawnLineVerticies, this.LineWidth, this.LineColor);

                this.vert_stack.Pop();

                base.OnDraw(graphicsDevice, scene, basicEffect);
            }
            else
            {
                GlobalPrimitives.DrawPolyline(Parent.LumaOverlayLineManager, basicEffect, this.LineVerticies.ToList(), this.LineWidth, this.LineColor);
            }
        }
    }

    /// <summary>
    /// Hold Left button down and drag a vertex to move a vertex
    /// Release left button to place the vertex and exit the command
    /// </summary>
    class AdjustPolylineCommand : PolylineCommandBase
    {
        int DraggedVertexIndex;

        List<GridVector2> vert_list;

        public override GridVector2[] LineVerticies
        {
            get { return vert_list.ToArray(); }
            protected set { vert_list = new List<GridVector2>(value);
            }
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
                                     OnCommandSuccess success_callback)
            : base(parent, color, LineWidth, success_callback)
        {
            vert_list = new List<GridVector2>(verticies);
            this.DraggedVertexIndex = DraggedVertex;

            parent.Cursor = Cursors.Cross;
        }

        public AdjustPolylineCommand(Viking.UI.Controls.SectionViewerControl parent,
                                     System.Drawing.Color color,
                                     GridVector2[] verticies,
                                     double LineWidth,
                                     int DraggedVertex,
                                     OnCommandSuccess success_callback)
            : this(parent, 
                   new Microsoft.Xna.Framework.Color((int)color.R,
                                                    (int)color.G,
                                                    (int)color.B,
                                                    0.5f),
                   verticies, 
                   LineWidth,
                   DraggedVertex,
                   success_callback)
        {
        }
        
        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);
             
            if (e.Button == MouseButtons.None)
            {
                if (OverlapsAnyVertex(WorldPos))
                {
                    Parent.Cursor = Cursors.Hand;
                }
                else
                {
                    Parent.Cursor = Cursors.Cross;
                }
            }
            else if(e.Button == MouseButtons.Left)
            {
                this.vert_list[this.DraggedVertexIndex] = WorldPos;
                Parent.Invalidate(); 
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                //If we release the left mouse button the command is completed
                GridVector2[] Verticies = LineVerticies.ToArray();
                Verticies[this.DraggedVertexIndex] = WorldPos;
                this.Execute();
                return;
            }

            base.OnMouseDown(sender, e);
        }
         

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
            CurveView.Draw(graphicsDevice, Parent.LumaOverlayLineManager, basicEffect, this.LineVerticies.ToList(), 5, false, this.LineColor, this.LineWidth);
           
            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }

}
