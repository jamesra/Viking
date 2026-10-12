using System;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// One decoded segment as a reduced-size soft field plus where it sits in the response's frame.
    /// </summary>
    /// <param name="Data">
    /// Soft bytes, 0–255, with <see cref="SegmentationMaskPolygonizer.SoftMaskIsoLevel"/> as the object boundary.
    /// Smoothed for tracing. Null when the segment carried nothing decodable.
    /// </param>
    /// <param name="Width">Samples across.</param>
    /// <param name="Height">Samples down.</param>
    /// <param name="Scale">Image pixels per sample: 1 for a full-resolution mask, 4 for SAM2's native logit grid.</param>
    /// <param name="X">Image-pixel X of the field's top-left, in the frame <see cref="SegmentationServiceTypes.SegmentResult.X"/> uses.</param>
    /// <param name="Y">Image-pixel Y of the field's top-left.</param>
    /// <param name="ServerData">
    /// The same grid as <paramref name="Data"/> with the logits exactly as the server sent them (no smoothing), so the
    /// mask overlay shows the server's mask. Null for a mask decoded from a PNG, which has nothing to smooth.
    /// </param>
    internal sealed record SegmentField(byte[]? Data, int Width, int Height, int Scale, int X, int Y, byte[]? ServerData = null)
    {
        /// <summary>Width of the field in image pixels.</summary>
        public int PixelWidth => Width * Scale;

        /// <summary>Height of the field in image pixels.</summary>
        public int PixelHeight => Height * Scale;
    }

    /// <summary>
    /// Turns the raw SAM2 logits a server sends with <c>ResponseContent.Logits | NoUpscale</c> into the soft byte field the
    /// rest of the segmentation code already traces, without upscaling.
    /// </summary>
    /// <remarks>
    /// The logits stay at SAM2's native resolution (four image pixels per sample). Marching squares runs on them
    /// there, so each ring vertex sits where the logit really crosses the iso level, and the world transform that
    /// follows is what scales the ring up. A byte is <c>127.5 + (logit - iso) * <see cref="SoftBytesPerLogit"/></c>,
    /// clamped: the boundary stays at 127.5 and the slope is linear around it, so linear interpolation between two
    /// bytes finds the same crossing as interpolating the logits.
    /// </remarks>
    internal static class SegmentationLogitField
    {
        /// <summary>
        /// Bytes per logit. Plus or minus six logits, a confident SAM2 answer, spans the whole byte range.
        /// </summary>
        public const float SoftBytesPerLogit = 21.25f;

        /// <summary>
        /// Standard deviation, in logit samples (four pixels each), of the smoothing applied before the field is
        /// traced. SAM2's logits hover around the threshold along an edge, so contouring them raw leaves one-sample
        /// holes and islands (63 and 80 in one real capture, none deeper than 0.5 logit) and a zigzag one sample
        /// long. 0.75 removed 97% of them in captures while moving the inside area by about 2%, but it moved the
        /// contour outward by a quarter pixel and split thin necks in 2 of 40 captures; 0.5 split none. The value
        /// in use is the <c>SegmentationSmoothingSigma</c> preference; this is its default.
        /// </summary>
        public const double DefaultSmoothingSigma = 0.5;

        /// <summary>
        /// True when <paramref name="segment"/> carries logits that match its declared size.
        /// </summary>
        public static bool HasLogits(SegmentationServiceTypes.SegmentResult segment) =>
            segment.LogitsWidth > 0 &&
            segment.LogitsHeight > 0 &&
            segment.Logits.Length == 2L * segment.LogitsWidth * segment.LogitsHeight;

        /// <summary>
        /// Image pixels per logit sample for <paramref name="segment"/>, 1 when it has no logits.
        /// </summary>
        public static int PixelScale(SegmentationServiceTypes.SegmentResult segment) =>
            HasLogits(segment) && segment.LogitsScale >= 1f ? (int)Math.Round(segment.LogitsScale) : 1;

        /// <summary>
        /// The segment's logits as a soft byte field at their own resolution, or null data when it has none.
        /// </summary>
        /// <param name="segment">A segment from a response that asked for logits.</param>
        /// <param name="iso">The logit the server thresholded at; it becomes the boundary at byte 127.5.</param>
        /// <param name="sigma">Smoothing, in logit samples, applied to the traced field; 0 or less leaves it as sent.</param>
        public static SegmentField Decode(SegmentationServiceTypes.SegmentResult segment, double iso, double sigma = DefaultSmoothingSigma)
        {
            if (!HasLogits(segment))
                return new SegmentField(null, 0, 0, 1, segment.X, segment.Y);

            int width = segment.LogitsWidth;
            int height = segment.LogitsHeight;
            byte[] half = segment.Logits.ToByteArray();
            float[] logits = new float[width * height];
            for (int i = 0; i < logits.Length; i++)
                logits[i] = HalfToSingle((ushort)(half[2 * i] | (half[(2 * i) + 1] << 8)));

            byte[] server = new byte[logits.Length];
            for (int i = 0; i < server.Length; i++)
                server[i] = ToSoftByte(logits[i], iso);

            float[] smoothed = Smooth(logits, width, height, sigma);
            byte[] soft = ReferenceEquals(smoothed, logits) ? server : new byte[logits.Length];
            if (!ReferenceEquals(soft, server))
            {
                for (int i = 0; i < soft.Length; i++)
                    soft[i] = ToSoftByte(smoothed[i], iso);
            }

            return new SegmentField(soft, width, height, PixelScale(segment), segment.LogitsX, segment.LogitsY, server);
        }

        /// <summary>
        /// A separable Gaussian blur of a logit grid. Samples past the edge repeat the nearest one, so the crop's
        /// border of background samples stays background. A non-finite sample is treated as the lowest logit
        /// so it cannot spread. <paramref name="sigma"/> of 0 or less returns the input.
        /// </summary>
        internal static float[] Smooth(float[] logits, int width, int height, double sigma)
        {
            if (sigma <= 0 || width <= 0 || height <= 0 || logits.Length != width * height)
                return logits;

            int radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
            float[] kernel = new float[(2 * radius) + 1];
            double total = 0;
            for (int i = -radius; i <= radius; i++)
            {
                double weight = Math.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + radius] = (float)weight;
                total += weight;
            }

            for (int i = 0; i < kernel.Length; i++)
                kernel[i] = (float)(kernel[i] / total);

            float[] horizontal = new float[logits.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sx = Math.Min(width - 1, Math.Max(0, x + k));
                        float value = logits[row + sx];
                        sum += kernel[k + radius] * (float.IsNaN(value) ? -20f : value);
                    }

                    horizontal[row + x] = sum;
                }
            }

            float[] output = new float[logits.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++)
                    {
                        int sy = Math.Min(height - 1, Math.Max(0, y + k));
                        sum += kernel[k + radius] * horizontal[(sy * width) + x];
                    }

                    output[(y * width) + x] = sum;
                }
            }

            return output;
        }

        /// <summary>
        /// One logit as a soft byte around <paramref name="iso"/>. A non-finite logit is background.
        /// </summary>
        internal static byte ToSoftByte(float logit, double iso)
        {
            if (float.IsNaN(logit))
                return 0;

            double value = 127.5 + ((logit - iso) * SoftBytesPerLogit);
            if (value <= 0)
                return 0;
            if (value >= 255)
                return 255;

            return (byte)Math.Round(value);
        }

        private static readonly float[] ExponentScale = BuildExponentScale();

        private static float[] BuildExponentScale()
        {
            float[] scale = new float[32];
            for (int exponent = 0; exponent < scale.Length; exponent++)
                scale[exponent] = (float)Math.Pow(2, exponent - 15);

            return scale;
        }

        /// <summary>
        /// IEEE 754 half-precision to single. .NET Framework has no <c>System.Half</c>.
        /// </summary>
        internal static float HalfToSingle(ushort bits)
        {
            int sign = (bits >> 15) & 1;
            int exponent = (bits >> 10) & 0x1F;
            int mantissa = bits & 0x3FF;
            float magnitude;
            if (exponent == 0)
                magnitude = mantissa / 1024f * ExponentScale[1];
            else if (exponent == 31)
                magnitude = mantissa == 0 ? float.PositiveInfinity : float.NaN;
            else
                magnitude = (1f + (mantissa / 1024f)) * ExponentScale[exponent];

            return sign == 1 ? -magnitude : magnitude;
        }
    }
}
