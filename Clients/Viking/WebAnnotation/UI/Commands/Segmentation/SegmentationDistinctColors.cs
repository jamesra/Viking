using Microsoft.Xna.Framework;
using System;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Distinct overlay hues for multi-mask segmentation UI.
    /// </summary>
    internal static class SegmentationDistinctColors
    {
        /// <summary>
        /// Evenly spaced hues for segment index <paramref name="index"/> among <paramref name="total"/> masks.
        /// </summary>
        internal static Color GenerateDistinctColor(int index, int total)
        {
            float hue = (float)index / Math.Max(total, 1);
            return ColorFromHsl(hue, 0.8f, 0.5f, 0.25f);
        }

        /// <summary>
        /// Converts HSL (each component 0–1) to an XNA <see cref="Color"/>.
        /// </summary>
        internal static Color ColorFromHsl(float hue, float saturation, float lightness, float alpha)
        {
            hue = hue - (float)Math.Floor(hue);

            float r, g, b;

            if (saturation == 0)
            {
                r = g = b = lightness;
            }
            else
            {
                float q = lightness < 0.5f
                    ? lightness * (1 + saturation)
                    : lightness + saturation - lightness * saturation;
                float p = 2 * lightness - q;

                r = HueToRgb(p, q, hue + 1f / 3f);
                g = HueToRgb(p, q, hue);
                b = HueToRgb(p, q, hue - 1f / 3f);
            }

            return new Color(r, g, b, alpha);
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
