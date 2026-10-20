namespace WebAnnotation.WPF.Controls
{
    /// <summary>
    /// How <see cref="CircleSizePreviewSlider.Value"/> is interpreted and drawn.
    /// </summary>
    public enum CircleSizePreviewMode
    {
        PercentOfScreen,
        PixelDiameter,
        PixelRadius,
        /// <summary>Value is a radius in nanometers; preview size uses NanometersPerPixel.</summary>
        NanometerRadius
    }
}
