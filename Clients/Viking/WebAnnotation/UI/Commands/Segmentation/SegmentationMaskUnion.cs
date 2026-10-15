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
    /// The union of several volume-space shapes (squares and polygon rings) that become one SAM2 mask prompt.
    /// </summary>
    /// <remarks>
    /// The segmentation server accepts one mask per <c>Feature</c>, so a client that has several shapes sums
    /// them here into one 1-bit, window-sized bitmap. Used for the background: the inscribed squares of other structures' circles and the
    /// polygons of other structures. A shape is a list of rings filled with the even-odd rule, so a polygon's
    /// holes stay open. Not thread safe; build it, then call <see cref="ToMask"/> from one thread.
    /// </remarks>
    internal sealed class SegmentationMaskUnion
    {
        /// <summary>
        /// Side of the square the mask covers, in pixels at the request's downsample: SAM2's input size, so a mask
        /// larger than this could never be used by one window.
        /// </summary>
        public const int WindowSize = 1024;

        private readonly List<IReadOnlyList<Vector2[]>> shapes = [];

        /// <summary>Number of shapes added.</summary>
        public int Count => shapes.Count;

        /// <summary>True when no shape has been added.</summary>
        public bool IsEmpty => shapes.Count == 0;

        /// <summary>Adds a seed: a circle's disc (a polygon with <see cref="CircleSegments"/> vertices), a polygon with its holes, or a rectangle.</summary>
        public void Add(SegmentationSeed seed)
        {
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
        /// Rasterizes the union into the <see cref="WindowSize"/> square centred on <paramref name="centreVolume"/> at
        /// <paramref name="downsample"/> and returns it as the wire <c>Mask</c>, or null when it is empty or no shape
        /// reaches the square.
        /// </summary>
        /// <remarks>
        /// <c>Bounds</c> are mosaic pixels (world divided by downsample, floored, Y up), always exactly
        /// <see cref="WindowSize"/> square. The PNG is a 1-bit greyscale bitmap of the same size with image row 0
        /// at <c>YMax</c>, as the proto requires. A pixel is in the mask when its centre is inside a shape. Shapes
        /// are clipped to the square, so background further than half a window from the centre is not in the mask;
        /// the background points still cover it. Each point in <paramref name="holePointsVolume"/> clears a disc of
        /// <paramref name="holeRadiusPixels"/> from the mask, so a background click inside a foreground mask is not
        /// overruled by it (the server returns every foreground mask pixel as object). The centre is normally the seed's centre, which is where the
        /// server's first window is.
        /// </remarks>
        public SegmentationServiceTypes.Mask? ToMask(
            int downsample,
            Vector2 centreVolume,
            IReadOnlyList<Vector2>? holePointsVolume = null,
            double holeRadiusPixels = 0)
        {
            byte[]? pixels = Rasterize(downsample, centreVolume, out int xMin, out int yMax, holePointsVolume, holeRadiusPixels);
            return pixels is null ? null : EncodeMask(pixels, xMin, yMax);
        }

        /// <summary>
        /// Rasterizes the union into a <see cref="WindowSize"/> square (row 0 is mosaic <paramref name="yMax"/>, set
        /// pixels are 255) exactly as <see cref="ToMask"/> does, but returns the pixels instead of a PNG. Null when
        /// the union is empty or nothing reaches the square.
        /// </summary>
        /// <param name="downsample">Pixel size in volume units.</param>
        /// <param name="centreVolume">Volume-space point the square is centred on.</param>
        /// <param name="xMin">Mosaic pixel column of the square's left edge.</param>
        /// <param name="yMax">Mosaic pixel row of the square's top edge.</param>
        /// <param name="holePointsVolume">See <see cref="ToMask"/>.</param>
        /// <param name="holeRadiusPixels">See <see cref="ToMask"/>.</param>
        public byte[]? Rasterize(
            int downsample,
            Vector2 centreVolume,
            out int xMin,
            out int yMax,
            IReadOnlyList<Vector2>? holePointsVolume = null,
            double holeRadiusPixels = 0)
        {
            xMin = 0;
            yMax = 0;
            if (shapes.Count == 0 || downsample <= 0)
                return null;

            xMin = (int)Math.Floor(centreVolume.X / downsample) - WindowSize / 2;
            int yMin = (int)Math.Floor(centreVolume.Y / downsample) - WindowSize / 2;
            yMax = yMin + WindowSize - 1;

            byte[] pixels = new byte[WindowSize * WindowSize];
            bool any = false;
            foreach (IReadOnlyList<Vector2[]> shape in shapes)
                any |= Fill(pixels, WindowSize, WindowSize, xMin, yMax, downsample, shape);

            if (any && holePointsVolume is { Count: > 0 } && holeRadiusPixels > 0)
                any = CutHoles(pixels, WindowSize, xMin, yMax, downsample, holePointsVolume, holeRadiusPixels);

            return any ? pixels : null;
        }

        /// <summary>
        /// Wraps a <see cref="WindowSize"/> square of 0/255 pixels (row 0 at mosaic <paramref name="yMax"/>) as the
        /// wire <c>Mask</c>: a 1-bit greyscale PNG with the square's mosaic bounds.
        /// </summary>
        public static SegmentationServiceTypes.Mask EncodeMask(byte[] pixels, int xMin, int yMax)
        {
            L8[] gray = new L8[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                gray[i] = new L8(pixels[i]);

            using MemoryStream stream = new();
            using (var image = SixLabors.ImageSharp.Image.LoadPixelData<L8>(gray, WindowSize, WindowSize))
            {
                image.Save(stream, new PngEncoder
                {
                    ColorType = PngColorType.Grayscale,
                    BitDepth = PngBitDepth.Bit1,
                    CompressionLevel = PngCompressionLevel.BestCompression
                });
            }

            return new SegmentationServiceTypes.Mask
            {
                Bounds = new SegmentationServiceTypes.BoundingBox
                {
                    XMin = xMin,
                    YMin = yMax - WindowSize + 1,
                    XMax = xMin + WindowSize - 1,
                    YMax = yMax
                },
                Png = Google.Protobuf.ByteString.CopyFrom(stream.ToArray())
            };
        }

        /// <summary>Clears a disc of <paramref name="radius"/> pixels around each point; returns whether any pixel is still set.</summary>
        private static bool CutHoles(byte[] pixels, int size, int xMin, int yMax, int downsample, IReadOnlyList<Vector2> points, double radius)
        {
            foreach (Vector2 point in points)
            {
                double centreColumn = point.X / downsample - xMin;
                double centreRow = yMax + 1 - point.Y / downsample;
                int firstRow = Math.Max(0, (int)Math.Floor(centreRow - radius));
                int lastRow = Math.Min(size - 1, (int)Math.Ceiling(centreRow + radius));
                int firstColumn = Math.Max(0, (int)Math.Floor(centreColumn - radius));
                int lastColumn = Math.Min(size - 1, (int)Math.Ceiling(centreColumn + radius));
                for (int row = firstRow; row <= lastRow; row++)
                {
                    for (int column = firstColumn; column <= lastColumn; column++)
                    {
                        double dx = column + 0.5 - centreColumn, dy = row + 0.5 - centreRow;
                        if (dx * dx + dy * dy <= radius * radius)
                            pixels[row * size + column] = 0;
                    }
                }
            }

            return Array.Exists(pixels, value => value != 0);
        }

        /// <summary>
        /// Even-odd scanline fill of one shape into <paramref name="pixels"/> (row 0 is mosaic <paramref name="yMax"/>).
        /// Returns whether any pixel was set.
        /// </summary>
        private static bool Fill(byte[] pixels, int width, int height, int xMin, int yMax, int downsample, IReadOnlyList<Vector2[]> rings)
        {
            double shapeMinY = double.MaxValue, shapeMaxY = double.MinValue;
            foreach (Vector2 vertex in rings[0])
            {
                shapeMinY = Math.Min(shapeMinY, vertex.Y / downsample);
                shapeMaxY = Math.Max(shapeMaxY, vertex.Y / downsample);
            }

            // Row r has its centre at mosaic y = yMax - r + 0.5.
            int firstRow = Math.Max(0, (int)Math.Floor(yMax + 0.5 - shapeMaxY));
            int lastRow = Math.Min(height - 1, (int)Math.Ceiling(yMax + 0.5 - shapeMinY));
            bool any = false;
            List<double> crossings = [];
            for (int row = firstRow; row <= lastRow; row++)
            {
                double y = yMax - row + 0.5;
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
                for (int i = 0; i + 1 < crossings.Count; i += 2)
                {
                    // A pixel column c has its centre at mosaic x = xMin + c + 0.5.
                    int first = Math.Max(0, (int)Math.Ceiling(crossings[i] - xMin - 0.5));
                    int last = Math.Min(width - 1, (int)Math.Floor(crossings[i + 1] - xMin - 0.5));
                    for (int col = first; col <= last; col++)
                    {
                        pixels[row * width + col] = 255;
                        any = true;
                    }
                }
            }

            return any;
        }
    }
}
