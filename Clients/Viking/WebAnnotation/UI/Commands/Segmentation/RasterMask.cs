#nullable enable

using System;
using System.Collections.Generic;
using Geometry;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// A square bitmap mask placed in volume space: the input and output of <see cref="SegmentationMaskEditCommand"/>.
    /// </summary>
    /// <remarks>
    /// Same layout as the wire mask built by <see cref="SegmentationMaskUnion"/>: <see cref="SegmentationMaskUnion.WindowSize"/>
    /// pixels square, each pixel <see cref="Downsample"/> volume units wide, row 0 at mosaic row <see cref="YMax"/> (Y up in
    /// the volume, so rows run downwards), pixel value 255 inside the mask and 0 outside. Immutable once built;
    /// <see cref="Pixels"/> must not be modified by callers.
    /// </remarks>
    internal sealed class RasterMask
    {
        /// <summary>Side of the bitmap in pixels.</summary>
        public const int Size = SegmentationMaskUnion.WindowSize;

        /// <param name="downsample">Volume units per pixel.</param>
        /// <param name="xMin">Mosaic pixel column of the left edge.</param>
        /// <param name="yMax">Mosaic pixel row of the top edge (row 0).</param>
        /// <param name="pixels"><see cref="Size"/> x <see cref="Size"/> bytes, 255 inside the mask.</param>
        public RasterMask(int downsample, int xMin, int yMax, byte[] pixels)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));
            if (pixels is null || pixels.Length != Size * Size)
                throw new ArgumentException($"A mask is {Size} x {Size} pixels.", nameof(pixels));

            Downsample = downsample;
            XMin = xMin;
            YMax = yMax;
            Pixels = pixels;
        }

        /// <summary>Volume units per pixel.</summary>
        public int Downsample { get; }

        /// <summary>Mosaic pixel column of the left edge.</summary>
        public int XMin { get; }

        /// <summary>Mosaic pixel row of the top edge.</summary>
        public int YMax { get; }

        /// <summary>The bitmap, row major, 255 inside the mask and 0 outside.</summary>
        public byte[] Pixels { get; }

        /// <summary>The square the bitmap covers, volume space.</summary>
        public Rectangle WorldBounds => new(
            new Vector2(XMin * (double)Downsample, (YMax + 1 - Size) * (double)Downsample),
            new Vector2((XMin + Size) * (double)Downsample, (YMax + 1) * (double)Downsample));

        /// <summary>True when no pixel is inside the mask.</summary>
        public bool IsEmpty => Array.TrueForAll(Pixels, value => value == 0);

        /// <summary>This mask's pixels replaced by <paramref name="pixels"/>, same placement.</summary>
        public RasterMask WithPixels(byte[] pixels) => new(Downsample, XMin, YMax, pixels);

        /// <summary>The mask as the segmentation server's foreground-mask prompt.</summary>
        public SegmentationServiceTypes.Mask ToWireMask() => SegmentationMaskUnion.EncodeMask(Pixels, XMin, YMax);

        /// <summary>
        /// Rasterizes a polygon (exterior ring followed by holes, volume space, even-odd fill) into a mask centred on its
        /// bounding box. Returns null when the rings are unusable or the polygon covers no pixel centre.
        /// </summary>
        /// <param name="rings">Exterior ring first; each ring needs at least three vertices.</param>
        /// <param name="downsample">Volume units per pixel.</param>
        public static RasterMask? FromRings(IReadOnlyList<Vector2[]> rings, int downsample)
        {
            if (rings is null || rings.Count == 0 || rings[0] is null || rings[0].Length < 3)
                return null;

            SegmentationMaskUnion union = new();
            union.Add(rings);
            byte[]? pixels = union.Rasterize(downsample, BoundsOf(rings[0]).Center, out int xMin, out int yMax);
            return pixels is null ? null : new RasterMask(downsample, xMin, yMax, pixels);
        }

        /// <summary>
        /// The smallest power-of-two downsample at which a shape of <paramref name="bounds"/> still leaves
        /// <paramref name="marginPixels"/> free on every side of the <see cref="Size"/> square, so it can be dilated.
        /// </summary>
        public static int ChooseDownsample(Rectangle bounds, int marginPixels)
        {
            double longest = Math.Max(bounds.Width, bounds.Height);
            double usable = Math.Max(1, Size - 2 * marginPixels);
            int downsample = 1;
            while (longest / downsample > usable && downsample < (1 << 20))
                downsample *= 2;

            return downsample;
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
