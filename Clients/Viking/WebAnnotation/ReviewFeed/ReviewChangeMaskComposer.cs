using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Geometry;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Builds a gallery-style card bitmap: optional EM crop plus a shape mask
    /// tinted the same way as annotation-gallery <c>tintMaskedPixels</c>.
    /// Pure bitmap work; the Review tab supplies an EM background when tiles are cached.
    /// Mask and EM share <see cref="ReviewChangeCardCrop"/> so they stay aligned.
    /// </summary>
    public static class ReviewChangeMaskComposer
    {
        public const int DefaultSize = 256;

        /// <summary>Default overlay hue (gallery-style; not pure red).</summary>
        public static readonly Color DefaultTint = Color.FromArgb(255, 64, 160, 255);

        /// <summary>Gallery default visibility fraction (0–1).</summary>
        public const float DefaultVisibility = 0.55f;

        /// <summary>
        /// Fills a binary mask where the shape ring covers the padded crop. Used by unit tests
        /// and by <see cref="Compose"/>. Crop window matches <see cref="ReviewChangeMosaicCropLoader"/>.
        /// </summary>
        public static bool[,] RasterizeMask(
            IReadOnlyList<Vector2> shapeRing,
            double minX,
            double minY,
            double maxX,
            double maxY,
            int size)
        {
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));

            var mask = new bool[size, size];
            if (shapeRing is null || shapeRing.Count < 3)
                return mask;

            // Same padded window as the EM tile stitcher.
            ReviewChangeCardCrop.GetPaddedBounds(minX, minY, maxX, maxY, out double left, out double bottom, out double spanX, out double spanY);

            using var path = new GraphicsPath();
            var points = new PointF[shapeRing.Count];
            for (int i = 0; i < shapeRing.Count; i++)
            {
                Vector2 p = shapeRing[i];
                ReviewChangeCardCrop.WorldToImage(p.X, p.Y, left, bottom, spanX, spanY, size, out float x, out float y);
                points[i] = new PointF(x, y);
            }

            path.AddPolygon(points);

            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.None;
                g.FillPath(Brushes.White, path);
            }

            // LockBits instead of GetPixel — GetPixel is far too slow for 256² card masks.
            BitmapData data = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, size, size),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                int bytes = stride * size;
                var buffer = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bytes);
                for (int y = 0; y < size; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < size; x++)
                        mask[x, y] = buffer[row + x * 4 + 3] > 127;
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            return mask;
        }

        /// <summary>
        /// Blends tint into pixels where the mask is set. Matches gallery visibility blending
        /// (lerp toward tint by <paramref name="visibility"/>). Outside the mask stays as-is
        /// when <paramref name="clearOutside"/> is false; otherwise outside alpha is cleared.
        /// </summary>
        public static void TintMaskedPixels(
            byte[] bgra,
            int width,
            int height,
            bool[,] mask,
            Color tint,
            float visibility,
            bool clearOutside)
        {
            if (bgra is null)
                throw new ArgumentNullException(nameof(bgra));
            if (mask is null)
                throw new ArgumentNullException(nameof(mask));
            if (mask.GetLength(0) != width || mask.GetLength(1) != height)
                throw new ArgumentException("Mask size must match width/height.", nameof(mask));
            if (bgra.Length < width * height * 4)
                throw new ArgumentException("Pixel buffer too small.", nameof(bgra));

            float alpha = Math.Min(1f, Math.Max(0f, visibility));
            float tr = tint.R / 255f;
            float tg = tint.G / 255f;
            float tb = tint.B / 255f;

            int i = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[x, y])
                    {
                        if (clearOutside)
                            bgra[i + 3] = 0;
                        i += 4;
                        continue;
                    }

                    float b = bgra[i] / 255f;
                    float g = bgra[i + 1] / 255f;
                    float r = bgra[i + 2] / 255f;
                    bgra[i] = (byte)Math.Round(((1 - alpha) * b + alpha * tb) * 255);
                    bgra[i + 1] = (byte)Math.Round(((1 - alpha) * g + alpha * tg) * 255);
                    bgra[i + 2] = (byte)Math.Round(((1 - alpha) * r + alpha * tr) * 255);
                    bgra[i + 3] = 255;
                    i += 4;
                }
            }
        }

        /// <summary>
        /// Composes a card: dark (or supplied) EM background with a tinted mosaic mask on top.
        /// Caller disposes the returned bitmap.
        /// </summary>
        public static Bitmap Compose(
            ReviewChangeEntry entry,
            int size = DefaultSize,
            Bitmap emBackground = null,
            Color? tint = null,
            float visibility = DefaultVisibility)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size));

            var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.Clear(Color.FromArgb(255, 32, 32, 36));
                if (emBackground != null)
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(emBackground, 0, 0, size, size);
                }
            }

            if (entry.IsDeleted || entry.ShapeRing.Count < 3)
                return result;

            bool[,] mask = RasterizeMask(
                entry.ShapeRing,
                entry.MinX,
                entry.MinY,
                entry.MaxX,
                entry.MaxY,
                size);

            BitmapData data = result.LockBits(
                new System.Drawing.Rectangle(0, 0, size, size),
                ImageLockMode.ReadWrite,
                PixelFormat.Format32bppArgb);
            try
            {
                int bytes = Math.Abs(data.Stride) * size;
                var buffer = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, bytes);

                // LockBits may pad stride; tint only the tightly packed view we build.
                var packed = new byte[size * size * 4];
                for (int y = 0; y < size; y++)
                {
                    int src = y * data.Stride;
                    int dst = y * size * 4;
                    Buffer.BlockCopy(buffer, src, packed, dst, size * 4);
                }

                TintMaskedPixels(packed, size, size, mask, tint ?? DefaultTint, visibility, clearOutside: false);

                for (int y = 0; y < size; y++)
                {
                    int src = y * size * 4;
                    int dst = y * data.Stride;
                    Buffer.BlockCopy(packed, src, buffer, dst, size * 4);
                }

                System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, bytes);
            }
            finally
            {
                result.UnlockBits(data);
            }

            return result;
        }
    }
}
