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
    /// them here. Used for the background: the inscribed squares of other structures' circles and the
    /// polygons of other structures. A shape is a list of rings filled with the even-odd rule, so a polygon's
    /// holes stay open. Not thread safe; build it, then call <see cref="ToMask"/> from one thread.
    /// </remarks>
    internal sealed class SegmentationMaskUnion
    {
        /// <summary>
        /// Largest mask bounds, in pixels at the request's downsample, that <see cref="ToMask"/> will rasterize.
        /// A union spread over more than this (for example annotations on a far-away part of a huge section) is
        /// not sent, because the bitmap would cost more than the hint is worth.
        /// </summary>
        public const long MaxPixels = 16_000_000;

        private readonly List<IReadOnlyList<Vector2[]>> shapes = [];

        /// <summary>Number of shapes added.</summary>
        public int Count => shapes.Count;

        /// <summary>True when no shape has been added.</summary>
        public bool IsEmpty => shapes.Count == 0;

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
        /// Rasterizes the union at <paramref name="downsample"/> and returns it as the wire <c>Mask</c>, or null
        /// when it is empty, bigger than <see cref="MaxPixels"/>, or covers no pixel centre.
        /// </summary>
        /// <remarks>
        /// <c>Bounds</c> are mosaic pixels (world divided by downsample, floored, Y up) and the PNG is
        /// exactly bounds-sized with image row 0 at <c>YMax</c>, as the proto requires. A pixel is in the mask
        /// when its centre is inside a shape.
        /// </remarks>
        public SegmentationServiceTypes.Mask? ToMask(int downsample)
        {
            if (shapes.Count == 0 || downsample <= 0)
                return null;

            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            foreach (IReadOnlyList<Vector2[]> shape in shapes)
            {
                foreach (Vector2 vertex in shape[0])
                {
                    minX = Math.Min(minX, vertex.X);
                    maxX = Math.Max(maxX, vertex.X);
                    minY = Math.Min(minY, vertex.Y);
                    maxY = Math.Max(maxY, vertex.Y);
                }
            }

            int xMin = (int)Math.Floor(minX / downsample);
            int xMax = (int)Math.Floor(maxX / downsample);
            int yMin = (int)Math.Floor(minY / downsample);
            int yMax = (int)Math.Floor(maxY / downsample);
            long width = (long)xMax - xMin + 1;
            long height = (long)yMax - yMin + 1;
            if (width * height > MaxPixels)
                return null;

            byte[] pixels = new byte[width * height];
            bool any = false;
            foreach (IReadOnlyList<Vector2[]> shape in shapes)
                any |= Fill(pixels, (int)width, (int)height, xMin, yMax, downsample, shape);

            if (!any)
                return null;

            using MemoryStream stream = new();
            L8[] gray = new L8[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
                gray[i] = new L8(pixels[i]);
            using (var image = SixLabors.ImageSharp.Image.LoadPixelData<L8>(gray, (int)width, (int)height))
            {
                image.Save(stream, new PngEncoder
                {
                    ColorType = PngColorType.Grayscale,
                    BitDepth = PngBitDepth.Bit8,
                    CompressionLevel = PngCompressionLevel.BestSpeed
                });
            }

            return new SegmentationServiceTypes.Mask
            {
                Bounds = new SegmentationServiceTypes.BoundingBox { XMin = xMin, YMin = yMin, XMax = xMax, YMax = yMax },
                Png = Google.Protobuf.ByteString.CopyFrom(stream.ToArray())
            };
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
