#nullable enable

using System;
using System.Collections.Generic;
using Geometry;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// A rectangular bitmap mask placed in volume space: the input and output of <see cref="SegmentationMaskEditCommand"/>.
    /// </summary>
    /// <remarks>
    /// Same placement as the wire mask built by <see cref="SegmentationMaskUnion"/> (see <see cref="MaskLayout"/>): raster
    /// pixel (column, row) stands for the <see cref="Scale"/> x <see cref="Scale"/> block of mosaic pixels whose low-x, high-y
    /// corner is (<see cref="XMin"/> + column * Scale, <see cref="YMax"/> - row * Scale); a mosaic pixel is
    /// <see cref="Downsample"/> volume units wide and rows run downwards (Y up in the volume). Pixel value 255 is inside the
    /// mask and 0 outside. The raster is <see cref="Width"/> x <see cref="Height"/> pixels of any size: scale 1 for a region
    /// that fits one SAM2 window, coarser blocks for a large region (see <see cref="MaskScalePolicy"/>). Immutable once
    /// built; <see cref="Pixels"/> must not be modified by callers.
    /// </remarks>
    internal sealed class RasterMask
    {
        /// <param name="downsample">Volume units per mosaic pixel, the tiles' downsample.</param>
        /// <param name="xMin">Mosaic pixel column of the left edge.</param>
        /// <param name="yMax">Mosaic pixel row of the top edge (row 0).</param>
        /// <param name="width">Raster width in pixels.</param>
        /// <param name="height">Raster height in pixels.</param>
        /// <param name="pixels"><paramref name="width"/> x <paramref name="height"/> bytes, 255 inside the mask.</param>
        /// <param name="scale">Mosaic pixels per raster pixel; 1 for full resolution.</param>
        public RasterMask(int downsample, int xMin, int yMax, int width, int height, byte[] pixels, int scale = 1)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "A mask has at least one pixel.");
            if (scale < 1)
                throw new ArgumentOutOfRangeException(nameof(scale));
            if (pixels is null || pixels.Length != (long)width * height)
                throw new ArgumentException($"A mask is {width} x {height} pixels.", nameof(pixels));

            Downsample = downsample;
            XMin = xMin;
            YMax = yMax;
            Width = width;
            Height = height;
            Scale = scale;
            Pixels = pixels;
        }

        /// <summary>Volume units per mosaic pixel.</summary>
        public int Downsample { get; }

        /// <summary>Mosaic pixel column of the left edge.</summary>
        public int XMin { get; }

        /// <summary>Mosaic pixel row of the top edge.</summary>
        public int YMax { get; }

        /// <summary>Raster width in pixels.</summary>
        public int Width { get; }

        /// <summary>Raster height in pixels.</summary>
        public int Height { get; }

        /// <summary>Mosaic pixels per raster pixel along both axes.</summary>
        public int Scale { get; }

        /// <summary>Volume units per raster pixel.</summary>
        public double WorldPerPixel => (double)Downsample * Scale;

        /// <summary>The bitmap, row major, 255 inside the mask and 0 outside.</summary>
        public byte[] Pixels { get; }

        /// <summary>The raster's placement as a wire layout: every block is whole, so the bounds are exactly the raster.</summary>
        public MaskLayout Layout => new(XMin, YMax, Width * Scale, Height * Scale, Scale);

        /// <summary>The rectangle the bitmap covers, volume space.</summary>
        public Rectangle WorldBounds => new(
            new Vector2(XMin * (double)Downsample, (YMax + 1 - Height * Scale) * (double)Downsample),
            new Vector2((XMin + Width * Scale) * (double)Downsample, (YMax + 1) * (double)Downsample));

        /// <summary>True when no pixel is inside the mask.</summary>
        public bool IsEmpty => Array.TrueForAll(Pixels, value => value == 0);

        /// <summary>True when the raster pixel holding the volume position <paramref name="world"/> is set; false outside the raster.</summary>
        public bool Contains(Vector2 world)
        {
            int column = (int)Math.Floor((world.X / Downsample - XMin) / Scale);
            int row = (int)Math.Floor((YMax + 1 - world.Y / Downsample) / Scale);
            return column >= 0 && row >= 0 && column < Width && row < Height && Pixels[row * Width + column] != 0;
        }

        /// <summary>
        /// This mask moved by whole mosaic pixels: <paramref name="columns"/> to the right and <paramref name="rows"/> up in
        /// volume space. Shares the pixels, which are not modified. Move by a multiple of <see cref="Scale"/> to keep every
        /// block whole.
        /// </summary>
        public RasterMask Translated(int columns, int rows) => new(Downsample, XMin + columns, YMax + rows, Width, Height, Pixels, Scale);

        /// <summary>This mask's pixels replaced by <paramref name="pixels"/>, same placement.</summary>
        public RasterMask WithPixels(byte[] pixels) => new(Downsample, XMin, YMax, Width, Height, pixels, Scale);

        /// <summary>
        /// The smallest volume-space rectangle holding every set pixel, or false when the mask is empty. Unlike
        /// <see cref="WorldBounds"/> it leaves out the margin left for dilating.
        /// </summary>
        public bool TryGetSetWorldBounds(out Rectangle bounds)
        {
            int minColumn = int.MaxValue, maxColumn = -1, minRow = int.MaxValue, maxRow = -1;
            for (int row = 0; row < Height; row++)
            {
                int offset = row * Width;
                for (int column = 0; column < Width; column++)
                {
                    if (Pixels[offset + column] == 0)
                        continue;

                    minColumn = Math.Min(minColumn, column);
                    maxColumn = Math.Max(maxColumn, column);
                    minRow = Math.Min(minRow, row);
                    maxRow = Math.Max(maxRow, row);
                }
            }

            if (maxColumn < 0)
            {
                bounds = default;
                return false;
            }

            bounds = new Rectangle(
                new Vector2((XMin + minColumn * Scale) * (double)Downsample, (YMax + 1 - (maxRow + 1) * Scale) * (double)Downsample),
                new Vector2((XMin + (maxColumn + 1) * Scale) * (double)Downsample, (YMax + 1 - minRow * Scale) * (double)Downsample));
            return true;
        }

        /// <summary>
        /// Rasterizes a polygon (exterior ring followed by holes, volume space, even-odd fill) into a mask over its bounding
        /// box with <paramref name="marginPixels"/> raster pixels free on every side, so it can be dilated. The scale comes
        /// from <see cref="MaskScalePolicy"/>, so the raster never exceeds <paramref name="maxRasterSide"/> on a side however large
        /// the polygon is. Returns null when the rings are unusable or the polygon covers no pixel centre.
        /// </summary>
        /// <param name="rings">Exterior ring first; each ring needs at least three vertices.</param>
        /// <param name="downsample">Volume units per mosaic pixel.</param>
        /// <param name="marginPixels">Raster pixels left free around the polygon.</param>
        /// <param name="maxRasterSide">
        /// The most pixels the raster may have along a side: the GPU's texture limit when the raster is drawn for editing. A polygon too big
        /// for it is downsampled (the scale doubles) until it fits.
        /// </param>
        public static RasterMask? FromRings(
            IReadOnlyList<Vector2[]> rings, int downsample, int marginPixels = 0, int maxRasterSide = MaskScalePolicy.DefaultMaxRasterSide)
        {
            if (rings is null || rings.Count == 0 || rings[0] is null || rings[0].Length < 3 || downsample <= 0)
                return null;

            SegmentationMaskUnion union = new();
            union.Add(rings);
            byte[]? pixels = union.Rasterize(downsample, out MaskLayout layout, marginRasterPixels: marginPixels, maxRasterSide: maxRasterSide);
            return pixels is null
                ? null
                : new RasterMask(downsample, layout.XMin, layout.YMax, layout.RasterWidth, layout.RasterHeight, pixels, layout.Scale);
        }

        /// <summary>
        /// A volume-space point well inside the mask: the pixel deepest from the edge (the first, when several tie), so it lies on the
        /// object even for a ring or a mask that was split into pieces, and never on a boundary pixel. Null when the mask is empty.
        /// </summary>
        public Vector2? InteriorPoint()
        {
            float[]? signedDistance = SegmentationMaskEdit.SignedDistance(Pixels, Width, Height);
            if (signedDistance is null)
                return null;

            int deepest = 0;
            for (int i = 1; i < signedDistance.Length; i++)
            {
                if (signedDistance[i] < signedDistance[deepest])
                    deepest = i;
            }

            int row = deepest / Width, column = deepest % Width;
            return new Vector2(
                (XMin + (column + 0.5) * Scale) * Downsample,
                (YMax + 1 - (row + 0.5) * Scale) * Downsample);
        }

        /// <summary>Axis-aligned bounds of a ring.</summary>
        public static Rectangle BoundsOf(Vector2[] ring)
        {
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (Vector2 v in ring)
            {
                minX = Math.Min(minX, v.X);
                minY = Math.Min(minY, v.Y);
                maxX = Math.Max(maxX, v.X);
                maxY = Math.Max(maxY, v.Y);
            }

            return new Rectangle(new Vector2(minX, minY), new Vector2(maxX, maxY));
        }
    }
}
