using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;

namespace MeasurementExtension
{
    [Viking.Common.CommandAttribute()]
    class MeasureCommand : Viking.UI.Commands.Command
    {
        GridVector2 Origin;

        public MeasureCommand(Viking.UI.Controls.SectionViewerControl parent)
            : base(parent)
        {
            //Make the cursor something distinct and appropriate for measuring
            parent.Cursor = Cursors.Cross;
        }

        protected override void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
//            GridVector2 NewPosition = Parent.ScreenToWorld(e.X, e.Y);

            //Figure out if we are starting a rectangle
            if (e.Button == MouseButtons.Left)
            {
                Origin = Parent.ScreenToWorld(e.X, e.Y);
                this.CommandActive = true;
            }

            if (e.Button == MouseButtons.Right)
                this.Execute();

            base.OnMouseDown(sender, e);
        }

        protected override void OnMouseMove(object sender, MouseEventArgs e)
        {
            base.OnMouseMove(sender, e);

            Parent.Invalidate(); 
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (CommandActive == false)
                return; 

            //Retrieve the mouse position from the last update, the base class records this for us
            Vector3 target = new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f); ;
            Color lineColor = new Color(Color.YellowGreen.R, Color.YellowGreen.G, Color.YellowGreen.B, 0.75f);

            double Distance = GridVector2.Distance(Origin, this.oldWorldPosition) * MeasurementExtension.Global.UnitsPerPixel;

            RoundLineCode.RoundLine lineToParent = new RoundLineCode.RoundLine((float)Origin.X,
                                                   (float)Origin.Y,
                                                   (float)target.X,
                                                   (float)target.Y);

            Parent.LineManager.Draw(lineToParent,
                                    (float)(1 * Parent.Downsample),
                                    lineColor,
                                    scene.Camera.View * scene.Projection,
                                    1,
                                    "Glow");

            //Draw the distance near the cursor
            Parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            GridVector2 DrawPosition = Parent.WorldToScreen(target.X, target.Y);

            Parent.spriteBatch.DrawString(Parent.fontArial,
                Distance.ToString("#0.00") + " " + Global.UnitOfMeasure,
                new Vector2((float)DrawPosition.X, (float)DrawPosition.Y),
                lineColor,
                0,
                new Vector2(0,0),
                0.5f,
                SpriteEffects.None,
                0);          

            Parent.spriteBatch.End();

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }

    }
}
