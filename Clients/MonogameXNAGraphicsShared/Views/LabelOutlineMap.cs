using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// The picture a label is cached as when it is drawn with the outline shader (<see cref="LabelOutlineEffect"/>): per pixel, the
    /// text's coverage and how far the pixel lies outside the text, so the shader can colour the text, a border of any width and a soft
    /// shadow at draw time, at a width in screen pixels however far the label is zoomed.
    /// </summary>
    /// <remarks>
    /// Four bytes per pixel, in the order of an XNA <c>SurfaceFormat.Color</c>: R is the text's coverage (255 inside the glyphs, antialiased
    /// at their edges), G is <c>255 * (1 - distance / range)</c> where the distance is in pixels from the glyphs' edge, so it is 255 on the
    /// text and falls linearly to 0 at <c>range</c> pixels away, B is unused and A is 255. The picture carries <c>range</c> blank pixels around
    /// the text so the fall-off is never cut off. The shader reads the slope of G per screen pixel from the screen-space derivatives of G, which
    /// is why G must be a clean linear ramp.
    /// </remarks>
    public static class LabelOutlineMap
    {
        /// <summary>Pixels of padding around the text, which is also the largest distance the picture can represent.</summary>
        public const int DefaultRange = 24;

        /// <summary>A coverage at or above this counts as inside the text when measuring distance.</summary>
        private const byte InsideThreshold = 128;

        /// <summary>
        /// Builds the picture from <paramref name="coverage"/>, <paramref name="width"/> x <paramref name="height"/> bytes (0 outside the text,
        /// 255 inside), which must already include <paramref name="range"/> blank pixels on every side. Returns RGBA bytes, row major.
        /// </summary>
        public static byte[] Build(byte[] coverage, int width, int height, int range = DefaultRange)
        {
            if (coverage is null || coverage.Length != width * height)
                throw new ArgumentException($"A {width} x {height} picture needs {width * height} coverage bytes.", nameof(coverage));
            if (range < 1)
                throw new ArgumentOutOfRangeException(nameof(range));

            float[] outside = DistanceToInside(coverage, width, height);
            byte[] packed = new byte[width * height * 4];
            for (int i = 0; i < coverage.Length; i++)
            {
                // The distance is between pixel centres; an antialiased edge pixel sits half a pixel inside the edge it borders.
                float distance = coverage[i] >= InsideThreshold ? 0f : Math.Max(0f, outside[i] - 0.5f);
                float ramp = 1f - Math.Min(distance, range) / range;
                int offset = i * 4;
                packed[offset] = coverage[i];
                packed[offset + 1] = (byte)Math.Round(ramp * 255f);
                packed[offset + 2] = 0;
                packed[offset + 3] = 255;
            }

            return packed;
        }

        /// <summary>
        /// Euclidean distance in pixels from every pixel to the nearest pixel inside the text (0 for those inside), by the exact
        /// two-pass squared-distance transform. A picture with nothing inside gets a large value everywhere.
        /// </summary>
        public static float[] DistanceToInside(byte[] coverage, int width, int height)
        {
            const float Far = 1e9f;
            float[] squared = new float[width * height];
            for (int i = 0; i < squared.Length; i++)
                squared[i] = coverage[i] >= InsideThreshold ? 0f : Far;

            int longest = Math.Max(width, height);
            float[] f = new float[longest];
            float[] d = new float[longest];
            int[] v = new int[longest];
            float[] z = new float[longest + 1];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    f[y] = squared[y * width + x];

                Transform1D(f, d, v, z, height);
                for (int y = 0; y < height; y++)
                    squared[y * width + x] = d[y];
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    f[x] = squared[y * width + x];

                Transform1D(f, d, v, z, width);
                for (int x = 0; x < width; x++)
                    squared[y * width + x] = d[x];
            }

            for (int i = 0; i < squared.Length; i++)
                squared[i] = (float)Math.Sqrt(squared[i]);

            return squared;
        }

        /// <summary>One dimension of the Felzenszwalb-Huttenlocher squared-distance transform: the lower envelope of parabolas rooted at each sample.</summary>
        private static void Transform1D(float[] f, float[] d, int[] v, float[] z, int n)
        {
            int k = 0;
            v[0] = 0;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                float s;
                while (true)
                {
                    int p = v[k];
                    s = ((f[q] + q * (float)q) - (f[p] + p * (float)p)) / (2f * q - 2f * p);
                    if (s <= z[k] && k > 0)
                    {
                        k--;
                        continue;
                    }

                    break;
                }

                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q)
                    k++;

                int p = v[k];
                d[q] = (q - p) * (float)(q - p) + f[p];
            }
        }
    }
}
