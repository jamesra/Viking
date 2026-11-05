using Google.Protobuf;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.gRPC.Geometry.V1;
using Viking.VolumeModel;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;
using Vector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Redraws a <c>SegmentLocation</c> result, whose logits lie on the mosaic grid at downsample 1, on the volume grid the
    /// viewer draws in, so the overlay, retrace and merge code, which all work in viewer space, take it as they take a
    /// <c>SegmentTilesStream</c> result. Each volume sample is looked up in the mosaic through the section's transform and
    /// interpolated bilinearly from the four nearest mosaic samples; a sample outside the mosaic field or the transform holds
    /// the field's lowest logit, which is outside the object. The samples keep the mosaic field's pixel scale.
    /// </summary>
    internal static class SegmentationFieldResampler
    {
        /// <summary>
        /// Largest side of a resampled field, in samples. A transform that stretches a field this far is broken there, and the
        /// island is dropped rather than allocated.
        /// </summary>
        private const int MaxSide = 4096;

        /// <summary>Points per side of the mosaic footprint mapped to find its volume bounds; the transform can bend the sides.</summary>
        private const int EdgeSteps = 16;

        /// <summary>
        /// A copy of <paramref name="mosaic"/> with every island's logits on the volume grid. An island whose footprint does not
        /// map is left out, as an island that found nothing is.
        /// </summary>
        public static SegmentationServiceTypes.SegmentationResponse ToVolume(
            SegmentationServiceTypes.SegmentationResponse mosaic,
            IVolumeToSectionTransform transform)
        {
            if (mosaic is null)
                throw new ArgumentNullException(nameof(mosaic));
            if (transform is null)
                throw new ArgumentNullException(nameof(transform));

            SegmentationServiceTypes.SegmentationResponse volume = mosaic.Clone();
            volume.Segments.Clear();
            foreach (SegmentationServiceTypes.SegmentResult segment in mosaic.Segments)
            {
                if (TryResample(segment, transform) is SegmentationServiceTypes.SegmentResult resampled)
                    volume.Segments.Add(resampled);
            }

            return volume;
        }

        /// <summary>One island's logits on the volume grid; null when it has none or its footprint does not map.</summary>
        internal static SegmentationServiceTypes.SegmentResult? TryResample(
            SegmentationServiceTypes.SegmentResult segment,
            IVolumeToSectionTransform transform)
        {
            if (!SegmentationLogitField.HasLogits(segment))
                return null;

            int scale = SegmentationLogitField.PixelScale(segment);
            int width = segment.LogitsSize.Width;
            int height = segment.LogitsSize.Height;
            float[] logits = Decode(segment.Logits, width * height);

            // The footprint in mosaic pixels: origin is the top-left pixel, and pixel (x, y) covers [x, x + 1) x [y, y + 1).
            double left = segment.LogitsOrigin.X;
            double top = segment.LogitsOrigin.Y + 1.0;
            double right = left + ((double)width * scale);
            double bottom = top - ((double)height * scale);

            List<Vector2> outline = [];
            for (int step = 0; step < EdgeSteps; step++)
            {
                double t = step / (double)EdgeSteps;
                outline.Add(new Vector2(left + (t * (right - left)), bottom));
                outline.Add(new Vector2(right, bottom + (t * (top - bottom))));
                outline.Add(new Vector2(right - (t * (right - left)), top));
                outline.Add(new Vector2(left, top - (t * (top - bottom))));
            }

            bool[] outlineMapped = LocationVolumeGeometry.TryMapPoints(outline, transform, out Vector2[] volumeOutline);
            Vector2[] mapped = [.. volumeOutline.Where((_, index) => outlineMapped[index])];
            if (mapped.Length < 2)
                return null;

            int outLeft = (int)Math.Floor(mapped.Min(point => point.X));
            int outTop = (int)Math.Ceiling(mapped.Max(point => point.Y));
            int outWidth = Math.Max(1, (int)Math.Ceiling((mapped.Max(point => point.X) - outLeft) / scale));
            int outHeight = Math.Max(1, (int)Math.Ceiling((outTop - mapped.Min(point => point.Y)) / scale));
            if (outWidth > MaxSide || outHeight > MaxSide)
                return null;

            Vector2[] centres = new Vector2[outWidth * outHeight];
            for (int row = 0; row < outHeight; row++)
            {
                for (int column = 0; column < outWidth; column++)
                    centres[(row * outWidth) + column] = new Vector2(outLeft + ((column + 0.5) * scale), outTop - ((row + 0.5) * scale));
            }

            bool[] centreMapped = LocationVolumeGeometry.TryMapEachPointToMosaic(centres, transform, out Vector2[] mosaicCentres);
            float outside = logits.Min();
            float[] resampled = new float[centres.Length];
            for (int i = 0; i < resampled.Length; i++)
            {
                resampled[i] = centreMapped[i]
                    ? Sample(logits, width, height, ((mosaicCentres[i].X - left) / scale) - 0.5, ((top - mosaicCentres[i].Y) / scale) - 0.5, outside)
                    : outside;
            }

            SegmentationServiceTypes.SegmentResult result = segment.Clone();
            result.Logits = ByteString.CopyFrom(Encode(resampled));
            result.LogitsSize = new IntSize { Width = outWidth, Height = outHeight };
            result.LogitsOrigin = new IntPoint { X = outLeft, Y = outTop - 1 };
            return result;
        }

        /// <summary>
        /// The logit at fractional sample (<paramref name="column"/>, <paramref name="row"/>), sample centres at whole numbers,
        /// interpolated bilinearly; <paramref name="outside"/> beyond the field's outer sample edges.
        /// </summary>
        internal static float Sample(float[] logits, int width, int height, double column, double row, float outside)
        {
            if (column < -0.5 || row < -0.5 || column > width - 0.5 || row > height - 0.5)
                return outside;

            double x = Math.Max(0, Math.Min(width - 1, column));
            double y = Math.Max(0, Math.Min(height - 1, row));
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(width - 1, x0 + 1);
            int y1 = Math.Min(height - 1, y0 + 1);
            double fx = x - x0;
            double fy = y - y0;
            double upper = (logits[(y0 * width) + x0] * (1 - fx)) + (logits[(y0 * width) + x1] * fx);
            double lower = (logits[(y1 * width) + x0] * (1 - fx)) + (logits[(y1 * width) + x1] * fx);
            return (float)((upper * (1 - fy)) + (lower * fy));
        }

        private static float[] Decode(ByteString bytes, int count)
        {
            byte[] half = bytes.ToByteArray();
            float[] logits = new float[count];
            for (int i = 0; i < count; i++)
                logits[i] = SegmentationLogitField.HalfToSingle((ushort)(half[2 * i] | (half[(2 * i) + 1] << 8)));

            return logits;
        }

        private static byte[] Encode(float[] logits)
        {
            byte[] half = new byte[logits.Length * 2];
            for (int i = 0; i < logits.Length; i++)
            {
                ushort bits = SegmentationLogitField.SingleToHalf(logits[i]);
                half[2 * i] = (byte)(bits & 0xFF);
                half[(2 * i) + 1] = (byte)(bits >> 8);
            }

            return half;
        }
    }
}
