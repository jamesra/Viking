using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Geometry;
using VikingXNAGraphics;

namespace MeasurementExtension
{
   
    struct UnitsAndScale
    {
        static string[] MetricUnits = { "nm", "um", "mm", "cm", "m", "km" };
        public string Units;
        public double Scalar;

        public UnitsAndScale(string units, double scalar)
        {
            this.Units = units;
            this.Scalar = scalar;
        }

        /// <summary>
        /// Given a starting distance and measurement we return a unit and scalar that will result in a distance of less than 1,000 
        /// </summary>
        /// <param name="UnitOfMeasure"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public static UnitsAndScale ConvertToReadableUnits(string UnitOfMeasure, double distance)
        {
            if(distance <= 0)
                return new UnitsAndScale(UnitOfMeasure, 1.0);

            double numDigits = Convert.ToInt32(Math.Ceiling(Math.Log10(distance)));

            if(numDigits <= 3)
            {
                return new UnitsAndScale(UnitOfMeasure, 1.0);
            }

            int iStartUnit = Array.IndexOf(MetricUnits, UnitOfMeasure.ToLower());

            //Figure out how many 1,000 sized steps we make
            int numUnitHops = Convert.ToInt32(Math.Floor(numDigits / 3.0));
            
            if(numUnitHops + iStartUnit > MetricUnits.Length)
            {
                numUnitHops = MetricUnits.Length - iStartUnit;
            }

            int iUnit = numUnitHops + iStartUnit;

            return new UnitsAndScale(MetricUnits[iUnit], 1.0 / Math.Pow(1000, numUnitHops));
        }
    }

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
         

        private string DistanceToString(double distance)
        {
            UnitsAndScale us = UnitsAndScale.ConvertToReadableUnits(Global.UnitOfMeasure, distance);

            double scaledDistance = distance * us.Scalar;
            return scaledDistance.ToString("#0.000") + " " + us.Units;
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
            Vector3 target = new Vector3((float)this.oldWorldPosition.X, (float)oldWorldPosition.Y, 0f); ;
            Color lineColor = new Color(Color.YellowGreen.R, Color.YellowGreen.G, Color.YellowGreen.B, 0.75f);

            double VolumeDistance = GridVector2.Distance(Origin, this.oldWorldPosition) * MeasurementExtension.Global.UnitsPerPixel;


            string mosaic_space_string =  "No mosaic transform";

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
            if(mosaic_space_string != null)
            {
                output_string = "Mosaic " + mosaic_space_string + "\n" + "Volume " + volume_space_string;
            }
            else
            {
                output_string = volume_space_string;
            }

            Parent.spriteBatch.DrawString(Parent.fontArial,
                output_string,
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
