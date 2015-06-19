using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Windows.Forms;
using VikingXNA;
using Geometry;

namespace Viking.UI.Commands
{
    public class ROIRectCommand : Command
    {
        GridRectangle rectangle; 

        public ROIRectCommand(Viking.UI.Controls.SectionViewerControl ctrl) : base(ctrl)
        {

        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            base.OnMouseMove(sender, e);
            GridVector2 WorldPosition = Parent.ScreenToWorld(e.X, e.Y);

            //Check if we should start a rectangle
            if (e.Button == MouseButtons.Left && oldMouse.Button != MouseButtons.Left)
            {
                this.rectangle = new GridRectangle(WorldPosition, 0, 0); 
            }
            else if (e.Button == MouseButtons.Left)
            {
                if (WorldPosition.Y < this.rectangle.Bottom)
                    this.rectangle.Bottom = WorldPosition.Y;
                else
                    this.rectangle.Top = WorldPosition.Y;

                if (WorldPosition.X < this.rectangle.Left)
                    this.rectangle.Left = WorldPosition.X;
                else
                    this.rectangle.Right = WorldPosition.X; 
            }
            //If the mouse was released we stop drawing rectangle
            else if (e.Button != MouseButtons.Left && oldMouse.Button == MouseButtons.Left)
            {
                this.CommandActive = false; 
            }
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            base.OnDraw(graphicsDevice, scene, basicEffect); 
        }

    
    }
}
