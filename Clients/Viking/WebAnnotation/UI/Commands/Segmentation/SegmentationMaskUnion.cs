using Geometry;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SegmentationServiceTypes = Viking.gRPC.SegmentationServiceTypes.V1;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The union of several volume-space shapes (rectangles, polygon rings and edited bitmaps) drawn as one raster: the edited
    /// bitmap a request sends as FOREGROUND mask tiles, and the polygon an edit starts from (<see cref="RasterMask.FromRings"/>).
    /// </summary>
    /// <remarks>
    /// A shape is a list of rings filled with the even-odd rule, so a polygon's holes stay open. The union is drawn a grid tile at a
    /// time (<see cref="RasterizeTile"/>, through an index of which shapes reach which tile), which is how a mask reaches the server;
    /// <see cref="Rasterize"/> draws it as one raster over its bounds, for the dilate/erode editor. Not thread safe; build it, then
    /// draw from one thread.
    /// </remarks>
    internal sealed class SegmentationMaskUnion
    {
        /// <summary>Side of one SAM2 window in pixels at the request's downsample, SAM2's input size.</summary>
        public const int WindowSize = 1024;

        private readonly List<IReadOnlyList<Vector2[]>> shapes = [];
        private readonly List<RasterMask> bitmaps = [];

        /// <summary>
        /// Per downsample, which shapes and bitmaps touch which tile. Dropped whenever something is added. Built on first use under
        /// <see cref="indexGate"/>, because a union is shared by the requests of a command and an abandoned request's task may still be
        /// drawing from it when the next one starts; once built an index is only read.
        /// </summary>
        private readonly Dictionary<int, TileIndex> tileIndexes = [];

        private readonly object indexGate = new();

        /// <summary>Number of shapes and bitmaps added.</summary>
        public int Count => shapes.Count + bitmaps.Count;

        /// <summary>True when nothing has been added.</summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// Adds a seed: a polygon with its holes, an edited bitmap, or a rectangle. A circle is refused: it goes to the server as
        /// <c>Shape.circle</c> (<see cref="SegmentationPromptShapes.FromSeed"/>), which softens it for its model, and is never
        /// rasterized here.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="seed"/> is a circle.</exception>
        public void Add(SegmentationSeed seed)
        {
            if (seed.Mask is not null)
            {
                bitmaps.Add(seed.Mask);
                lock (indexGate)
                    tileIndexes.Clear();

                return;
            }

            if (seed.Rings is not null)
            {
                Add(seed.Rings);
                return;
            }

            if (seed.Circle is not null)
                throw new ArgumentException("A circle seed is sent as a shape, not rasterized.", nameof(seed));

            Add(seed.Bounds);
        }

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
            lock (indexGate)
                tileIndexes.Clear();
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
            lock (indexGate)
                tileIndexes.Clear();
        }

        /// <summary>
        /// Rasterizes the union over its combined bounds into one raster: row 0 is mosaic <see cref="MaskLayout.YMax"/>, set pixels
        /// are 255, and <paramref name="layout"/> says where the pixels sit and at which scale. A pixel (at a scale above 1, a block) is
        /// set when its centre is inside a shape, so the raster is the shapes themselves at that resolution, neither grown nor
        /// shrunk. Null when the union is empty or nothing remains of it.
        /// </summary>
        /// <param name="downsample">Mosaic pixel size in volume units.</param>
        /// <param name="layout">Where the pixels sit and at which scale.</param>
        /// <param name="holePointsVolume">Each clears a disc of <paramref name="holeRadiusPixels"/> mosaic pixels from the raster.</param>
        /// <param name="holeRadiusPixels">Radius of those discs, in mosaic pixels.</param>
        /// <param name="marginRasterPixels">Raster pixels left free on every side of the bounds (room to dilate an edited mask).</param>
        /// <param name="maxRasterSide">The most pixels the raster may have along a side; the scale doubles until it fits.</param>
        public byte[]? Rasterize(
            int downsample,
            out MaskLayout layout,
            IReadOnlyList<Vector2>? holePointsVolume = null,
            double holeRadiusPixels = 0,
            int marginRasterPixels = 0,
            int maxRasterSide = MaskScalePolicy.DefaultMaxRasterSide)
        {
            layout = default;
            if (IsEmpty || downsample <= 0 || !TryGetMosaicBounds(downsample, out long xMin, out long xMax, out long yMin, out long yMax))
                return null;

            layout = MaskScalePolicy.Plan(xMin, xMax, yMin, yMax, marginRasterPixels, maxRasterSide);

            byte[] pixels = new byte[layout.RasterWidth * layout.RasterHeight];
            bool any = false;
            // A raster that is edited is unbiased: a block is set when its centre is inside the shape, so radius 0 is the polygon itself
            // at any scale. (A mask that is sent to the server covers instead: see RasterizeTile.)
            foreach (IReadOnlyList<Vector2[]> shape in shapes)
                any |= FillCentres(pixels, layout, downsample, shape);

            foreach (RasterMask bitmap in bitmaps)
                any |= Blit(pixels, layout, downsample, bitmap);

            if (any && holePointsVolume is { Count: > 0 } && holeRadiusPixels > 0)
                any = CutHoles(pixels, layout, downsample, holePointsVolume, holeRadiusPixels);

            return any ? pixels : null;
        }

        /// <summary>A 1-bit greyscale PNG of <paramref name="width"/> x <paramref name="height"/> pixels, 255 (or any non-zero) set.</summary>
        public static byte[] EncodePng(byte[] pixels, int width, int height)
        {
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

            return stream.ToArray();
        }

        /// <summary>The inclusive mosaic-pixel box that holds every shape and every set bitmap pixel at <paramref name="downsample"/>.</summary>
        private bool TryGetMosaicBounds(int downsample, out long xMin, out long xMax, out long yMin, out long yMax)
        {
            xMin = yMin = long.MaxValue;
            xMax = yMax = long.MinValue;

            foreach (IReadOnlyList<Vector2[]> shape in shapes)
            {
                if (!TryGetShapeBounds(shape, downsample, out long firstColumn, out long lastColumn, out long firstRow, out long lastRow))
                    continue;

                xMin = Math.Min(xMin, firstColumn);
                xMax = Math.Max(xMax, lastColumn);
                yMin = Math.Min(yMin, firstRow);
                yMax = Math.Max(yMax, lastRow);
            }

            foreach (RasterMask bitmap in bitmaps)
            {
                if (!TryGetBitmapBounds(bitmap, downsample, out long firstColumn, out long lastColumn, out long firstRow, out long lastRow))
                    continue;

                xMin = Math.Min(xMin, firstColumn);
                xMax = Math.Max(xMax, lastColumn);
                yMin = Math.Min(yMin, firstRow);
                yMax = Math.Max(yMax, lastRow);
            }

            return xMax >= xMin && yMax >= yMin;
        }

        /// <summary>
        /// The mosaic pixels whose centres (column + 0.5) can be inside the shape; false for a shape thinner than a pixel,
        /// which has none.
        /// </summary>
        private static bool TryGetShapeBounds(
            IReadOnlyList<Vector2[]> shape, int downsample, out long firstColumn, out long lastColumn, out long firstRow, out long lastRow)
        {
            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (Vector2 vertex in shape[0])
            {
                minX = Math.Min(minX, vertex.X / downsample);
                maxX = Math.Max(maxX, vertex.X / downsample);
                minY = Math.Min(minY, vertex.Y / downsample);
                maxY = Math.Max(maxY, vertex.Y / downsample);
            }

            firstColumn = (long)Math.Ceiling(minX - 0.5);
            lastColumn = (long)Math.Floor(maxX - 0.5);
            firstRow = (long)Math.Ceiling(minY - 0.5);
            lastRow = (long)Math.Floor(maxY - 0.5);
            return lastColumn >= firstColumn && lastRow >= firstRow;
        }

        /// <summary>The mosaic pixels the set pixels of <paramref name="bitmap"/> cover; false for an empty bitmap.</summary>
        private static bool TryGetBitmapBounds(
            RasterMask bitmap, int downsample, out long firstColumn, out long lastColumn, out long firstRow, out long lastRow)
        {
            firstColumn = lastColumn = firstRow = lastRow = 0;
            if (!bitmap.TryGetSetWorldBounds(out Rectangle bounds))
                return false;

            firstColumn = (long)Math.Floor(bounds.Left / downsample);
            lastColumn = (long)Math.Ceiling(bounds.Right / downsample) - 1;
            firstRow = (long)Math.Floor(bounds.Bottom / downsample);
            lastRow = (long)Math.Ceiling(bounds.Top / downsample) - 1;
            return true;
        }

        // ---- tiles ---------------------------------------------------------------------------------------------------

        /// <summary>Which shapes and bitmaps touch each 1024-pixel tile of the grid at one downsample.</summary>
        private sealed class TileIndex
        {
            public readonly Dictionary<TileCell, (List<int> Shapes, List<int> Bitmaps)> Cells = [];

            public List<int> Shapes(TileCell cell) => Entry(cell).Shapes;

            public List<int> Bitmaps(TileCell cell) => Entry(cell).Bitmaps;

            private (List<int> Shapes, List<int> Bitmaps) Entry(TileCell cell)
            {
                if (!Cells.TryGetValue(cell, out var entry))
                {
                    entry = ([], []);
                    Cells[cell] = entry;
                }

                return entry;
            }
        }

        private static long FloorDivide(long value, int divisor) => (long)Math.Floor(value / (double)divisor);

        private TileIndex IndexFor(int downsample)
        {
            lock (indexGate)
                return IndexForLocked(downsample);
        }

        private TileIndex IndexForLocked(int downsample)
        {
            if (tileIndexes.TryGetValue(downsample, out TileIndex? index))
                return index;

            index = new TileIndex();
            const int Tile = SegmentationTileGrid.TileSize;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (!TryGetShapeBounds(shapes[i], downsample, out long x0, out long x1, out long y0, out long y1))
                    continue;

                for (long row = FloorDivide(y0, Tile); row <= FloorDivide(y1, Tile); row++)
                    for (long col = FloorDivide(x0, Tile); col <= FloorDivide(x1, Tile); col++)
                        index.Shapes(new TileCell((int)row, (int)col)).Add(i);
            }

            for (int i = 0; i < bitmaps.Count; i++)
            {
                if (!TryGetBitmapBounds(bitmaps[i], downsample, out long x0, out long x1, out long y0, out long y1))
                    continue;

                for (long row = FloorDivide(y0, Tile); row <= FloorDivide(y1, Tile); row++)
                    for (long col = FloorDivide(x0, Tile); col <= FloorDivide(x1, Tile); col++)
                        index.Bitmaps(new TileCell((int)row, (int)col)).Add(i);
            }

            tileIndexes[downsample] = index;
            return index;
        }

        /// <summary>
        /// The tiles of the grid at <paramref name="downsample"/> that some shape or set bitmap pixel can reach, in row then
        /// column order. A tile listed may still rasterize to nothing (a shape's bounding box touches it but the shape does not).
        /// </summary>
        public IReadOnlyList<TileCell> TilesTouched(int downsample)
        {
            if (IsEmpty || downsample <= 0)
                return [];

            return [.. IndexFor(downsample).Cells.Keys.OrderBy(cell => cell.Row).ThenBy(cell => cell.Col)];
        }

        /// <summary>
        /// The longest side, in mosaic pixels at <paramref name="downsample"/>, of the set pixels of any bitmap in the union; 0 when there
        /// is none. A bitmap is an edited mask, so this says how large the edit was.
        /// </summary>
        public long LargestBitmapSide(int downsample)
        {
            long largest = 0;
            foreach (RasterMask bitmap in bitmaps)
            {
                if (TryGetBitmapBounds(bitmap, downsample, out long x0, out long x1, out long y0, out long y1))
                    largest = Math.Max(largest, Math.Max(x1 - x0 + 1, y1 - y0 + 1));
            }

            return largest;
        }

        /// <summary>How many shapes and bitmaps can reach <paramref name="cell"/>: what <see cref="RasterizeTile"/> would draw.</summary>
        internal int ShapesReaching(int downsample, TileCell cell)
        {
            if (IsEmpty || downsample <= 0 || !IndexFor(downsample).Cells.TryGetValue(cell, out var entry))
                return 0;

            return entry.Shapes.Count + entry.Bitmaps.Count;
        }

        /// <summary>
        /// Rasterizes the union into one grid tile: row 0 the highest mosaic y of the tile (Y down, like the image tile), 255 set.
        /// At <paramref name="scale"/> 1 the tile is <see cref="SegmentationTileGrid.TileSize"/> square; at 4 it is 256 square and a
        /// pixel is set when any of its 4 x 4 mosaic pixels is. A coarse tile is drawn directly from the shapes and bitmaps, never
        /// from a full-size tile, and a bitmap coarser than the tile is expanded only here. Only the shapes that reach the tile are
        /// drawn, found through an index built once per downsample, so a tile far from most shapes costs almost nothing.
        /// Null when nothing in the tile is set.
        /// </summary>
        /// <param name="holePointsVolume">Each clears a disc of <paramref name="holeRadiusPixels"/> mosaic pixels, as in <see cref="ToMask"/>.</param>
        /// <param name="scale">Mosaic pixels per tile pixel; it must divide the tile size (1 or 4).</param>
        public byte[]? RasterizeTile(
            int downsample,
            TileCell cell,
            IReadOnlyList<Vector2>? holePointsVolume = null,
            double holeRadiusPixels = 0,
            int scale = 1)
        {
            if (IsEmpty || downsample <= 0 || !IndexFor(downsample).Cells.TryGetValue(cell, out var entry))
                return null;

            const int Tile = SegmentationTileGrid.TileSize;
            MaskLayout layout = new(cell.Col * Tile, cell.Row * Tile + Tile - 1, Tile, Tile, scale);

            // Most tiles in a shape's bounding box turn out empty, so draw into a per-thread scratch raster and copy only a tile that
            // has something in it, instead of allocating a megabyte for every candidate.
            int length = layout.RasterWidth * layout.RasterHeight;
            byte[] pixels = ScratchFor(length);
            Array.Clear(pixels, 0, length);
            bool any = false;
            foreach (int i in entry.Shapes)
                any |= Fill(pixels, layout, downsample, shapes[i]);

            foreach (int i in entry.Bitmaps)
                any |= Blit(pixels, layout, downsample, bitmaps[i]);

            if (any && holePointsVolume is { Count: > 0 } && holeRadiusPixels > 0)
                any = CutHoles(pixels, layout, downsample, holePointsVolume, holeRadiusPixels);

            if (!any)
                return null;

            byte[] drawn = new byte[length];
            Buffer.BlockCopy(pixels, 0, drawn, 0, length);
            return drawn;
        }

        [ThreadStatic]
        private static byte[]? scratchFull;

        [ThreadStatic]
        private static byte[]? scratchCoarse;

        /// <summary>A per-thread raster of <paramref name="length"/> pixels (a full or a coarse tile), reused between calls.</summary>
        private static byte[] ScratchFor(int length)
        {
            const int Full = SegmentationTileGrid.TileSize * SegmentationTileGrid.TileSize;
            if (length == Full)
                return scratchFull ??= new byte[Full];

            if (scratchCoarse is null || scratchCoarse.Length != length)
                scratchCoarse = new byte[length];

            return scratchCoarse;
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

            // Target ranges grow monotonically along each axis, so the rows and columns that reach the layout are contiguous.
            int sourceFirstRow = Array.FindIndex(rows, range => range.First <= range.Last);
            if (sourceFirstRow < 0)
                return false;

            int sourceLastRow = Array.FindLastIndex(rows, range => range.First <= range.Last);
            int sourceFirstColumn = Array.FindIndex(columns, range => range.First <= range.Last);
            if (sourceFirstColumn < 0)
                return false;

            int sourceLastColumn = Array.FindLastIndex(columns, range => range.First <= range.Last);

            bool any = false;
            for (int row = sourceFirstRow; row <= sourceLastRow; row++)
            {
                (int firstRow, int lastRow) = rows[row];
                if (firstRow > lastRow)
                    continue;

                int sourceOffset = row * bitmap.Width;
                for (int column = sourceFirstColumn; column <= sourceLastColumn; column++)
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

        /// <summary>The non-horizontal edges of the rings, in mosaic units (world divided by the downsample).</summary>
        private static (double Ax, double Ay, double Bx, double By)[] EdgesOf(IReadOnlyList<Vector2[]> rings, int downsample)
        {
            List<(double, double, double, double)> edges = [];
            foreach (Vector2[] ring in rings)
            {
                for (int i = 0; i < ring.Length; i++)
                {
                    Vector2 a = ring[i];
                    Vector2 b = ring[(i + 1) % ring.Length];
                    if (a.Y != b.Y)
                        edges.Add((a.X / downsample, a.Y / downsample, b.X / downsample, b.Y / downsample));
                }
            }

            return [.. edges];
        }

        /// <summary>The x of every edge that crosses the horizontal line at mosaic <paramref name="y"/>, sorted (even-odd pairs are inside).</summary>
        private static void CrossingsAt(double y, (double Ax, double Ay, double Bx, double By)[] edges, List<double> crossings)
        {
            crossings.Clear();
            foreach ((double ax, double ay, double bx, double by) in edges)
            {
                if ((ay <= y) == (by <= y))
                    continue;

                crossings.Add(ax + (y - ay) / (by - ay) * (bx - ax));
            }

            crossings.Sort();
        }

        /// <summary>
        /// Even-odd scanline fill of one shape into the raster, covering: it works a mosaic pixel row at a time and sets the raster
        /// block of every mosaic pixel whose centre is inside the shape, so at scale 1 it is the exact fill and above 1 the blocks
        /// cover the shape and never lose any of it. Returns whether any pixel was set.
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
            var edges = EdgesOf(rings, downsample);

            // Mosaic row m has its centre at y = m + 0.5.
            long firstRow = Math.Max(layout.YMin, (long)Math.Floor(shapeMinY - 0.5));
            long lastRow = Math.Min(layout.YMax, (long)Math.Ceiling(shapeMaxY - 0.5));
            bool any = false;
            List<double> crossings = [];
            for (long mosaicRow = firstRow; mosaicRow <= lastRow; mosaicRow++)
            {
                CrossingsAt(mosaicRow + 0.5, edges, crossings);
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

        /// <summary>
        /// Even-odd scanline fill of one shape into the raster, sampling: a raster pixel (a block, above scale 1) is set when its centre is
        /// inside the shape. At scale 1 this is <see cref="Fill"/>; above it the result is the shape at that resolution rather than a
        /// grown copy. Returns whether any pixel was set.
        /// </summary>
        private static bool FillCentres(byte[] pixels, MaskLayout layout, int downsample, IReadOnlyList<Vector2[]> rings)
        {
            double shapeMinY = double.MaxValue, shapeMaxY = double.MinValue;
            foreach (Vector2 vertex in rings[0])
            {
                shapeMinY = Math.Min(shapeMinY, vertex.Y / downsample);
                shapeMaxY = Math.Max(shapeMaxY, vertex.Y / downsample);
            }

            int width = layout.RasterWidth, height = layout.RasterHeight;
            int scale = layout.Scale;
            var edges = EdgesOf(rings, downsample);

            // Raster row r has its centre at mosaic y = YMax + 1 - (r + 0.5) * scale; raster column c at x = XMin + (c + 0.5) * scale.
            int firstRow = Math.Max(0, (int)Math.Floor((layout.YMax + 1 - shapeMaxY) / scale - 0.5));
            int lastRow = Math.Min(height - 1, (int)Math.Ceiling((layout.YMax + 1 - shapeMinY) / scale - 0.5));
            bool any = false;
            List<double> crossings = [];
            for (int row = firstRow; row <= lastRow; row++)
            {
                CrossingsAt(layout.YMax + 1 - (row + 0.5) * scale, edges, crossings);
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    int first = Math.Max(0, (int)Math.Ceiling((crossings[i] - layout.XMin) / scale - 0.5));
                    int last = Math.Min(width - 1, (int)Math.Floor((crossings[i + 1] - layout.XMin) / scale - 0.5));
                    for (int column = first; column <= last; column++)
                    {
                        pixels[row * width + column] = 255;
                        any = true;
                    }
                }
            }

            return any;
        }
    }
}
