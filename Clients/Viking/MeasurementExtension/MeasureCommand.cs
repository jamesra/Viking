using Geometry;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SIMeasurement;
using System.Collections.Generic;
using System.Web.UI.WebControls;
using System.Windows.Forms;
using VikingXNA;
using VikingXNAGraphics;
using HorizontalAlignment = VikingXNAGraphics.HorizontalAlignment;
using Label = System.Web.UI.WebControls.Label;

namespace MeasurementExtension
{
    [Viking.Common.CommandAttribute()]
    public class MeasureCommand : Viking.UI.Commands.Command, Viking.Common.IObservableHelpStrings
    {
        GridVector2 Origin;
        private readonly LengthMeasurement PixelSize;
        private readonly LabelView distanceLabel;

        private static readonly string[] DefaultHelpStrings = new string[]
        {
            "Hold SHIFT: Force horizontal measurement line"
        };

        public virtual string[] HelpStrings
        {
            get
            {
                List<string> s = new List<string>(MeasureCommand.DefaultHelpStrings)
                {
                    "CTRL - Horizontal measurement",
                    "SHIFT - Vertical measurement"
                };
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

        public MeasureCommand(Viking.UI.Controls.SectionViewerControl parent, LengthMeasurement pixelSize)
            : base(parent)
        {
            //Make the cursor something distinct and appropriate for measuring
            parent.Cursor = Cursors.Cross;
            PixelSize = pixelSize;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="distance"></param>
        /// <returns>A string describing the distance in human readable units</returns>
        private static string DistanceToString(LengthMeasurement distance)
        {
            return LengthMeasurement.ConvertToReadableUnits(distance).ToString(3, PreserveNonSignificant: true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="distance"></param>
        /// <returns>null if no value passed, otherwise as string describing the distance</returns>
        private string DistanceToString(LengthMeasurement? distance)
        {
            if (distance.HasValue)
            {
                return LengthMeasurement.ConvertToReadableUnits(distance.Value).ToString(3, PreserveNonSignificant: true);
            }

            return null;
        }

        private LengthMeasurement GetVolumeDistance(GridVector2 target)
        { 
            return new LengthMeasurement(PixelSize.Units,
                GridVector2.Distance(Origin, target) * PixelSize.Length);
        }

        private LengthMeasurement? GetMosaicDistance(GridVector2 target)
        {
            if (false == Viking.UI.State.volume.UsingVolumeTransform)
            {
                return null;
            }

            bool transformedOrigin = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(Origin, out GridVector2 mosaic_origin);
            bool transformedTarget = Parent.Section.ActiveSectionToVolumeTransform.TryVolumeToSection(target, out GridVector2 mosaic_target);

            if (transformedOrigin && transformedTarget)
            {
                return new LengthMeasurement(PixelSize.Units, GridVector2.Distance(mosaic_origin, mosaic_target) * PixelSize.Length);
            }

            return null;
        }

        public override void OnDraw(GraphicsDevice graphicsDevice, VikingXNA.Scene scene, BasicEffect basicEffect)
        {
            if (CommandActive == false)
                return;

            Color lineColor = Color.Yellow.SetAlpha(0.9f);//new Color(Color.YellowGreen.R, Color.YellowGreen.G, Color.YellowGreen.B, 0.75f));

            //Retrieve the mouse position from the last update, the base class records this for us
            Vector3 target = new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f);
            if ((Control.ModifierKeys == Keys.Shift))
            {
                target.Y = (float)Origin.Y;
            }
            else if((Control.ModifierKeys == Keys.Control))
            {
                target.X = (float)Origin.X;
            } 

            LengthMeasurement volumeDistance = GetVolumeDistance(target.ToGridVector2XY());
            var mosaicDistance = GetMosaicDistance(target.ToGridVector2XY());
              
            RoundLineCode.RoundLine lineToParent = new RoundLineCode.RoundLine((float)Origin.X,
                                                   (float)Origin.Y,
                                                   (float)target.X,
                                                   (float)target.Y);

            Parent.LumaOverlayLineManager.Draw(lineToParent,
                                    (float)(6 * Parent.Downsample),
                                    lineColor.ConvertToHSL(),
                                    scene.Camera.View * scene.Projection,
                                    1,
                                    "Glow");

            //Draw the distance near the cursor
            string distanceLabelString = DistanceLabel(volumeDistance, mosaicDistance);
            GridVector2 DrawPosition = Parent.WorldToScreen(target.X, target.Y);
            var alignment = FindTextAlignment(Origin, target.ToGridVector2XY());
            var anchor = FindTextAnchor(Origin, target.ToGridVector2XY());
            var fontSize = 40.0f;
            float fontScaleForVolume = (float)(fontSize / (double)VikingXNAGraphics.Global.DefaultFont.LineSpacing);
            //distanceLabel = new LabelView(distanceLabelString, target.ToGridVector2XY(),  lineColor, alignment,
                //anchor, scaleFontWithScene: false,  fontSize: 32);

            var label_size = VikingXNAGraphics.Global.DefaultFont.MeasureString(distanceLabelString) * fontScaleForVolume;
            var half_label_size = label_size / 2;
            GridVector2 offset = new GridVector2(
                anchor.Horizontal == HorizontalAlignment.LEFT ? 0 : anchor.Horizontal == HorizontalAlignment.RIGHT ? -label_size.X : -half_label_size.X,
                anchor.Vertical == VerticalAlignment.BOTTOM ? -label_size.Y : anchor.Vertical == VerticalAlignment.TOP ? 0 : -half_label_size.Y
            );

            DrawPosition += offset;

            //Draw the distance near the cursor
            Parent.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
               
            Parent.spriteBatch.DrawString(VikingXNAGraphics.Global.DefaultFont,
                distanceLabelString,
                new Vector2((float)DrawPosition.X, (float)DrawPosition.Y),
                Color.DarkMagenta,
                0,
                new Vector2(0, 0),
                fontScaleForVolume,
                SpriteEffects.None,
                0);

            Parent.spriteBatch.End();
             
            base.OnDraw(graphicsDevice, scene, basicEffect);
        }
        
        /// <summary>
        /// Choose a text alignment that puts the label away from the target, and not over the line we are going to draw
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private Alignment FindTextAlignment(GridVector2 origin, GridVector2 target)
        {
            return Alignment.TopLeft;
            HorizontalAlignment hAlign = origin.X < target.X ? HorizontalAlignment.RIGHT : HorizontalAlignment.LEFT;
            VerticalAlignment vAlign = origin.Y < target.Y ? VerticalAlignment.BOTTOM : VerticalAlignment.TOP;
            return new Alignment { Horizontal = hAlign, Vertical = vAlign };
                
        }

        /// <summary>
        /// Choose a text anchor that puts the label away from the target, and not over the line we are going to draw
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private Anchor FindTextAnchor(GridVector2 origin, GridVector2 target)
        { 
            HorizontalAlignment hAlign = origin.X <= target.X ? HorizontalAlignment.RIGHT : HorizontalAlignment.LEFT;
            VerticalAlignment vAlign = origin.Y < target.Y ? VerticalAlignment.BOTTOM : VerticalAlignment.TOP;
            return new Anchor { Horizontal = hAlign, Vertical = vAlign };
                
        }

        private string DistanceLabel(LengthMeasurement volumeDistance, LengthMeasurement? mosaicDistance)
        {
            string volume_space_string = DistanceToString(volumeDistance);
            string mosaic_space_string = DistanceToString(mosaicDistance);
            if (mosaicDistance != null)
            {
                return $"{mosaic_space_string} Mosaic\n{volume_space_string} Volume";
            }
            else
            {
                return volume_space_string;
            }
        }
    }
}
