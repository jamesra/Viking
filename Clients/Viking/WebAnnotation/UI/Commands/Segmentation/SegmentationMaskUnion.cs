using Geometry;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The union of several volume-space shapes (squares, polygon rings and edited bitmaps) that become one SAM2 mask prompt.
    /// </summary>
    /// <remarks>
    /// The segmentation server accepts one mask per <c>Feature</c>, so a client that has several shapes sums
    /// them here into one 1-bit bitmap over their combined bounds. Used for the foreground seeds and for the background: the
    /// inscribed squares of other structures' circles and the polygons of other structures. A shape is a list of rings filled
    /// with the even-odd rule, so a polygon's holes stay open. The bitmap is sent whole, at one scale (see
    /// <see cref="MaskScalePolicy"/>): full resolution when the combined bounds fit one SAM2 window, coarse blocks above that.
    /// The server crops it per window, so a region may span many cells. Not thread safe; build it, then call
    /// <see cref="ToMask"/> from one thread.
    /// </remarks>
    internal sealed class SegmentationMaskUnion
    {
        /// <summary>Side of one SAM2 window in pixels at the request's downsample, SAM2's input size.</summary>
        public const int WindowSize = 1024;

        private readonly List<IReadOnlyList<Vector2[]>> shapes = [];
        private readonly List<RasterMask> bitmaps = [];

        /// <summary>Number of shapes and bitmaps added.</summary>
        public int Count => shapes.Count + bitmaps.Count;

        /// <summary>True when nothing has been added.</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>Adds a seed: a circle's disc (a polygon with <see cref="CircleSegments"/> vertices), a polygon with its holes, an edited bitmap, or a rectangle.</summary>
        public void Add(SegmentationSeed seed)
        {
            if (seed.Mask is not null)
            {
                bitmaps.Add(seed.Mask);
                return;
            }

            if (seed.Rings is not null)
            {
                Add(seed.Rings);
                return;
            }

            if (seed.Circle is not Circle circle)
            {
                Add(seed.Bounds);
                return;
            }

            Vector2[] ring = new Vector2[CircleSegments];
            for (int i = 0; i < ring.Length; i++)
            {
                double angle = 2 * Math.PI * i / ring.Length;
                ring[i] = new Vector2(circle.Center.X + circle.Radius * Math.Cos(angle), circle.Center.Y + circle.Radius * Math.Sin(angle));
            }

            Add([ring]);
        }

        /// <summary>Vertices of the polygon that stands in for a circle: at 1024 px the edge is within half a pixel of the true circle.</summary>
        private const int CircleSegments = 180;

        /// <summary>Adds an axis-aligned rectangle, volume space.</summary>
        public void Add(Rectangle rectangle)
        {
            shapes.Add(
            [[
                new Vector2(rectangle.Left, rectangle.Bottom),
                new Vector2(rectangle.Right, rectangle.Bottom),
                new Vector2(rectangle.Right, rectangle.Top),
                new Vector2(rectangle.Left, rectangle.Top),
            ]]);
        }

        /// <summary>
        /// Adds a polygon given as its exterior ring followed by any holes, volume space. A ring needs at
        /// least three vertices (a repeated closing vertex is fine); a shape with an unusable exterior is ignored.
        /// </summary>
        public void Add(IReadOnlyList<Vector2[]> rings)
        {
            if (rings is null || rings.Count == 0 || rings[0] is null || rings[0].Length < 3)
                return;

            shapes.Add(rings);
        }

        /// <summary>
        /// Rasterizes the union over its combined bounds and returns it as the wire <c>Mask</c>, or null when it is empty or
        /// nothing is left of it.
        /// </summary>
        /// <remarks>
        /// <c>Bounds</c> are mosaic pixels (world divided by downsample, floored, Y up) and cover every shape, snapped
        /// outward for the scale. The PNG is a 1-bit greyscale bitmap with image row 0 at <c>YMax</c>, as the proto requires,
        /// and <c>Scale</c> is set only above 1. A pixel is in the mask when its centre is inside a shape; at a scale above 1
        /// a block is in the mask when any mosaic pixel centre in it is, so snapping never shrinks a shape. Each point in
        /// <paramref name="holePointsVolume"/> clears a disc of <paramref name="holeRadiusPixels"/> mosaic pixels from the
        /// mask, so a background click inside a foreground mask is not overruled by it (the server returns every foreground
        /// mask pixel as object). A union wider or taller than <see cref="MaskScalePolicy.MaxBoundsSide"/> is cut down to
        /// that around <paramref name="centreVolume"/>, normally the seed's centre.
        /// </remarks>
        public SegmentationServiceTypes.Mask? ToMask(
            int downsample,
            Vector2 centreVolume,
            IReadOnlyList<Vector2>? holePointsVolume = null,
            double holeRadiusPixels = 0)
        {
            byte[]? pixels = Rasterize(downsample, centreVolume, out MaskLayout layout, holePointsVolume, holeRadiusPixels);
            return pixels is null ? null : EncodeMask(pixels, layout);
        }

        /// <summary>
        /// Rasterizes the union exactly as <see cref="ToMask"/> does, but returns the pixels (row 0 is mosaic
        /// <see cref="MaskLayout.YMax"/>, set pixels are 255) and their placement instead of a PNG. Null when the union is
        /// empty or nothing remains of it.
        /// </summary>
        /// <param name="downsample">Mosaic pixel size in volume units.</param>
        /// <param name="centreVolume">Volume-space point an oversized union is cut down around.</param>
        /// <param name="layout">Where the pixels sit and at which scale.</param>
        /// <param name="holePointsVolume">See <see cref="ToMask"/>.</param>
        /// <param name="holeRadiusPixels">See <see cref="ToMask"/>.</param>
        /// <param name="marginRasterPixels">Raster pixels left free on every side of the bounds (room to dilate an edited mask).</param>
        public byte[]? Rasterize(
            int downsample,
            Vector2 centreVolume,
            out MaskLayout layout,
            IReadOnlyList<Vector2>? holePointsVolume = null,
            double holeRadiusPixels = 0,
            int marginRasterPixels = 0)
        {
            layout = default;
            if (IsEmpty || downsample <= 0 || !TryGetMosaicBounds(downsample, out long xMin, out long xMax, out long yMin, out long yMax))
                return null;

            (xMin, xMax) = CutToLimit(xMin, xMax, (long)Math.Floor(centreVolume.X / downsample));
            (yMin, yMax) = CutToLimit(yMin, yMax, (long)Math.Floor(centreVolume.Y / downsample));
            layout = MaskScalePolicy.Plan(xMin, xMax, yMin, yMax, marginRasterPixels);

            byte[] pixels = new byte[layout.RasterWidth * layout.RasterHeight];
            bool any = false;
            foreach (IReadOnlyList<Vector2[]> shape in shapes)
                any |= Fill(pixels, layout, downsample, shape);

            foreach (RasterMask bitmap in bitmaps)
                any |= Blit(pixels, layout, downsample, bitmap);

            if (any && holePointsVolume is { Count: > 0 } && holeRadiusPixels > 0)
                any = CutHoles(pixels, layout, downsample, holePointsVolume, holeRadiusPixels);

            return any ? pixels : null;
        }

        /// <summary>
        /// Wraps raster pixels (0/255, row 0 at <see cref="MaskLayout.YMax"/>) as the wire <c>Mask</c>: a 1-bit greyscale PNG
        /// of <see cref="MaskLayout.RasterWidth"/> x <see cref="MaskLayout.RasterHeight"/>, the layout's inclusive mosaic
        /// bounds, and <c>Scale</c> when it is above 1.
        /// </summary>
        public static SegmentationServiceTypes.Mask EncodeMask(byte[] pixels, MaskLayout layout)
        {
            int width = layout.RasterWidth, height = layout.RasterHeight;
            if (pixels is null || pixels.Length != (long)width * height)
                throw new ArgumentException($"The layout needs {width} x {height} pixels.", nameof(pixels));

            L8[] gray = new L8[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                gray[i] = new L8(pixels[i]);

            using MemoryStream stream = new();
            using (var image = SixLabors.ImageSharp.Image.LoadPixelData<L8>(gray, width, height))
            {
                image.Save(stream, new PngEncoder
                {
                    ColorType = PngColorType.Grayscale,
                    BitDepth = PngBitDepth.Bit1,
                    CompressionLevel = PngCompressionLevel.BestCompression
                });
            }

            SegmentationServiceTypes.Mask mask = new()
            {
                Bounds = new SegmentationServiceTypes.BoundingBox
                {
                    XMin = layout.XMin,
                    YMin = layout.YMin,
                    XMax = layout.XMax,
                    YMax = layout.YMax
                },
                Png = Google.Protobuf.ByteString.CopyFrom(stream.ToArray())
            };
            if (layout.Scale > 1)
                mask.Scale = (uint)layout.Scale;

            return mask;
        }

        /// <summary>The inclusive mosaic-pixel box that holds every shape and every set bitmap pixel at <paramref name="downsample"/>.</summary>
        private bool TryGetMosaicBounds(int downsample, out long xMin, out long xMax, out long yMin, out long yMax)
        {
            xMin = yMin = long.MaxValue;
            xMax = yMax = long.MinValue;

            foreach (IReadOnlyList<Vector2[]> shape in shapes)
            {
                double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
                foreach (Vector2 vertex in shape[0])
                {
                    minX = Math.Min(minX, vertex.X / downsample);
                    maxX = Math.Max(maxX, vertex.X / downsample);
                    minY = Math.Min(minY, vertex.Y / downsample);
                    maxY = Math.Max(maxY, vertex.Y / downsample);
                }

                // The pixels whose centres (column + 0.5) can be inside the shape; a shape thinner than a pixel may have none.
                long firstColumn = (long)Math.Ceiling(minX - 0.5), lastColumn = (long)Math.Floor(maxX - 0.5);
                long firstRow = (long)Math.Ceiling(minY - 0.5), lastRow = (long)Math.Floor(maxY - 0.5);
                if (lastColumn < firstColumn || lastRow < firstRow)
                    continue;

                xMin = Math.Min(xMin, firstColumn);
                xMax = Math.Max(xMax, lastColumn);
                yMin = Math.Min(yMin, firstRow);
                yMax = Math.Max(yMax, lastRow);
            }

            foreach (RasterMask bitmap in bitmaps)
            {
                if (!bitmap.TryGetSetWorldBounds(out Rectangle bounds))
                    continue;

                xMin = Math.Min(xMin, (long)Math.Floor(bounds.Left / downsample));
                xMax = Math.Max(xMax, (long)Math.Ceiling(bounds.Right / downsample) - 1);
                yMin = Math.Min(yMin, (long)Math.Floor(bounds.Bottom / downsample));
                yMax = Math.Max(yMax, (long)Math.Ceiling(bounds.Top / downsample) - 1);
            }

            return xMax >= xMin && yMax >= yMin;
        }

        /// <summary>
        /// <paramref name="min"/>..<paramref name="max"/> unchanged when it is within <see cref="MaskScalePolicy.MaxBoundsSide"/>,
        /// otherwise that many pixels placed around <paramref name="centre"/> and kept inside the original range.
        /// </summary>
        private static (long Min, long Max) CutToLimit(long min, long max, long centre)
        {
            const int Side = MaskScalePolicy.MaxBoundsSide;
            if (max - min + 1 <= Side)
                return (min, max);

            long start = Math.Max(min, Math.Min(centre - Side / 2, max - Side + 1));
            return (start, start + Side - 1);
        }

        /// <summary>
        /// Sets every raster pixel whose block holds a set pixel of <paramref name="bitmap"/>. The bitmap may be at a different
        /// downsample or scale than the layout; it is mapped by world position, so nothing is traced or fitted, and a coarser
        /// target block is set when any part of it is set (so it never loses mask).
        /// </summary>
        private static bool Blit(byte[] pixels, MaskLayout layout, int downsample, RasterMask bitmap)
        {
            int width = layout.RasterWidth;
            (int First, int Last)[] columns = new (int, int)[bitmap.Width];
            for (int column = 0; column < bitmap.Width; column++)
            {
                double left = (bitmap.XMin + (long)column * bitmap.Scale) * (double)bitmap.Downsample;
                double right = (bitmap.XMin + (long)(column + 1) * bitmap.Scale) * (double)bitmap.Downsample;
                columns[column] = TargetRange(left, right, downsample, layout.XMin, layout.XMax, layout.XMin, layout.Scale, reverse: false);
            }

            (int First, int Last)[] rows = new (int, int)[bitmap.Height];
            for (int row = 0; row < bitmap.Height; row++)
            {
                double top = (bitmap.YMax + 1 - (long)row * bitmap.Scale) * (double)bitmap.Downsample;
                double bottom = (bitmap.YMax + 1 - (long)(row + 1) * bitmap.Scale) * (double)bitmap.Downsample;
                rows[row] = TargetRange(bottom, top, downsample, layout.YMin, layout.YMax, layout.YMax, layout.Scale, reverse: true);
            }

            bool any = false;
            for (int row = 0; row < bitmap.Height; row++)
            {
                (int firstRow, int lastRow) = rows[row];
                if (firstRow > lastRow)
                    continue;

                int sourceOffset = row * bitmap.Width;
                for (int column = 0; column < bitmap.Width; column++)
                {
                    (int firstColumn, int lastColumn) = columns[column];
                    if (firstColumn > lastColumn || bitmap.Pixels[sourceOffset + column] == 0)
                        continue;

                    for (int targetRow = firstRow; targetRow <= lastRow; targetRow++)
                    {
                        int targetOffset = targetRow * width;
                        for (int targetColumn = firstColumn; targetColumn <= lastColumn; targetColumn++)
                            pixels[targetOffset + targetColumn] = 255;
                    }

                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// The raster columns (or rows, when <paramref name="reverse"/>) of the target layout that the world range
        /// <paramref name="low"/>..<paramref name="high"/> touches; First greater than Last when it misses the layout.
        /// </summary>
        /// <param name="regionMin">Lowest mosaic pixel of the layout on this axis.</param>
        /// <param name="regionMax">Highest mosaic pixel of the layout on this axis.</param>
        /// <param name="anchor">The mosaic pixel raster index 0 starts at: XMin for columns, YMax for rows.</param>
        private static (int First, int Last) TargetRange(
            double low, double high, int downsample, long regionMin, long regionMax, long anchor, int scale, bool reverse)
        {
            long rawFirst = (long)Math.Floor(low / downsample);
            long rawLast = Math.Max(rawFirst, (long)Math.Ceiling(high / downsample) - 1);
            if (rawLast < regionMin || rawFirst > regionMax)
                return (1, 0);

            long first = Math.Max(regionMin, rawFirst);
            long last = Math.Min(regionMax, rawLast);

            return reverse
                ? ((int)((anchor - last) / scale), (int)((anchor - first) / scale))
                : ((int)((first - anchor) / scale), (int)((last - anchor) / scale));
        }

        /// <summary>
        /// Clears a disc of <paramref name="radius"/> mosaic pixels around each point; returns whether any pixel is still set.
        /// </summary>
        private static bool CutHoles(byte[] pixels, MaskLayout layout, int downsample, IReadOnlyList<Vector2> points, double radius)
        {
            int width = layout.RasterWidth, height = layout.RasterHeight;
            double rasterRadius = radius / layout.Scale;
            foreach (Vector2 point in points)
            {
                double centreColumn = (point.X / downsample - layout.XMin) / layout.Scale;
                double centreRow = (layout.YMax + 1 - point.Y / downsample) / layout.Scale;
                int firstRow = Math.Max(0, (int)Math.Floor(centreRow - rasterRadius));
                int lastRow = Math.Min(height - 1, (int)Math.Ceiling(centreRow + rasterRadius));
                int firstColumn = Math.Max(0, (int)Math.Floor(centreColumn - rasterRadius));
                int lastColumn = Math.Min(width - 1, (int)Math.Ceiling(centreColumn + rasterRadius));
                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = firstColumn; column <= lastColumn; column++)
                    {
                        double dx = column + 0.5 - centreColumn, dy = row + 0.5 - centreRow;
                        if (dx * dx + dy * dy <= rasterRadius * rasterRadius)
                            pixels[row * width + column] = 0;
                    }
                }
            }

            return Array.Exists(pixels, value => value != 0);
        }

        /// <summary>
        /// Even-odd scanline fill of one shape into the raster. Works a mosaic pixel row at a time and sets the raster block of
        /// every mosaic pixel whose centre is inside the shape, so at scale 1 it is the exact fill and above 1 the blocks cover the
        /// shape. Returns whether any pixel was set.
        /// </summary>
        private static bool Fill(byte[] pixels, MaskLayout layout, int downsample, IReadOnlyList<Vector2[]> rings)
        {
            double shapeMinY = double.MaxValue, shapeMaxY = double.MinValue;
            foreach (Vector2 vertex in rings[0])
            {
                shapeMinY = Math.Min(shapeMinY, vertex.Y / downsample);
                shapeMaxY = Math.Max(shapeMaxY, vertex.Y / downsample);
            }

            int width = layout.RasterWidth;
            int scale = layout.Scale;

            // Mosaic row m has its centre at y = m + 0.5.
            long firstRow = Math.Max(layout.YMin, (long)Math.Floor(shapeMinY - 0.5));
            long lastRow = Math.Min(layout.YMax, (long)Math.Ceiling(shapeMaxY - 0.5));
            bool any = false;
            List<double> crossings = [];
            for (long mosaicRow = firstRow; mosaicRow <= lastRow; mosaicRow++)
            {
                double y = mosaicRow + 0.5;
                crossings.Clear();
                foreach (Vector2[] ring in rings)
                {
                    for (int i = 0; i < ring.Length; i++)
                    {
                        Vector2 a = ring[i];
                        Vector2 b = ring[(i + 1) % ring.Length];
                        double ay = a.Y / downsample, by = b.Y / downsample;
                        if ((ay <= y) == (by <= y))
                            continue;

                        double t = (y - ay) / (by - ay);
                        crossings.Add((a.X + t * (b.X - a.X)) / downsample);
                    }
                }

                crossings.Sort();
                int rasterRow = (int)((layout.YMax - mosaicRow) / scale);
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    // A mosaic column c has its centre at x = c + 0.5.
                    long first = Math.Max(layout.XMin, (long)Math.Ceiling(crossings[i] - 0.5));
                    long last = Math.Min(layout.XMax, (long)Math.Floor(crossings[i + 1] - 0.5));
                    if (last < first)
                        continue;

                    int firstColumn = (int)((first - layout.XMin) / scale);
                    int lastColumn = (int)((last - layout.XMin) / scale);
                    for (int column = firstColumn; column <= lastColumn; column++)
                        pixels[rasterRow * width + column] = 255;

                    any = true;
                }
            }

            return any;
        }
    }
}
