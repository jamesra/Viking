using Microsoft.Xna.Framework.Graphics;
using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// Text widths for laying out labels before they are drawn. A <see cref="LabelView"/>'s FontSize is its line spacing
    /// in volume units, so a row's width is its unit width times its FontSize.
    /// </summary>
    public static class LabelMetrics
    {
        /// <summary>Width of one character, in line spacings, when no font is loaded.</summary>
        public const double FallbackCharacterWidth = 0.6;

        /// <summary>
        /// Width of <paramref name="text"/> drawn on one row in <see cref="Global.DefaultFont"/> at a FontSize of 1.
        /// <see cref="SpriteFont.MeasureString(string)"/> only reads the glyph table, so this is safe off the render thread.
        /// </summary>
        public static double UnitWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            SpriteFont font = Global.TryGetDefaultFont();
            if (font is null || font.LineSpacing <= 0)
                return FallbackCharacterWidth * text.Length;

            try
            {
                return font.MeasureString(text).X / font.LineSpacing;
            }
            catch (ArgumentException)
            {
                // A character the font lacks
                return FallbackCharacterWidth * text.Length;
            }
        }
    }
}
