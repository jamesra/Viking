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
        private SectionViewerControl? Parent;

        public static double MeasureBarWidthScreenTargetFraction = 0.15;
        public static double MeasureBarWidthScreenMinimumFraction = 0.075;
        public static double MeasureBarHeight = double.NaN;

        public static double ScaleBarStartXFraction = 0.01;
        public static double ScaleBarStartYFraction = 0.05;

        public static Geometry.Vector2 CornerOffsetFractions = new(0.01, 0.05);

        public void Draw(GraphicsDevice graphicsDevice, Scene scene, Texture BackgroundLuma, Texture BackgroundColors, ref int NextStencilValue)
        {
            if (!Measurement.Properties.Settings.Default.ShowScaleBar || Parent is null)
                return;

            double ViewWidthInPixels = scene.VisibleWorldBounds.Width;
            if (ViewWidthInPixels <= 0)
                return;

            double ViewWidthInUnits = ViewWidthInPixels / Global.UnitsPerPixel;
            if (ViewWidthInUnits <= 0)
                return;

            LengthMeasurement ApproximateViewBarWidth = new(Global.UnitOfMeasure, ViewWidthInUnits * MeasureBarWidthScreenTargetFraction);
            LengthMeasurement AdjustedApproximateViewBarWidth = LengthMeasurement.ConvertToReadableUnits(Global.UnitOfMeasure, ViewWidthInUnits * MeasureBarWidthScreenTargetFraction);

            if (!ScaleBarLayout.TryRoundReadableLengthToBarDistance(AdjustedApproximateViewBarWidth.Length, out double MeasureBarDistance))
                return;

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

            Geometry.Rectangle scaleBarRect = new(new Geometry.Vector2(BarStartX, BarStartY), BarWidthInPixels, BarHeightInPixels);

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
