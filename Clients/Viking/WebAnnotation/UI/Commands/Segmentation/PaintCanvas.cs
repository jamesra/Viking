#nullable enable

using System;
using Geometry;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// The layer the paintbrush segmentation command paints on: one byte per raster pixel saying whether the user marked it as
    /// object (<see cref="Foreground"/>), as not the object (<see cref="Background"/>) or left it alone (<see cref="None"/>).
    /// A pixel is never both. Placed in volume space exactly like a <see cref="RasterMask"/>, so the layer becomes the foreground and
    /// background masks of a segmentation request without resampling.
    /// </summary>
    /// <remarks>
    /// Raster pixel (column, row) stands for the <see cref="Scale"/> x <see cref="Scale"/> block of mosaic pixels whose low-x, high-y corner is
    /// (<see cref="MaskLayout.XMin"/> + column * Scale, <see cref="MaskLayout.YMax"/> - row * Scale); a mosaic pixel is <see cref="Downsample"/>
    /// volume units wide and rows run downwards (Y up in the volume). Painting tracks a dirty rectangle so the display texture can be
    /// refreshed in part. Used on the UI thread only.
    /// </remarks>
    internal sealed class PaintCanvas
    {
        /// <summary>Pixel value: not painted.</summary>
        public const byte None = 0;

        /// <summary>Pixel value: painted as the object.</summary>
        public const byte Foreground = 1;

        /// <summary>Pixel value: painted as not the object.</summary>
        public const byte Background = 2;

        private readonly MaskLayout layout;
        private int dirtyMinColumn = int.MaxValue, dirtyMaxColumn = -1, dirtyMinRow = int.MaxValue, dirtyMaxRow = -1;

        /// <param name="layout">Where the pixels sit; the raster is <c>layout.RasterWidth</c> x <c>layout.RasterHeight</c>.</param>
        /// <param name="downsample">Volume units per mosaic pixel.</param>
        public PaintCanvas(MaskLayout layout, int downsample)
        {
            if (downsample <= 0)
                throw new ArgumentOutOfRangeException(nameof(downsample));

            this.layout = layout;
            Downsample = downsample;
            Pixels = new byte[layout.RasterWidth * layout.RasterHeight];
        }

        /// <summary>Raster width in pixels.</summary>
        public int Width => layout.RasterWidth;

        /// <summary>Raster height in pixels.</summary>
        public int Height => layout.RasterHeight;

        /// <summary>Mosaic pixels per raster pixel.</summary>
        public int Scale => layout.Scale;

        /// <summary>Volume units per mosaic pixel.</summary>
        public int Downsample { get; }

        /// <summary>Volume units per raster pixel.</summary>
        public double WorldPerPixel => (double)Downsample * layout.Scale;

        /// <summary>The raster, row major, with the pixel values <see cref="None"/>, <see cref="Foreground"/> and <see cref="Background"/>.</summary>
        public byte[] Pixels { get; }

        /// <summary>The rectangle the raster covers, volume space.</summary>
        public Rectangle WorldBounds => new(
            new Vector2(layout.XMin * (double)Downsample, (layout.YMax + 1 - Height * layout.Scale) * (double)Downsample),
            new Vector2((layout.XMin + Width * layout.Scale) * (double)Downsample, (layout.YMax + 1) * (double)Downsample));

        /// <summary>
        /// A canvas covering the area around <paramref name="view"/>: the view plus <paramref name="marginFraction"/> of its size on
        /// every side, so the user can pan a little while painting. Full resolution unless that would be more than
        /// <paramref name="maxRasterSide"/> pixels on a side, in which case the pixels are coarser blocks (see <see cref="MaskScalePolicy"/>).
        /// </summary>
        public static PaintCanvas ForView(Rectangle view, int downsample, int maxRasterSide, double marginFraction = 0.5)
        {
            double marginX = view.Width * marginFraction, marginY = view.Height * marginFraction;
            long xMin = (long)Math.Floor((view.Left - marginX) / downsample);
            long xMax = (long)Math.Ceiling((view.Right + marginX) / downsample);
            long yMin = (long)Math.Floor((view.Bottom - marginY) / downsample);
            long yMax = (long)Math.Ceiling((view.Top + marginY) / downsample);
            return new PaintCanvas(MaskScalePolicy.Plan(xMin, xMax, yMin, yMax, 0, maxRasterSide), downsample);
        }

        /// <summary>The volume position of the centre of raster pixel (<paramref name="column"/>, <paramref name="row"/>).</summary>
        public Vector2 PixelCenter(int column, int row) => new(
            (layout.XMin + (column + 0.5) * layout.Scale) * Downsample,
            (layout.YMax + 1 - (row + 0.5) * layout.Scale) * Downsample);

        /// <summary>The raster pixel holding <paramref name="world"/>, or false when it is outside the canvas.</summary>
        public bool TryPixelAt(Vector2 world, out int column, out int row)
        {
            double c = (world.X / Downsample - layout.XMin) / layout.Scale;
            double r = (layout.YMax + 1 - world.Y / Downsample) / layout.Scale;
            column = (int)Math.Floor(c);
            row = (int)Math.Floor(r);
            return c >= 0 && r >= 0 && column < Width && row < Height;
        }

        /// <summary>The value of the pixel under <paramref name="world"/>; <see cref="None"/> outside the canvas.</summary>
        public byte ValueAt(Vector2 world) =>
            TryPixelAt(world, out int column, out int row) ? Pixels[row * Width + column] : None;

        /// <summary>True when any pixel is <see cref="Foreground"/> or <see cref="Background"/>.</summary>
        public bool HasPaint => Array.Exists(Pixels, value => value != None);

        /// <summary>True when any pixel has <paramref name="value"/>.</summary>
        public bool Has(byte value) => Array.IndexOf(Pixels, value) >= 0;

        /// <summary>
        /// Paints a disc of <paramref name="value"/> centred on <paramref name="center"/> with the given radius in volume units: every pixel
        /// whose centre is inside it. Painting <see cref="Foreground"/> over <see cref="Background"/> (or the reverse) replaces it.
        /// Returns the number of pixels that changed.
        /// </summary>
        public int PaintDisc(Vector2 center, double radius, byte value) => Stamp(center, radius, value, replaceOnly: null);

        /// <summary>
        /// Clears the pixels of type <paramref name="type"/> inside the disc, leaving the other type alone. Returns the number of pixels cleared.
        /// </summary>
        public int EraseDisc(Vector2 center, double radius, byte type) => Stamp(center, radius, None, replaceOnly: type);

        /// <summary>
        /// Paints a stroke from <paramref name="from"/> to <paramref name="to"/>: discs spaced so that no gap shows however fast the
        /// cursor moved. With <paramref name="erase"/> the stroke clears pixels of type <paramref name="value"/> instead of painting it.
        /// </summary>
        public int PaintStroke(Vector2 from, Vector2 to, double radius, byte value, bool erase)
        {
            double length = Vector2.Distance(from, to);
            double spacing = Math.Max(WorldPerPixel, radius * 0.5);
            int steps = length <= 0 ? 1 : (int)Math.Ceiling(length / spacing);
            int changed = 0;
            for (int i = 0; i <= steps; i++)
            {
                double t = steps == 0 ? 1 : (double)i / steps;
                Vector2 at = new(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t);
                changed += erase ? EraseDisc(at, radius, value) : PaintDisc(at, radius, value);
            }

            return changed;
        }

        /// <summary>
        /// Paints <paramref name="value"/> on every canvas pixel whose centre is inside the set pixels of <paramref name="mask"/>, however the two
        /// rasters' scales compare, so an existing mask (a rasterized polygon) can seed the canvas. Returns the number of pixels that changed.
        /// </summary>
        public int Fill(RasterMask mask, byte value)
        {
            Rectangle bounds = mask.WorldBounds;
            int minColumn = Math.Max(0, (int)Math.Floor((bounds.Left / Downsample - layout.XMin) / layout.Scale));
            int maxColumn = Math.Min(Width - 1, (int)Math.Ceiling((bounds.Right / Downsample - layout.XMin) / layout.Scale));
            int minRow = Math.Max(0, (int)Math.Floor((layout.YMax + 1 - bounds.Top / Downsample) / layout.Scale));
            int maxRow = Math.Min(Height - 1, (int)Math.Ceiling((layout.YMax + 1 - bounds.Bottom / Downsample) / layout.Scale));
            int changed = 0;
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    int index = row * Width + column;
                    if (Pixels[index] == value || !mask.Contains(PixelCenter(column, row)))
                        continue;

                    Pixels[index] = value;
                    changed++;
                    MarkDirty(column, row);
                }
            }

            return changed;
        }

        /// <summary>
        /// What is painted under a disc: the number of pixels of each type and the distance from the centre to the nearest pixel centre of
        /// each type (<see cref="double.PositiveInfinity"/> when none).
        /// </summary>
        public (int Foreground, int Background, double NearestForeground, double NearestBackground) Survey(Vector2 center, double radius)
        {
            int fg = 0, bg = 0;
            double nearestFg = double.PositiveInfinity, nearestBg = double.PositiveInfinity;
            ForEachPixelInDisc(center, radius, (index, distance) =>
            {
                byte value = Pixels[index];
                if (value == Foreground)
                {
                    fg++;
                    nearestFg = Math.Min(nearestFg, distance);
                }
                else if (value == Background)
                {
                    bg++;
                    nearestBg = Math.Min(nearestBg, distance);
                }
            });

            return (fg, bg, nearestFg, nearestBg);
        }

        /// <summary>
        /// The pixels of type <paramref name="value"/> as a mask (255 inside) cropped to their bounding box, placed in volume space; null when
        /// there are none. Cropping keeps the mask small for the server: only the painted area is sent.
        /// </summary>
        public RasterMask? ToMask(byte value)
        {
            int minColumn = int.MaxValue, maxColumn = -1, minRow = int.MaxValue, maxRow = -1;
            for (int row = 0; row < Height; row++)
            {
                int offset = row * Width;
                for (int column = 0; column < Width; column++)
                {
                    if (Pixels[offset + column] != value)
                        continue;

                    minColumn = Math.Min(minColumn, column);
                    maxColumn = Math.Max(maxColumn, column);
                    minRow = Math.Min(minRow, row);
                    maxRow = Math.Max(maxRow, row);
                }
            }

            if (maxColumn < 0)
                return null;

            return Crop(minColumn, maxColumn, minRow, maxRow, index => Pixels[index] == value);
        }

        /// <summary>
        /// One cropped mask per separate piece of <paramref name="value"/> paint. Pixels that touch, diagonally too, are one piece, so
        /// strokes that overlap or meet merge into one; pieces ordered top to bottom, left to right by their first pixel.
        /// </summary>
        public System.Collections.Generic.List<RasterMask> ComponentMasks(byte value)
        {
            System.Collections.Generic.List<RasterMask> masks = [];
            bool[] visited = new bool[Pixels.Length];
            System.Collections.Generic.Stack<int> pending = new();
            for (int start = 0; start < Pixels.Length; start++)
            {
                if (visited[start] || Pixels[start] != value)
                    continue;

                System.Collections.Generic.HashSet<int> members = [];
                int minColumn = int.MaxValue, maxColumn = -1, minRow = int.MaxValue, maxRow = -1;
                visited[start] = true;
                pending.Push(start);
                while (pending.Count > 0)
                {
                    int index = pending.Pop();
                    members.Add(index);
                    int column = index % Width, row = index / Width;
                    minColumn = Math.Min(minColumn, column);
                    maxColumn = Math.Max(maxColumn, column);
                    minRow = Math.Min(minRow, row);
                    maxRow = Math.Max(maxRow, row);
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int c = column + dx, r = row + dy;
                            if (c < 0 || r < 0 || c >= Width || r >= Height)
                                continue;

                            int neighbour = r * Width + c;
                            if (visited[neighbour] || Pixels[neighbour] != value)
                                continue;

                            visited[neighbour] = true;
                            pending.Push(neighbour);
                        }
                    }
                }

                masks.Add(Crop(minColumn, maxColumn, minRow, maxRow, members.Contains));
            }

            return masks;
        }

        /// <summary>One interior point (see <see cref="RasterMask.InteriorPoint"/>) for each separate piece of <paramref name="value"/> paint.</summary>
        public System.Collections.Generic.List<Vector2> InteriorPoints(byte value)
        {
            System.Collections.Generic.List<Vector2> points = [];
            foreach (RasterMask piece in ComponentMasks(value))
            {
                if (piece.InteriorPoint() is Vector2 point)
                    points.Add(point);
            }

            return points;
        }

        /// <summary>The raster rectangle as a mask (255 where <paramref name="include"/> says the canvas index belongs), placed in volume space.</summary>
        private RasterMask Crop(int minColumn, int maxColumn, int minRow, int maxRow, Func<int, bool> include)
        {
            int width = maxColumn - minColumn + 1, height = maxRow - minRow + 1;
            byte[] cropped = new byte[width * height];
            for (int row = 0; row < height; row++)
            {
                int source = (minRow + row) * Width + minColumn;
                int target = row * width;
                for (int column = 0; column < width; column++)
                    cropped[target + column] = include(source + column) ? (byte)255 : (byte)0;
            }

            return new RasterMask(
                Downsample,
                layout.XMin + minColumn * layout.Scale,
                layout.YMax - minRow * layout.Scale,
                width,
                height,
                cropped,
                layout.Scale);
        }

        /// <summary>
        /// The raster rectangle changed since the last call, as (column, row, width, height), or false when nothing changed. Resets the
        /// record, so each change is reported once; the display texture refreshes just that part.
        /// </summary>
        public bool TakeDirtyRegion(out int column, out int row, out int width, out int height)
        {
            if (dirtyMaxColumn < 0)
            {
                column = row = width = height = 0;
                return false;
            }

            column = dirtyMinColumn;
            row = dirtyMinRow;
            width = dirtyMaxColumn - dirtyMinColumn + 1;
            height = dirtyMaxRow - dirtyMinRow + 1;
            dirtyMinColumn = dirtyMinRow = int.MaxValue;
            dirtyMaxColumn = dirtyMaxRow = -1;
            return true;
        }

        private int Stamp(Vector2 center, double radius, byte value, byte? replaceOnly)
        {
            int changed = 0;
            ForEachPixelInDisc(center, radius, (index, _) =>
            {
                byte current = Pixels[index];
                if (current == value || (replaceOnly is byte only && current != only))
                    return;

                Pixels[index] = value;
                changed++;
                MarkDirty(index % Width, index / Width);
            });

            return changed;
        }

        private void MarkDirty(int column, int row)
        {
            dirtyMinColumn = Math.Min(dirtyMinColumn, column);
            dirtyMaxColumn = Math.Max(dirtyMaxColumn, column);
            dirtyMinRow = Math.Min(dirtyMinRow, row);
            dirtyMaxRow = Math.Max(dirtyMaxRow, row);
        }

        /// <summary>Calls <paramref name="visit"/> with the index and centre distance of every pixel whose centre is within <paramref name="radius"/>.</summary>
        private void ForEachPixelInDisc(Vector2 center, double radius, Action<int, double> visit)
        {
            if (!(radius >= 0))
                return;

            double perPixel = WorldPerPixel;
            double centerColumn = (center.X / Downsample - layout.XMin) / layout.Scale;
            double centerRow = (layout.YMax + 1 - center.Y / Downsample) / layout.Scale;
            double pixelRadius = radius / perPixel;
            int minColumn = Math.Max(0, (int)Math.Floor(centerColumn - pixelRadius - 0.5));
            int maxColumn = Math.Min(Width - 1, (int)Math.Ceiling(centerColumn + pixelRadius - 0.5));
            int minRow = Math.Max(0, (int)Math.Floor(centerRow - pixelRadius - 0.5));
            int maxRow = Math.Min(Height - 1, (int)Math.Ceiling(centerRow + pixelRadius - 0.5));
            double limit = pixelRadius * pixelRadius;
            for (int row = minRow; row <= maxRow; row++)
            {
                double dy = row + 0.5 - centerRow;
                for (int column = minColumn; column <= maxColumn; column++)
                {
                    double dx = column + 0.5 - centerColumn;
                    double squared = dx * dx + dy * dy;
                    if (squared <= limit)
                        visit(row * Width + column, Math.Sqrt(squared) * perPixel);
                }
            }
        }
    }
}
