using Geometry;
using Microsoft.Xna.Framework.Graphics;
using System.Windows.Forms;
using VikingXNAWinForms;


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
            if (e.Button.Left() && !oldMouse.Button.Left())
            {
                this.rectangle = new GridRectangle(WorldPosition, 0, 0);
            }
            else if (e.Button.Left())
            {
                this.rectangle += WorldPosition;
            }
            //If the mouse was released we stop drawing rectangle
            else if (!e.Button.Left() && oldMouse.Button.Left())
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
