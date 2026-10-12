using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using Vector2 = Geometry.Vector2;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The grayscale tiles the viewer captured for SAM2, kept so a traced contour can be compared with the image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each tile is stored blurred (<see cref="PrepareGray"/>): EM is grainy, and the edge snap reads brightness
    /// differences a pixel or two apart. A tile is 1 MiB, so the store holds a bounded number and drops the least
    /// recently stored. All members are safe to call from several threads; capture continuations store from the
    /// thread pool while polygonizing reads.
    /// </para>
    /// <para>
    /// Images are keyed by the same string as the session's uploaded-tile keys (<c>TileKey</c>), so a tile for another
    /// section, channel, transform or downsample is never mistaken for this one.
    /// </para>
    /// </remarks>
    internal sealed class SegmentationTileImages
    {
        /// <summary>Most tiles kept at once (about 48 MiB).</summary>
        public const int Capacity = 48;

        /// <summary>Standard deviation, in pixels, of the blur applied when a tile is stored.</summary>
        public const double BlurSigma = 1.5;

        private readonly object gate = new();
        private readonly Dictionary<string, byte[]> tiles = [];
        private readonly Dictionary<string, double> edgeScales = [];
        private readonly LinkedList<string> order = new();

        /// <summary>Number of tiles held.</summary>
        public int Count
        {
            get
            {
                lock (gate)
                    return tiles.Count;
            }
        }

        /// <summary>
        /// Keeps <paramref name="blurredGray"/> (a <see cref="SegmentationTileGrid.TileSize"/> square, Y down, from
        /// <see cref="PrepareGray"/>) under <paramref name="key"/>, replacing an earlier image of the same tile and
        /// dropping the oldest tile when full. The array is kept by reference and must not be changed afterwards.
        /// </summary>
        public void Store(string key, byte[] blurredGray)
        {
            if (blurredGray is null || blurredGray.Length != SegmentationTileGrid.TileSize * SegmentationTileGrid.TileSize)
                throw new ArgumentException("A tile image must be a full tile.", nameof(blurredGray));

            double scale = EdgeScaleOf(blurredGray, SegmentationTileGrid.TileSize, SegmentationTileGrid.TileSize);
            lock (gate)
            {
                if (tiles.ContainsKey(key))
                    order.Remove(key);

                tiles[key] = blurredGray;
                edgeScales[key] = scale;
                order.AddLast(key);
                while (tiles.Count > Capacity)
                {
                    string oldest = order.First!.Value;
                    tiles.Remove(oldest);
                    edgeScales.Remove(oldest);
                    order.RemoveFirst();
                }
            }
        }

        /// <summary>The stored image for <paramref name="key"/>, or null.</summary>
        public byte[]? Get(string key)
        {
            lock (gate)
                return tiles.TryGetValue(key, out byte[]? gray) ? gray : null;
        }

        /// <summary>Forgets every tile, when the view identity changes.</summary>
        public void Clear()
        {
            lock (gate)
            {
                tiles.Clear();
                edgeScales.Clear();
                order.Clear();
            }
        }

        /// <summary>
        /// A captured tile's luminance, blurred, one byte per pixel, Y down.
        /// </summary>
        /// <param name="stretchedLuma">The luminance SAM2 is sent: <see cref="SegmentationCaptureEncoder.StretchedLuma"/>.
        /// Using the stretched values keeps contrast comparable between tiles, so one edge threshold means the same thing everywhere.</param>
        /// <param name="width">Tile width in pixels.</param>
        /// <param name="height">Tile height in pixels.</param>
        /// <remarks>
        /// The blur is a separable Gaussian of <see cref="BlurSigma"/> with repeated edge pixels. Pure function of its
        /// input; call it off the UI thread, it touches a million pixels.
        /// </remarks>
        public static byte[] PrepareGray(byte[] stretchedLuma, int width, int height)
        {
            if (stretchedLuma is null || stretchedLuma.Length != width * height)
                throw new ArgumentException("Pixel count does not match the size.", nameof(stretchedLuma));

            float[] luminance = new float[stretchedLuma.Length];
            for (int i = 0; i < luminance.Length; i++)
                luminance[i] = stretchedLuma[i];

            return ToBytes(Blur(luminance, width, height, BlurSigma));
        }

        /// <summary>Separable Gaussian blur with edge pixels repeated. Returns the input when <paramref name="sigma"/> is not positive.</summary>
        internal static float[] Blur(float[] source, int width, int height, double sigma)
        {
            if (!(sigma > 0))
                return source;

            int radius = Math.Max(1, (int)Math.Ceiling(3 * sigma));
            float[] kernel = new float[(2 * radius) + 1];
            double total = 0;
            for (int i = -radius; i <= radius; i++)
            {
                double weight = Math.Exp(-(i * i) / (2 * sigma * sigma));
                kernel[i + radius] = (float)weight;
                total += weight;
            }

            for (int i = 0; i < kernel.Length; i++)
                kernel[i] = (float)(kernel[i] / total);

            float[] horizontal = new float[source.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++)
                        sum += kernel[k + radius] * source[row + Math.Min(width - 1, Math.Max(0, x + k))];

                    horizontal[row + x] = sum;
                }
            }

            float[] output = new float[source.Length];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0;
                    for (int k = -radius; k <= radius; k++)
                        sum += kernel[k + radius] * horizontal[(Math.Min(height - 1, Math.Max(0, y + k)) * width) + x];

                    output[(y * width) + x] = sum;
                }
            }

            return output;
        }

        /// <summary>
        /// A salient edge for this tile: the 90th percentile of its brightness gradient, in levels per pixel.
        /// </summary>
        /// <remarks>
        /// EM is textured. On real captured tiles (blurred by <see cref="BlurSigma"/>) the median gradient is about
        /// 7 levels per pixel and the 90th percentile about 13, so a fixed edge threshold means nothing and the
        /// edge snap judges every edge against this number. It is the gradient along each axis over two pixels, taken
        /// from a 256-bin histogram so it costs two passes over the tile.
        /// </remarks>
        internal static double EdgeScaleOf(byte[] blurredGray, int width, int height)
        {
            int[] histogram = new int[256];
            long total = 0;
            for (int y = 1; y < height - 1; y++)
            {
                int row = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int dx = Math.Abs(blurredGray[row + x + 1] - blurredGray[row + x - 1]);
                    int dy = Math.Abs(blurredGray[row + x + width] - blurredGray[row + x - width]);
                    histogram[Math.Min(255, dx)]++;
                    histogram[Math.Min(255, dy)]++;
                    total += 2;
                }
            }

            if (total == 0)
                return 0;

            long target = (long)Math.Ceiling(total * 0.90);
            long seen = 0;
            for (int level = 0; level < histogram.Length; level++)
            {
                seen += histogram[level];
                if (seen >= target)
                    return level / 2.0;
            }

            return 127.5;
        }

        private static byte[] ToBytes(float[] values)
        {
            byte[] bytes = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
                bytes[i] = (byte)Math.Min(255, Math.Max(0, (int)Math.Round(values[i])));

            return bytes;
        }

        /// <summary>
        /// The tiles from this store that cover a rectangle, as an immutable lookup that can be sampled by world position.
        /// </summary>
        /// <param name="downsample">Pixels of the tiles are this many world units wide.</param>
        /// <param name="keyFor">The store key of tile (row, column) for the current view identity.</param>
        /// <param name="coverage">World rectangle the lookup must be able to answer; a margin of one tile is added.</param>
        /// <returns>Null when none of the tiles are held.</returns>
        public TileImageSet? Snapshot(int downsample, Func<int, int, string> keyFor, Geometry.Rectangle coverage)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));

            double tileWorld = SegmentationTileGrid.TileSize * (double)downsample;
            int firstColumn = (int)Math.Floor(coverage.Left / tileWorld) - 1;
            int lastColumn = (int)Math.Floor(coverage.Right / tileWorld) + 1;
            int firstRow = (int)Math.Floor(coverage.Bottom / tileWorld) - 1;
            int lastRow = (int)Math.Floor(coverage.Top / tileWorld) + 1;

            Dictionary<(int Row, int Column), byte[]> held = [];
            List<double> scales = [];
            lock (gate)
            {
                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = firstColumn; column <= lastColumn; column++)
                    {
                        string key = keyFor(row, column);
                        if (tiles.TryGetValue(key, out byte[]? gray))
                        {
                            held[(row, column)] = gray;
                            scales.Add(edgeScales[key]);
                        }
                    }
                }
            }

            if (held.Count == 0)
                return null;

            scales.Sort();
            return new TileImageSet(held, downsample, scales[scales.Count / 2]);
        }
    }

    /// <summary>
    /// Captured tile images, sampled by world position. Immutable: it holds the arrays it was made from, so a later
    /// eviction from <see cref="SegmentationTileImages"/> does not change what an existing refiner sees.
    /// </summary>
    internal sealed class TileImageSet
    {
        private readonly IReadOnlyDictionary<(int Row, int Column), byte[]> tiles;
        private readonly int downsample;

        /// <summary>
        /// Builds a set from tiles keyed by (row, column) of the aligned 1024 grid.
        /// </summary>
        /// <param name="tiles">Blurred grayscale tiles, Y down.</param>
        /// <param name="downsample">World units per pixel.</param>
        /// <param name="edgeScale">A salient edge for these images, levels per pixel (<see cref="SegmentationTileImages.EdgeScaleOf"/>).</param>
        public TileImageSet(IReadOnlyDictionary<(int Row, int Column), byte[]> tiles, int downsample, double edgeScale)
        {
            this.tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            this.downsample = Math.Max(1, downsample);
            EdgeScale = edgeScale;
        }

        /// <summary>
        /// A salient edge for these images, in brightness levels per pixel: the median over the tiles of each tile's
        /// 90th-percentile gradient. What the edge snap compares edges against.
        /// </summary>
        public double EdgeScale { get; }

        /// <summary>Number of tiles in the set.</summary>
        public int Count => tiles.Count;

        /// <summary>World units per image pixel.</summary>
        public int Downsample => downsample;

        /// <summary>
        /// Brightness, 0 to 255, at a world position by bilinear interpolation between pixel centres, or
        /// <see cref="double.NaN"/> when any of the four pixels is outside the tiles held.
        /// </summary>
        /// <remarks>
        /// World X and Y are divided by the downsample to give mosaic pixels (X right, Y up). A tile's image is Y down,
        /// so mosaic row <c>y</c> of tile row <c>r</c> is image row <c>1023 - (y - 1024 r)</c>.
        /// </remarks>
        public double Sample(Vector2 world)
        {
            double u = (world.X / downsample) - 0.5;
            double v = (world.Y / downsample) - 0.5;
            int x0 = (int)Math.Floor(u);
            int y0 = (int)Math.Floor(v);
            double fx = u - x0;
            double fy = v - y0;

            double a = Pixel(x0, y0);
            double b = Pixel(x0 + 1, y0);
            double c = Pixel(x0, y0 + 1);
            double d = Pixel(x0 + 1, y0 + 1);
            if (double.IsNaN(a) || double.IsNaN(b) || double.IsNaN(c) || double.IsNaN(d))
                return double.NaN;

            return (((a * (1 - fx)) + (b * fx)) * (1 - fy)) + (((c * (1 - fx)) + (d * fx)) * fy);
        }

        private double Pixel(int mosaicX, int mosaicY)
        {
            const int size = SegmentationTileGrid.TileSize;
            int column = FloorDivide(mosaicX, size);
            int row = FloorDivide(mosaicY, size);
            if (!tiles.TryGetValue((row, column), out byte[]? gray))
                return double.NaN;

            int x = mosaicX - (column * size);
            int y = (size - 1) - (mosaicY - (row * size));
            return gray[(y * size) + x];
        }

        private static int FloorDivide(int value, int divisor) =>
            (int)Math.Floor(value / (double)divisor);
    }
}
