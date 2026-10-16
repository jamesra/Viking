#nullable enable

using System;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Pure arithmetic behind <see cref="SegmentationMaskEditCommand"/>: a signed distance field of a bitmap mask, from
    /// which any dilation or erosion by a radius is one comparison per pixel.
    /// </summary>
    /// <remarks>
    /// The field is computed once per mask (two exact Euclidean distance transforms). Growing or shrinking the mask by
    /// <c>r</c> pixels is then <c>field &lt;= r</c>, with no per-tick morphology, so a mouse-wheel tick costs one pass
    /// over the bitmap. Distances are measured between pixel centres and shifted by half a pixel so the zero level sits
    /// on the pixel edge: <c>r = 0</c> reproduces the mask exactly. Round (Euclidean) structuring element, so corners
    /// stay round when dilating. No threading state; all members are static.
    /// </remarks>
    internal static class SegmentationMaskEdit
    {
        /// <summary>A pixel at or above this value is inside the mask.</summary>
        public const byte Inside = 128;

        /// <summary>
        /// Signed distance in pixels from every pixel to the mask edge: negative inside, positive outside, magnitude at
        /// least 0.5. Null when no pixel is inside the mask.
        /// </summary>
        /// <param name="pixels">Row-major mask, value at or above <see cref="Inside"/> is inside.</param>
        public static float[]? SignedDistance(byte[] pixels, int width, int height)
        {
            bool[] inside = new bool[pixels.Length];
            bool any = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                inside[i] = pixels[i] >= Inside;
                any |= inside[i];
            }

            if (!any)
                return null;

            float[] toInside = SegmentationThinFeatureCut.SquaredDistanceTo(inside, width, height, seedValue: true);
            float[] toOutside = SegmentationThinFeatureCut.SquaredDistanceTo(inside, width, height, seedValue: false);
            float[] signed = new float[pixels.Length];
            for (int i = 0; i < signed.Length; i++)
            {
                signed[i] = inside[i]
                    ? -((float)Math.Sqrt(toOutside[i]) - 0.5f)
                    : (float)Math.Sqrt(toInside[i]) - 0.5f;
            }

            return signed;
        }

        /// <summary>
        /// Writes the mask grown by <paramref name="radiusPixels"/> (shrunk when negative) into <paramref name="destination"/>
        /// as 255 inside and 0 outside.
        /// </summary>
        public static void Threshold(float[] signedDistance, double radiusPixels, byte[] destination)
        {
            float limit = (float)radiusPixels;
            for (int i = 0; i < signedDistance.Length; i++)
                destination[i] = signedDistance[i] <= limit ? (byte)255 : (byte)0;
        }

        /// <summary>
        /// The most the mask can be shrunk (most negative radius) before it disappears: erosion by this radius leaves
        /// exactly its deepest pixels.
        /// </summary>
        public static double MinRadius(float[] signedDistance)
        {
            float min = 0;
            foreach (float value in signedDistance)
                min = Math.Min(min, value);

            return min;
        }

        /// <summary>
        /// The most the mask can be grown before it would reach the bitmap's edge and be clipped, measured from the
        /// nearest edge to the mask's bounding box. Zero when the mask already touches an edge.
        /// </summary>
        public static double MaxRadius(byte[] pixels, int width, int height)
        {
            int left = width, right = -1, top = height, bottom = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x] < Inside)
                        continue;

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (right < 0)
                return 0;

            int room = Math.Min(Math.Min(left, top), Math.Min(width - 1 - right, height - 1 - bottom));
            return Math.Max(0, room);
        }

        /// <summary>
        /// Radius change for <paramref name="notches"/> wheel notches (positive grows). A notch is
        /// <paramref name="stepWorld"/> volume units, but never less than one raster pixel (<paramref name="worldPerPixel"/>
        /// volume units) so a notch always moves the edge visibly; <paramref name="fine"/> takes a quarter of that.
        /// </summary>
        /// <returns>Raster pixels.</returns>
        public static double RadiusChange(double notches, double stepWorld, double worldPerPixel, bool fine)
        {
            double stepPixels = Math.Max(stepWorld / worldPerPixel, 1.0);
            return notches * stepPixels * (fine ? 0.25 : 1.0);
        }
    }
}
