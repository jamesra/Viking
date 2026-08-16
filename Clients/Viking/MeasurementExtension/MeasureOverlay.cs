using Geometry;
using Rectangle = Geometry.Rectangle;
using Microsoft.Xna.Framework.Graphics;
using SIMeasurement;
using System;
using Viking.UI.Controls;
using VikingXNA;
using VikingXNAGraphics;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace MeasurementExtension
{
    [Viking.Common.SectionOverlay("Scale Bar")]
    public class MeasureOverlay : Viking.Common.ISectionOverlayExtension
    {
        private SectionViewerControl? Parent;

        public static double MeasureBarWidthScreenTargetFraction = 0.15;
        public static double MeasureBarWidthScreenMinimumFraction = 0.075;
        public static double MeasureBarHeight = double.NaN;

        public static double ScaleBarStartXFraction = 0.01;
        public static double ScaleBarStartYFraction = 0.05;

        public static Geometry.Vector2 CornerOffsetFractions = new(0.01, 0.05);

        private static readonly double log5 = Math.Log(5);

        public void Draw(GraphicsDevice graphicsDevice, Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int NextStencilValue)
        {
            if (!Measurement.Properties.Settings.Default.ShowScaleBar || Parent is null)
                return;

            double ViewWidthInPixels = scene.VisibleWorldBounds.Width;
            double ViewWidthInUnits = ViewWidthInPixels / Global.UnitsPerPixel;

            LengthMeasurement ApproximateViewBarWidth = new(Global.UnitOfMeasure, ViewWidthInUnits * MeasureBarWidthScreenTargetFraction);
            LengthMeasurement AdjustedApproximateViewBarWidth = LengthMeasurement.ConvertToReadableUnits(Global.UnitOfMeasure, ViewWidthInUnits * MeasureBarWidthScreenTargetFraction);

            //Round to the nearest power of 10
            double log10 = Math.Log10(AdjustedApproximateViewBarWidth.Length);
            int numDigits = Convert.ToInt32(Math.Ceiling(log10));

            double MeasureBarDistance = Math.Pow(10, numDigits);

            if (log10 - Math.Floor(log10) > log5 - 1)
            {
                MeasureBarDistance *= 5;
            }

            LengthMeasurement FinalBarWidth = new(AdjustedApproximateViewBarWidth.Units, MeasureBarDistance);
            FinalBarWidth = FinalBarWidth.ConvertTo(Global.PixelWidth.Units);
            //Determine how large our scale bar is in screen pixels
            double BarWidthInPixels = FinalBarWidth / Global.PixelWidth;
            while (BarWidthInPixels / ViewWidthInPixels < MeasureBarWidthScreenMinimumFraction)
            {
                FinalBarWidth *= 2;
                BarWidthInPixels = FinalBarWidth / Global.PixelWidth;
            }

            double BarHeightInPixels = (VikingXNAGraphics.Global.DefaultFont.LineSpacing * Parent.Downsample) / 3;

            Geometry.Vector2 CornerOffset = new(scene.VisibleWorldBounds.Width * CornerOffsetFractions.X, scene.VisibleWorldBounds.Height * CornerOffsetFractions.Y);

            //double BarStartX = scene.VisibleWorldBounds.Left + CornerOffset.X;
            double BarStartY = scene.VisibleWorldBounds.Bottom + (CornerOffset.Y + (2 * BarHeightInPixels));

            double BarEndX = scene.VisibleWorldBounds.Right - CornerOffset.X;
            double BarStartX = BarEndX - BarWidthInPixels;

            Rectangle scaleBarRect = new(new Geometry.Vector2(BarStartX, BarStartY), BarWidthInPixels, BarHeightInPixels);

            //Draw a black box
            RectangleView scaleBarView = new(scaleBarRect, Microsoft.Xna.Framework.Color.Black);

            RectangleView.Draw(graphicsDevice, scene, OverlayStyle.Alpha, [scaleBarView]);

            LabelView label = new(LengthMeasurement.ConvertToReadableUnits(FinalBarWidth).ToString(), scaleBarRect.Center, scaleFontWithScene: true)
            {
                Color = Microsoft.Xna.Framework.Color.White,
                FontSize = BarHeightInPixels * 0.9
            };

            if (Parent.spriteBatch != null)
                LabelView.Draw(Parent.spriteBatch, VikingXNAGraphics.Global.DefaultFont, scene, new LabelView[] { label });
        }

        public int DrawOrder() => 10;

        public string Name() => "Scale Bar";

        public object? ObjectAtPosition(Geometry.Vector2 WorldPosition, out double distance)
        {
            distance = double.MaxValue;
            return null;
        }

        public void SetParent(SectionViewerControl parent) => Parent = parent;
    }
}
