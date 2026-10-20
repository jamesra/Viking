#nullable enable

using System;
using System.Linq;
using Microsoft.Xna.Framework;
using VikingXNAGraphics;
using GeometryCircle = Geometry.Circle;

namespace WebAnnotation.UI.Commands
{
    /// <summary>
    /// Look shared by the on-screen pen buttons (choice buttons, accept/skip, cancel): an opaque disc framed by the same
    /// ring the polygon vertices use, with a caption colour chosen to read on the disc.
    /// </summary>
    internal static class PenButtonStyle
    {
        /// <summary>The ring is drawn at the disc's own radius, so its band is the outermost slice of the button.</summary>
        internal const double RingScale = 1.0;

        /// <summary>Opaque border band, as a fraction of the button radius.</summary>
        internal const double RingThickness = 0.05;

        /// <summary>Soft fade inside the border band, as a fraction of the button radius.</summary>
        internal const double RingFade = 0.05;

        /// <summary>Opacity of the button fill, so the image under a button shows through.</summary>
        internal const float DiscAlpha = 0.5f;

        /// <summary>Luminance above which a disc is light enough that black text reads better than white.</summary>
        private const double DarkTextLuminanceThreshold = 0.4;

        /// <summary>Longest word, in characters, that fits a button caption at full font size.</summary>
        private const int FullSizeWordLength = 9;

        /// <summary>Smallest fraction of the full font size a long word may shrink to.</summary>
        private const double MinimumFontScale = 0.6;

        /// <summary>
        /// The polygon-vertex ring (see <see cref="RingCircleView"/>) framing a button disc of <paramref name="disc"/>.
        /// Draw it after the disc.
        /// </summary>
        internal static RingCircleView RingFor(GeometryCircle disc, Color color) =>
            new(new GeometryCircle(disc.Center, disc.Radius * RingScale), color)
            {
                SolidStart = (float)(1 - RingThickness),
                FadeStart = (float)(1 - RingThickness - RingFade),
            };

        /// <summary>Black or white, whichever has the better contrast on a disc of <paramref name="background"/>.</summary>
        internal static Color LabelColorFor(Color background) =>
            RelativeLuminance(background) > DarkTextLuminanceThreshold ? Color.Black : Color.White;

        /// <summary>WCAG relative luminance (0 black .. 1 white) of an sRGB color; alpha is ignored.</summary>
        internal static double RelativeLuminance(Color color)
        {
            static double Linear(byte channel)
            {
                double c = channel / 255.0;
                return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        }

        /// <summary>
        /// Fraction (<see cref="MinimumFontScale"/>..1) of the base caption font size to use so the longest word of
        /// <paramref name="text"/> still fits across the button.
        /// </summary>
        internal static double LabelFontScale(string text)
        {
            int longestWord = text
                .Split([' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length)
                .DefaultIfEmpty(0)
                .Max();
            if (longestWord <= FullSizeWordLength)
                return 1;

            return Math.Max(MinimumFontScale, (double)FullSizeWordLength / longestWord);
        }
    }
}
