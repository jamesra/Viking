using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using System.Diagnostics;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using WebAnnotation.ViewModel;
using WebAnnotationModel; 

namespace WebAnnotation.UI.Commands
{
    class ResizeCircleCommand : Viking.UI.Commands.Command
    {
        public double radius;
         
        public GridVector2 Origin;
        System.Drawing.Color CircleColor; 
 
        //DateTime CreationTime; 
        private bool HasMouseMoved = false;

        public delegate void OnCommandSuccess(double radius);
        OnCommandSuccess success_callback;

        public ResizeCircleCommand(Viking.UI.Controls.SectionViewerControl parent, 
                                     System.Drawing.Color color,  
                                     GridVector2 origin,  
                                     OnCommandSuccess success_callback)
            : base(parent)
        { 
            CircleColor = color; 
            Origin = origin;
            parent.Cursor = Cursors.SizeAll;
            this.success_callback = success_callback;
        }

        protected override void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            this.radius = GridVector2.Distance(Origin, WorldPos); 

            HasMouseMoved=true;

            Parent.Invalidate(); 

            base.OnMouseMove(sender, e);
        }

        public override void OnDeactivate()
        {
            //A bit of a hack.  We null the selected object so the viewer control doesn't decide to start the default
            //command for the selected object when it creates the next command.  It should launch the default command instead.
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        protected override void Execute()
        {
            if(this.success_callback != null)
                this.success_callback(this.radius);

            base.Execute();
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
            //    TimeSpan Elapsed = new TimeSpan(DateTime.Now.Ticks - CreationTime.Ticks);
                GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

                this.radius = GridVector2.Distance(Origin, WorldPos); 
                
                this.Execute(); 
            }

            base.OnMouseDown(sender, e);
        }

        public override void OnDraw(Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice, VikingXNA.Scene scene, Microsoft.Xna.Framework.Graphics.BasicEffect basicEffect)
        {
//            double OldRadius = selected.Radius;
            GridVector2 Pos = Origin;

            Microsoft.Xna.Framework.Color color = new Microsoft.Xna.Framework.Color(CircleColor.R,
                CircleColor.G,
                CircleColor.B,
                128);

            GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, Pos, this.radius, color); 
            
            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
