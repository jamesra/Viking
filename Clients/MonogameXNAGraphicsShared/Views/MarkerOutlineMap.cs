using System;

namespace VikingXNAGraphics
{
    /// <summary>
    /// The picture a single-colour icon marker (the arrows marking a link to an overlapped location) is drawn from when it gets a
    /// contrasting border like a label's (<see cref="OverlayShaderEffect.Techniques.CircleSingleColorTextureLumaOutlinedOverlayEffect"/>).
    /// The icon is still drawn in any color; the border is drawn in a second color around it, at a width in screen pixels.
    /// </summary>
    /// <remarks>
    /// Four bytes per pixel, in the order of an XNA <c>SurfaceFormat.Color</c>, all from the source icon shrunk by <c>scale</c> about the
    /// centre so its outer ring has room for a border inside the quad. G is the icon's signed distance field:
    /// <c>255 * (0.5 - distance / (2 * range))</c>, where the distance from the icon's edge is in pixels, negative inside, and
    /// <c>range = width * <see cref="DistanceRange"/></c>; so G is 128 on the edge, above it inside and 0 from <c>range</c> pixels
    /// outside. The shader draws the icon's edge and the border from G alone, antialiased in screen pixels at any size, and nothing where
    /// both are empty, so the gaps inside the icon stay transparent. The edge is found at sub-pixel precision by measuring on a picture
    /// twice the size, which keeps it smooth when the marker is drawn larger than the picture. R and B are the icon's coverage and A its
    /// luma mask.
    /// </remarks>
    public static class MarkerOutlineMap
    {
        /// <summary>The icon's size in the picture, as a fraction of the source, leaving room for the border round its outer ring.</summary>
        public const float DefaultScale = 0.85f;

        /// <summary>
        /// The distance G spans either side of the edge, as a fraction of the picture's width (the shader's <c>markerDistanceRange</c>).
        /// 1/16 is 32 pixels of a 512 picture: room for the border on a marker as small as 40 screen pixels, at four steps of G a pixel.
        /// </summary>
        public const float DistanceRange = 1f / 16f;

        /// <summary>Times finer than the picture the edge is measured at.</summary>
        private const int Supersample = 2;

        /// <summary>A coverage at or above this counts as inside the icon.</summary>
        private const byte InsideThreshold = 128;

        /// <summary>
        /// Builds the picture from <paramref name="source"/>, <paramref name="width"/> x <paramref name="height"/> RGBA bytes whose R is the
        /// icon's coverage and A its mask. Returns RGBA bytes of the same size, row major.
        /// </summary>
        public static byte[] Build(byte[] source, int width, int height, float scale = DefaultScale)
        {
            if (source is null || source.Length != width * height * 4)
                throw new ArgumentException($"A {width} x {height} picture needs {width * height * 4} bytes.", nameof(source));
            if (scale <= 0f || scale > 1f)
                throw new ArgumentOutOfRangeException(nameof(scale));

            float[] distance = SignedDistance(source, width, height, scale);
            float range = width * DistanceRange;
            byte[] packed = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    float sx = SourceCoordinate(x, width, scale, 1);
                    float sy = SourceCoordinate(y, height, scale, 1);
                    byte coverage = Sample(source, width, height, sx, sy, 0);
                    float ramp = 0.5f - distance[i] / (2f * range);
                    packed[i * 4] = coverage;
                    packed[i * 4 + 1] = (byte)Math.Round(Math.Min(Math.Max(ramp, 0f), 1f) * 255f);
                    packed[i * 4 + 2] = coverage;
                    packed[i * 4 + 3] = Sample(source, width, height, sx, sy, 3);
                }
            }

            return packed;
        }

        /// <summary>
        /// Per pixel of the picture, the distance in its pixels from the shrunk icon's edge, negative inside: measured on a picture
        /// <see cref="Supersample"/> times finer and averaged back, so the edge lies between pixels rather than on them.
        /// </summary>
        private static float[] SignedDistance(byte[] source, int width, int height, float scale)
        {
            int fineWidth = width * Supersample;
            int fineHeight = height * Supersample;
            byte[] inside = new byte[fineWidth * fineHeight];
            byte[] outside = new byte[inside.Length];
            for (int y = 0; y < fineHeight; y++)
            {
                float sy = SourceCoordinate(y, fineHeight, scale, Supersample);
                for (int x = 0; x < fineWidth; x++)
                {
                    bool isInside = Sample(source, width, height, SourceCoordinate(x, fineWidth, scale, Supersample), sy, 0) >= InsideThreshold;
                    inside[y * fineWidth + x] = isInside ? (byte)255 : (byte)0;
                    outside[y * fineWidth + x] = isInside ? (byte)0 : (byte)255;
                }
            }

            float[] toInside = LabelOutlineMap.DistanceToInside(inside, fineWidth, fineHeight);
            float[] toOutside = LabelOutlineMap.DistanceToInside(outside, fineWidth, fineHeight);

            float[] distance = new float[width * height];
            for (int y = 0; y < fineHeight; y++)
            {
                for (int x = 0; x < fineWidth; x++)
                {
                    int i = y * fineWidth + x;
                    // Distances run between pixel centres; the edge lies half a pixel from the last pixel on either side of it
                    float signed = inside[i] != 0 ? 0.5f - toOutside[i] : toInside[i] - 0.5f;
                    distance[(y / Supersample) * width + x / Supersample] += signed / (Supersample * Supersample * Supersample);
                }
            }

            return distance;
        }

        /// <summary>
        /// The source coordinate, in source pixels, that pixel <paramref name="index"/> of a picture <paramref name="size"/> pixels across
        /// (<paramref name="fineness"/> times the source's) shows, with the icon shrunk by <paramref name="scale"/> about the centre.
        /// </summary>
        private static float SourceCoordinate(int index, int size, float scale, int fineness)
        {
            float centre = size / 2f;
            return (centre + (index + 0.5f - centre) / scale) / fineness - 0.5f;
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
