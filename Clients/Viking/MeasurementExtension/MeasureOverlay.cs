using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Geometry;
using Microsoft.Xna.Framework.Graphics;
using Viking.UI.Controls;
using VikingXNA;
using SIMeasurement;
using VikingXNAGraphics;

namespace MeasurementExtension
{
    [Viking.Common.SectionOverlay("Scale Bar")]
    public class MeasureOverlay : Viking.Common.ISectionOverlayExtension
    {
        private SectionViewerControl Parent; 

        public static double MeasureBarWidthScreenFraction = 0.15;
        public static double MeasureBarHeight = double.NaN;

        public static double ScaleBarStartXFraction = 0.01;
        public static double ScaleBarStartYFraction = 0.05;

        private static double log5 = Math.Log(5);

        public void Draw(GraphicsDevice graphicsDevice, Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int NextStencilValue)
        {
            double ViewWidthInPixels = scene.VisibleWorldBounds.Width;
            double ViewWidthInUnits = ViewWidthInPixels / Global.UnitsPerPixel;

            LengthMeasurement ApproximateViewBarWidth = new LengthMeasurement(Global.UnitOfMeasure, ViewWidthInUnits * MeasureBarWidthScreenFraction);

            LengthMeasurement AdjustedApproximateViewBarWidth = LengthMeasurement.ConvertToReadableUnits(Global.UnitOfMeasure, ViewWidthInUnits * MeasureBarWidthScreenFraction);

            //Round to the nearest power of 10
            double log10 = Math.Log10(AdjustedApproximateViewBarWidth.Length);
            int numDigits = Convert.ToInt32(Math.Ceiling(log10));

            double MeasureBarDistance = Math.Pow(10, numDigits);

            if(log10 - Math.Floor(log10) > log5 - 1)
            {
                MeasureBarDistance *= 5;
            }

            LengthMeasurement FinalBarWidth = new LengthMeasurement(AdjustedApproximateViewBarWidth.Units, MeasureBarDistance);
            FinalBarWidth = FinalBarWidth.ConvertTo(SILengthUnits.nm);
            //Determine how large our scale bar is in screen pixels
            double BarWidthInPixels = FinalBarWidth / Global.PixelWidth;
            double BarHeightInPixels = Parent.fontArial.LineSpacing * Parent.Downsample / 3;

            double BarStartX = scene.VisibleWorldBounds.Left + (ViewWidthInPixels * ScaleBarStartXFraction);
            double BarStartY = scene.VisibleWorldBounds.Top - (1 * (scene.VisibleWorldBounds.Height * ScaleBarStartYFraction));

            GridRectangle scaleBarRect = new GridRectangle(new GridVector2(BarStartX, BarStartY), BarWidthInPixels, BarHeightInPixels);

            //Draw a black box
            RectangleView scaleBarView = new RectangleView(scaleBarRect, Microsoft.Xna.Framework.Color.Black);

            RectangleView.Draw(graphicsDevice, scene, Parent.basicEffect, Parent.AnnotationOverlayEffect, new RectangleView[] { scaleBarView });

            LabelView label = new LabelView(LengthMeasurement.ConvertToReadableUnits(FinalBarWidth).ToString(), scaleBarRect.Center);
            label.Color = Microsoft.Xna.Framework.Color.White;
            label.FontSize = BarHeightInPixels * 0.9;

            

            Parent.spriteBatch.Begin();
            label.Draw(Parent.spriteBatch, Parent.fontArial, scene);
            Parent.spriteBatch.End();
        }

        public int DrawOrder()
        {
            return 10;
        }

        public string Name()
        {
            return "Scale Bar";
        }

        public object ObjectAtPosition(GridVector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue;
            return null;
        }

        public void SetParent(SectionViewerControl parent)
        {
            Parent = parent;
        }
    }
}
