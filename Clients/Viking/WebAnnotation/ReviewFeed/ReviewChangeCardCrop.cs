using System;

namespace WebAnnotation.ReviewFeed
{
    /// <summary>
    /// Shared padded square crop window (Y-up) for Review card EM tiles and shape masks, in the
    /// entry's coordinate space (volume or mosaic). <see cref="ReviewChangeMaskComposer"/> and
    /// <see cref="ReviewChangeMosaicCropLoader"/> both map through one window so the overlay lines
    /// up with the EM. One span serves both axes, so mask and EM stay isotropic (no X/Y stretch).
    /// </summary>
    public readonly struct ReviewChangeCardCrop
    {
        /// <summary>Fraction of the larger bbox side added on each edge.</summary>
        public const double PadFraction = 0.15;

        /// <summary>World X of the window's left edge.</summary>
        public double Left { get; }

        /// <summary>World Y of the window's bottom edge.</summary>
        public double Bottom { get; }

        /// <summary>Window width and height in world units.</summary>
        public double Span { get; }

        ReviewChangeCardCrop(double left, double bottom, double span)
        {
            Left = left;
            Bottom = bottom;
            Span = span;
        }

        /// <summary>The window as a world rectangle, for tile queries.</summary>
        public Geometry.Rectangle Bounds => new(Left, Left + Span, Bottom, Bottom + Span);

        /// <summary>Padded window around <paramref name="entry"/>'s bbox.</summary>
        public static ReviewChangeCardCrop For(ReviewChangeEntry entry)
        {
            if (entry is null)
                throw new ArgumentNullException(nameof(entry));

            return FromBounds(entry.MinX, entry.MinY, entry.MaxX, entry.MaxY);
        }

        /// <summary>
        /// Padded square window around an axis-aligned bbox (Y-up). A zero-size side counts as
        /// 1e-6 so a point still gets a non-empty window.
        /// </summary>
        public static ReviewChangeCardCrop FromBounds(double minX, double minY, double maxX, double maxY)
        {
            double width = Math.Max(maxX - minX, 1e-6);
            double height = Math.Max(maxY - minY, 1e-6);
            double pad = Math.Max(width, height) * PadFraction;
            double span = Math.Max(width, height) + 2 * pad;
            double centerX = (minX + maxX) * 0.5;
            double centerY = (minY + maxY) * 0.5;
            return new ReviewChangeCardCrop(centerX - span * 0.5, centerY - span * 0.5, span);
        }

        /// <summary>
        /// Maps a world point (Y-up) into a <paramref name="size"/>-pixel card (Y-down, origin top-left).
        /// The math stays in double; only the pixel result narrows to float for GDI.
        /// </summary>
        public void WorldToImage(double worldX, double worldY, int size, out float imageX, out float imageY)
        {
            imageX = (float)((worldX - Left) / Span * size);
            imageY = (float)(size - (worldY - Bottom) / Span * size);
        }

        /// <summary>
        /// Pixel rectangle covering a world rectangle on the card: corners mapped through
        /// <see cref="WorldToImage"/>, floored at the top-left and ceiled to at least one pixel across.
        /// </summary>
        public System.Drawing.Rectangle WorldRectToImageRect(Geometry.Rectangle world, int size)
        {
            WorldToImage(world.Left, world.Top, size, out float x0, out float yTop);
            WorldToImage(world.Right, world.Bottom, size, out float x1, out float yBottom);

            float left = Math.Min(x0, x1);
            float top = Math.Min(yTop, yBottom);
            float width = Math.Abs(x1 - x0);
            float height = Math.Abs(yBottom - yTop);
            return new System.Drawing.Rectangle(
                (int)Math.Floor(left),
                (int)Math.Floor(top),
                Math.Max(1, (int)Math.Ceiling(width)),
                Math.Max(1, (int)Math.Ceiling(height)));
        }
    }
}
