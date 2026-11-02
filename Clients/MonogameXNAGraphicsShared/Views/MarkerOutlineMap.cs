using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// The picture a single-colour icon marker (the arrows marking a link to an overlapped location) is drawn from when it gets a
    /// contrasting border like a label's (<see cref="OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOutlinedOverlayEffect"/>).
    /// The icon is still drawn in any color; the border is drawn in a second color around it, at a width in screen pixels.
    /// </summary>
    /// <remarks>
    /// Four bytes per pixel, in the order of an XNA <c>SurfaceFormat.Color</c>. R is the icon's coverage and A its luma mask, both
    /// from the source icon shrunk by <c>scale</c> about the centre so its outer ring has room for a border inside the quad. G is the
    /// <see cref="LabelOutlineMap"/> ramp: 255 on the icon, falling linearly to 0 at <c>range</c> pixels away, so the shader can measure
    /// the distance from the icon in screen pixels. B repeats R.
    /// </remarks>
    public static class MarkerOutlineMap
    {
        /// <summary>The icon's size in the picture, as a fraction of the source, leaving room for the border round its outer ring.</summary>
        public const float DefaultScale = 0.85f;

        /// <summary>Pixels the G ramp runs over; more than the border is wide on screen at any marker size it is drawn at.</summary>
        public const int DefaultRange = 40;

        /// <summary>
        /// Builds the picture from <paramref name="source"/>, <paramref name="width"/> x <paramref name="height"/> RGBA bytes whose R is the
        /// icon's coverage and A its mask. Returns RGBA bytes of the same size, row major.
        /// </summary>
        public static byte[] Build(byte[] source, int width, int height, float scale = DefaultScale, int range = DefaultRange)
        {
            if (source is null || source.Length != width * height * 4)
                throw new ArgumentException($"A {width} x {height} picture needs {width * height * 4} bytes.", nameof(source));
            if (scale <= 0f || scale > 1f)
                throw new ArgumentOutOfRangeException(nameof(scale));

            byte[] coverage = new byte[width * height];
            byte[] mask = new byte[width * height];
            float cx = width / 2f;
            float cy = height / 2f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // The source pixel this one shows, at the shrunk icon's scale about the centre
                    float sx = cx + (x + 0.5f - cx) / scale - 0.5f;
                    float sy = cy + (y + 0.5f - cy) / scale - 0.5f;
                    coverage[y * width + x] = Sample(source, width, height, sx, sy, 0);
                    mask[y * width + x] = Sample(source, width, height, sx, sy, 3);
                }
            }

            byte[] packed = LabelOutlineMap.Build(coverage, width, height, range);
            for (int i = 0; i < coverage.Length; i++)
            {
                packed[i * 4 + 2] = coverage[i];
                packed[i * 4 + 3] = mask[i];
            }

            return packed;
        }

        /// <summary>Bilinear sample of one channel; 0 outside the picture.</summary>
        private static byte Sample(byte[] rgba, int width, int height, float x, float y, int channel)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float fx = x - x0;
            float fy = y - y0;
            float top = Lerp(Texel(rgba, width, height, x0, y0, channel), Texel(rgba, width, height, x0 + 1, y0, channel), fx);
            float bottom = Lerp(Texel(rgba, width, height, x0, y0 + 1, channel), Texel(rgba, width, height, x0 + 1, y0 + 1, channel), fx);
            return (byte)Math.Round(Lerp(top, bottom, fy));
        }

        private static float Texel(byte[] rgba, int width, int height, int x, int y, int channel) =>
            x < 0 || y < 0 || x >= width || y >= height ? 0f : rgba[(y * width + x) * 4 + channel];

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
