#nullable enable

using System;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Where a wire <c>Mask</c> sits and how coarse its PNG is: the inclusive mosaic-pixel bounds of the region, and
    /// <see cref="Scale"/>, the mosaic pixels per PNG pixel.
    /// </summary>
    /// <remarks>
    /// Mosaic pixels are at the tiles' downsample, X right and Y up. PNG pixel (c, r) stands for the
    /// <see cref="Scale"/> x <see cref="Scale"/> block whose low-x, high-y corner is
    /// (<see cref="XMin"/> + c * Scale, <see cref="YMax"/> - r * Scale); blocks at the right and bottom edges are clipped to the
    /// bounds, so the PNG is ceil(<see cref="Width"/> / Scale) x ceil(<see cref="Height"/> / Scale) pixels. The server
    /// reduces a mask to SAM2's 256 x 256 sample grid per window and only agrees exactly with a full-resolution mask when
    /// the block grid lines up with the window grid, so <see cref="ForRegion"/> snaps <see cref="XMin"/> and
    /// <see cref="YMax"/> + 1 outward to multiples of the scale (cell windows start at multiples of 512, which every
    /// power-of-two scale up to 512 divides).
    /// </remarks>
    internal readonly struct MaskLayout
    {
        public MaskLayout(int xMin, int yMax, int width, int height, int scale)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "A mask region has at least one pixel.");
            if (scale < 1)
                throw new ArgumentOutOfRangeException(nameof(scale));

            XMin = xMin;
            YMax = yMax;
            Width = width;
            Height = height;
            Scale = scale;
        }

        /// <summary>Mosaic pixel column of the left edge. A multiple of <see cref="Scale"/> when built by <see cref="ForRegion"/>.</summary>
        public int XMin { get; }

        /// <summary>Mosaic pixel row of the top edge (PNG row 0). <c>YMax + 1</c> is a multiple of <see cref="Scale"/> when built by <see cref="ForRegion"/>.</summary>
        public int YMax { get; }

        /// <summary>Region width in mosaic pixels, <c>XMax - XMin + 1</c>.</summary>
        public int Width { get; }

        /// <summary>Region height in mosaic pixels, <c>YMax - YMin + 1</c>.</summary>
        public int Height { get; }

        /// <summary>Mosaic pixels per PNG pixel along both axes; 1 means one PNG pixel per mosaic pixel.</summary>
        public int Scale { get; }

        /// <summary>Mosaic pixel column of the right edge, inclusive.</summary>
        public int XMax => XMin + Width - 1;

        /// <summary>Mosaic pixel row of the bottom edge, inclusive.</summary>
        public int YMin => YMax - Height + 1;

        /// <summary>PNG width: <c>ceil(Width / Scale)</c>.</summary>
        public int RasterWidth => (Width + Scale - 1) / Scale;

        /// <summary>PNG height: <c>ceil(Height / Scale)</c>.</summary>
        public int RasterHeight => (Height + Scale - 1) / Scale;

        /// <summary>
        /// The layout for the inclusive mosaic region at <paramref name="scale"/>, grown outward (never shrunk) so
        /// <see cref="XMin"/> is a multiple of <paramref name="scale"/> and <see cref="YMax"/> + 1 is a multiple of it. The
        /// right and bottom edges are left where they are.
        /// </summary>
        public static MaskLayout ForRegion(long xMin, long xMax, long yMin, long yMax, int scale)
        {
            if (xMax < xMin || yMax < yMin)
                throw new ArgumentException("The region is empty.");
            if (scale < 1)
                throw new ArgumentOutOfRangeException(nameof(scale));

            long snappedXMin = FloorToMultiple(xMin, scale);
            long snappedTop = CeilToMultiple(yMax + 1, scale);
            return new MaskLayout(
                checked((int)snappedXMin),
                checked((int)(snappedTop - 1)),
                checked((int)(xMax - snappedXMin + 1)),
                checked((int)(snappedTop - yMin)),
                scale);
        }

        private static long FloorToMultiple(long value, int multiple) => (long)Math.Floor(value / (double)multiple) * multiple;

        private static long CeilToMultiple(long value, int multiple) => (long)Math.Ceiling(value / (double)multiple) * multiple;
    }

    /// <summary>
    /// How a mask region is sent: at full resolution when it fits one SAM2 window, otherwise as coarse blocks.
    /// </summary>
    /// <remarks>
    /// Full resolution keeps the mask edge exact for the encodings that read edge distances. A coarse mask turns the
    /// edge into blocks and the server pins every pixel of a set block as object, so it is used only for regions too big to
    /// send whole. The server has no side limit on the PNG beyond <see cref="MaxBoundsSide"/>'s pixel budget.
    /// </remarks>
    internal static class MaskScalePolicy
    {
        /// <summary>Side of one SAM2 window in mosaic pixels. A region within this is sent at full resolution.</summary>
        public const int SingleWindowSide = SegmentationMaskUnion.WindowSize;

        /// <summary>Mosaic pixels per PNG pixel for a region larger than one window. Divides the 512-pixel cell grid.</summary>
        public const int LargeRegionScale = 4;

        /// <summary>Largest PNG side the client sends; the scale doubles until the PNG fits.</summary>
        public const int MaxPngSide = 4096;

        /// <summary>
        /// Largest bounds side the client sends, in mosaic pixels. The server refuses bounds above 1 &lt;&lt; 26 pixels
        /// (8192 x 8192); this leaves room for the outward snap.
        /// </summary>
        public const int MaxBoundsSide = 8000;

        /// <summary>
        /// The layout for the inclusive mosaic region, with <paramref name="marginRasterPixels"/> free on every side
        /// (room to dilate an edited mask).
        /// </summary>
        /// <remarks>
        /// Scale 1 when the region and its margin fit one window; otherwise <see cref="LargeRegionScale"/>, doubled while the
        /// raster would exceed <paramref name="maxRasterSide"/> (<see cref="MaxPngSide"/> for a PNG on the wire; a raster that is
        /// drawn as a texture passes the GPU's limit, so a mask too big for it is simply downsampled until it fits). The margin is
        /// added after the scale is known, so it is always that many raster pixels.
        /// </remarks>
        public static MaskLayout Plan(long xMin, long xMax, long yMin, long yMax, int marginRasterPixels = 0, int maxRasterSide = MaxPngSide)
        {
            long width = xMax - xMin + 1 + 2L * marginRasterPixels;
            long height = yMax - yMin + 1 + 2L * marginRasterPixels;
            int scale = width <= SingleWindowSide && height <= SingleWindowSide ? 1 : LargeRegionScale;
            while (true)
            {
                long margin = (long)marginRasterPixels * scale;
                MaskLayout layout = MaskLayout.ForRegion(xMin - margin, xMax + margin, yMin - margin, yMax + margin, scale);
                if (layout.RasterWidth <= maxRasterSide && layout.RasterHeight <= maxRasterSide)
                    return layout;

                scale *= 2;
            }
        }
    }
}
