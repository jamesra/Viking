using Geometry;
using System.Windows.Forms;
using VikingXNAGraphics;

namespace WebAnnotation.UI.Commands
{
    internal class ResizeCircleCommand : Viking.UI.Commands.Command
    {
        public double Radius;

        public GridVector2 Origin;
        private readonly System.Drawing.Color CircleColor;

        public delegate void OnCommandSuccess(double radius);

        private readonly OnCommandSuccess success_callback;

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

        private void UpdateRadius(MouseEventArgs e)
        {
            GridVector2 WorldPos = Parent.ScreenToWorld(e.X, e.Y);

            Radius = GridVector2.Distance(Origin, WorldPos);
        }

        protected override void OnMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            UpdateRadius(e);

            Parent.Invalidate();

            base.OnMouseMove(sender, e);
        }

        protected override void OnDeactivate()
        {
            //A bit of a hack.  We null the selected object so the viewer control doesn't decide to start the default
            //command for the selected object when it creates the next command.  It should launch the default command instead.
            Viking.UI.State.SelectedObject = null;

            base.OnDeactivate();
        }

        protected override void Execute()
        {
            if (success_callback != null)
            {
                success_callback(Radius);
            }

            base.Execute();
        }

        protected override void OnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //    TimeSpan Elapsed = new TimeSpan(DateTime.Now.Ticks - CreationTime.Ticks);
                UpdateRadius(e);

                Execute();
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

            GlobalPrimitives.DrawCircle(graphicsDevice, basicEffect, Pos, Radius, color);

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
    }
}
