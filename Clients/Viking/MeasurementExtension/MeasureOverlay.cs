using Geometry;
using Microsoft.Xna.Framework.Graphics;
using SIMeasurement;
using System;
using Viking.UI.Controls;
using VikingXNA;
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

        public static GridVector2 CornerOffsetFractions = new GridVector2(0.01, 0.05);

        private static readonly double log5 = Math.Log(5);
          
        public void Draw(GraphicsDevice graphicsDevice, Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int NextStencilValue)
        {
            if (!Measurement.Properties.Settings.Default.ShowScaleBar)
                return; 

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
            double BarHeightInPixels = (VikingXNAGraphics.Global.DefaultFont.LineSpacing * Parent.Downsample) / 3;

            GridVector2 CornerOffset = new GridVector2(scene.VisibleWorldBounds.Width * CornerOffsetFractions.X, scene.VisibleWorldBounds.Height * CornerOffsetFractions.Y);

            //double BarStartX = scene.VisibleWorldBounds.Left + CornerOffset.X;
            double BarStartY = scene.VisibleWorldBounds.Bottom + (CornerOffset.Y + (2 * BarHeightInPixels));

            double BarEndX = scene.VisibleWorldBounds.Right - CornerOffset.X;
            double BarStartX = BarEndX - BarWidthInPixels;

            GridRectangle scaleBarRect = new GridRectangle(new GridVector2(BarStartX, BarStartY), BarWidthInPixels, BarHeightInPixels);

            //Draw a black box
            RectangleView scaleBarView = new RectangleView(scaleBarRect, Microsoft.Xna.Framework.Color.Black);
            
            RectangleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, new RectangleView[] { scaleBarView });

            LabelView label = new LabelView(LengthMeasurement.ConvertToReadableUnits(FinalBarWidth).ToString(), scaleBarRect.Center)
            {
                Color = Microsoft.Xna.Framework.Color.White,
                FontSize = BarHeightInPixels * 0.9
            };

            LabelView.Draw(Parent.spriteBatch, VikingXNAGraphics.Global.DefaultFont, scene,  new LabelView[] { label });
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
