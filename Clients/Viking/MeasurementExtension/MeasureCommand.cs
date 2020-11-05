using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SIMeasurement;
using System.Collections.Generic;
using System.Windows.Forms;
using VikingXNAGraphics;

namespace MeasurementExtension
{
    [Viking.Common.CommandAttribute()]
    class MeasureCommand : Viking.UI.Commands.Command, Viking.Common.IObservableHelpStrings
    {
        GridVector2 Origin;

        private static string[] DefaultHelpStrings = new string[]
        {
            "Hold SHIFT: Force horizontal measurement line"
        };

        public virtual string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>(MeasureCommand.DefaultHelpStrings);
                s.AddRange(Viking.UI.Commands.Command.DefaultKeyHelpStrings);
                return s.ToArray();
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<string> ObservableHelpStrings
        {
            get
            {
                return new System.Collections.ObjectModel.ObservableCollection<string>(this.HelpStrings);
            }
        }

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


        private string DistanceToString(double distance)
        {
            LengthMeasurement us = LengthMeasurement.ConvertToReadableUnits(Global.UnitOfMeasure, distance);
            return us.ToString(3, true);
            //return us.Length.ToString("#0.000") + " " + us.Units;
        }

        private double? GetMosaicDistance()
        {
            GridVector2 mosaic_origin;
            GridVector2 mosaic_target;
            bool transformed_origin = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(Origin, out mosaic_origin);
            bool transformed_current = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(this.oldWorldPosition, out mosaic_target);

            if (transformed_origin && transformed_current)
            {
                return new double?(GridVector2.Distance(mosaic_origin, mosaic_target) * MeasurementExtension.Global.UnitsPerPixel);
            }

            return new double?();
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (CommandActive == false)
                return;

            //Retrieve the mouse position from the last update, the base class records this for us
            Vector3 target = new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f);
            if ((Control.ModifierKeys == Keys.Shift))
            {
                target.Y = (float)Origin.Y;
            }

            Color lineColor = new Color(Color.YellowGreen.R, Color.YellowGreen.G, Color.YellowGreen.B, 0.75f);

            double VolumeDistance = GridVector2.Distance(Origin, this.oldWorldPosition) * MeasurementExtension.Global.UnitsPerPixel;

            string mosaic_space_string = "No mosaic transform";

            if (Viking.UI.State.volume.UsingVolumeTransform)
            {
                double? mosaicDistance = GetMosaicDistance();
                if (mosaicDistance.HasValue)
                {
                    mosaic_space_string = DistanceToString(mosaicDistance.Value);
                }
            }
            else
            {
                mosaic_space_string = null;
            }

            string volume_space_string = DistanceToString(VolumeDistance);

            RoundLineCode.RoundLine lineToParent = new RoundLineCode.RoundLine((float)Origin.X,
                                                   (float)Origin.Y,
                                                   (float)target.X,
                                                   (float)target.Y);

            Parent.LumaOverlayLineManager.Draw(lineToParent,
                                    (float)(1 * Parent.Downsample),
                                    lineColor.ConvertToHSL(),
                                    scene.Camera.View * scene.Projection,
                                    1,
                                    "Glow");

            //Draw the distance near the cursor
            Parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            GridVector2 DrawPosition = Parent.WorldToScreen(target.X, target.Y);

            string output_string = null;
            if (mosaic_space_string != null)
            {
                output_string = mosaic_space_string + " Mosaic\n" + volume_space_string + " Volume";
            }
            else
            {
                output_string = volume_space_string;
            }

            Parent.spriteBatch.DrawString(VikingXNAGraphics.Global.DefaultFont,
                output_string,
                new Vector2((float)DrawPosition.X, (float)DrawPosition.Y),
                lineColor,
                0,
                new Vector2(0, 0),
                0.25f,
                SpriteEffects.None,
                0);

            Parent.spriteBatch.End();

            base.OnDraw(graphicsDevice, scene, basicEffect);
        }

    }
}
