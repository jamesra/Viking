using System;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Shared padded crop window for Review card EM tiles and shape masks.
    /// Both must use the same left/bottom/span so the overlay lines up with the EM.
    /// </summary>
    public static class ReviewChangeCardCrop
    {
        /// <summary>Fraction of the larger bbox side added on each edge.</summary>
        public const double PadFraction = 0.15;

        /// <summary>
        /// Padded axis-aligned window in the entry's coordinate space (volume or mosaic).
        /// </summary>
        public static void GetPaddedBounds(
            ReviewChangeEntry entry,
            out double left,
            out double bottom,
            out double spanX,
            out double spanY)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            GetPaddedBounds(entry.MinX, entry.MinY, entry.MaxX, entry.MaxY, out left, out bottom, out spanX, out spanY);
        }

        /// <summary>
        /// Padded square crop from an axis-aligned bbox (Y-up). Uses one world-units-per-pixel
        /// scale on both axes so mask and EM stay isotropic (no X/Y stretch mismatch).
        /// </summary>
        public static void GetPaddedBounds(
            double minX,
            double minY,
            double maxX,
            double maxY,
            out double left,
            out double bottom,
            out double spanX,
            out double spanY)
        {
            double width = Math.Max(maxX - minX, 1e-6);
            double height = Math.Max(maxY - minY, 1e-6);
            double pad = Math.Max(width, height) * PadFraction;
            double span = Math.Max(width, height) + 2 * pad;
            double centerX = (minX + maxX) * 0.5;
            double centerY = (minY + maxY) * 0.5;
            left = centerX - span * 0.5;
            bottom = centerY - span * 0.5;
            spanX = span;
            spanY = span;
        }

        /// <summary>
        /// Maps a world point (Y-up) into card pixel space (Y-down, origin top-left).
        /// </summary>
        public static void WorldToImage(
            double worldX,
            double worldY,
            double left,
            double bottom,
            double spanX,
            double spanY,
            int size,
            out float imageX,
            out float imageY)
        {
            imageX = (float)((worldX - left) / spanX * size);
            imageY = (float)(size - (worldY - bottom) / spanY * size);
        }
    }
}
