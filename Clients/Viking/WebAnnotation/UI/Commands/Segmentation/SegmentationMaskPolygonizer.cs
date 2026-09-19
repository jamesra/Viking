using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Converts a SAM2 binary mask to world-space <see cref="GridPolygon"/>s.
    /// Huge near-full-frame masks are skipped. Remaining masks are downsampled before
    /// morphological cleanup and marching squares so a 4K capture does not run open/close at full res.
    /// </summary>
    internal static class SegmentationMaskPolygonizer
    {
        private const double SimplificationTolerancePixels = 2.0;

        /// <summary>
        /// Skip polygonize when foreground exceeds this fraction of the capture and the image is large enough.
        /// Prevents minute-long marching squares on a near-full-frame blob.
        /// </summary>
        internal const double HugeMaskForegroundFraction = 0.40;

        /// <summary>
        /// Images smaller than this skip the huge-mask check so small captures still polygonize.
        /// </summary>
        internal const int HugeMaskMinImagePixels = 250_000;

        /// <summary>
        /// Marching squares runs on a mask no larger than this; extra resolution is downsampled away.
        /// </summary>
        internal const int PolygonizeMaxMaskPixels = 512 * 512;

        /// <summary>
        /// Timing and pixel counts from optional morphological cleanup, used by segmentation profile logs.
        /// </summary>
        internal readonly struct CleanupStats
        {
            public CleanupStats(long elapsedMilliseconds, int foregroundPixelsBefore, int foregroundPixelsAfter)
            {
                ElapsedMilliseconds = elapsedMilliseconds;
                ForegroundPixelsBefore = foregroundPixelsBefore;
                ForegroundPixelsAfter = foregroundPixelsAfter;
            }

            public long ElapsedMilliseconds { get; }
            public int ForegroundPixelsBefore { get; }
            public int ForegroundPixelsAfter { get; }
        }

        /// <summary>
        /// Polygonizes a decoded mask. Returns empty when the mask is missing or huge.
        /// If cleanup produces an unusable ring, retries on the original mask.
        /// </summary>
        public static IReadOnlyList<GridPolygon> CreatePolygons(
            byte[] maskData,
            int maskWidth,
            int maskHeight,
            int offsetX,
            int offsetY,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds,
            double holeDropFraction,
            IReadOnlyList<GridVector2> preserveHolesContainingWorldPoints = null,
            int edgeCleanupRadius = 0)
        {
            return CreatePolygons(
                maskData,
                maskWidth,
                maskHeight,
                offsetX,
                offsetY,
                imageWidth,
                imageHeight,
                viewportBounds,
                holeDropFraction,
                preserveHolesContainingWorldPoints,
                edgeCleanupRadius,
                out _);
        }

        public static IReadOnlyList<GridPolygon> CreatePolygons(
            byte[] maskData,
            int maskWidth,
            int maskHeight,
            int offsetX,
            int offsetY,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds,
            double holeDropFraction,
            IReadOnlyList<GridVector2> preserveHolesContainingWorldPoints,
            int edgeCleanupRadius,
            out CleanupStats cleanupStats)
        {
            cleanupStats = new CleanupStats(0, 0, 0);
            if (maskData is null || maskData.Length != maskWidth * maskHeight)
                return [];

            int foregroundBefore = CountForeground(maskData);
            if (ShouldSkipHugeMask(foregroundBefore, imageWidth, imageHeight))
            {
                cleanupStats = new CleanupStats(0, foregroundBefore, foregroundBefore);
                return [];
            }

            var (polygonMask, polygonWidth, polygonHeight, scale) =
                DownsampleUntil(maskData, maskWidth, maskHeight, PolygonizeMaxMaskPixels);

            byte[] workingMask = polygonMask;
            long cleanupMs = 0;
            if (edgeCleanupRadius > 0)
            {
                int scaledRadius = scale <= 1
                    ? edgeCleanupRadius
                    : Math.Max(1, (int)Math.Round(edgeCleanupRadius / (double)scale));
                Stopwatch cleanupTimer = Stopwatch.StartNew();
                workingMask = CleanMask(polygonMask, polygonWidth, polygonHeight, scaledRadius);
                cleanupMs = cleanupTimer.ElapsedMilliseconds;
            }

            int foregroundAfter = CountForeground(workingMask);
            cleanupStats = new CleanupStats(cleanupMs, foregroundBefore, foregroundAfter);

            IReadOnlyList<GridPolygon> cleanedPolygons = PolygonizeMask(
                workingMask,
                polygonWidth,
                polygonHeight,
                offsetX,
                offsetY,
                imageWidth,
                imageHeight,
                viewportBounds,
                holeDropFraction,
                preserveHolesContainingWorldPoints,
                scale);

            if (IsUsable(cleanedPolygons))
                return cleanedPolygons;

            if (edgeCleanupRadius > 0 && !ReferenceEquals(workingMask, polygonMask))
            {
                IReadOnlyList<GridPolygon> originalPolygons = PolygonizeMask(
                    polygonMask,
                    polygonWidth,
                    polygonHeight,
                    offsetX,
                    offsetY,
                    imageWidth,
                    imageHeight,
                    viewportBounds,
                    holeDropFraction,
                    preserveHolesContainingWorldPoints,
                    scale);
                if (IsUsable(originalPolygons))
                    return originalPolygons;
            }

            return cleanedPolygons;
        }

        /// <summary>
        /// True when the mask covers more than <see cref="HugeMaskForegroundFraction"/> of a large capture.
        /// Small images always return false.
        /// </summary>
        public static bool ShouldSkipHugeMask(int foregroundPixels, int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || foregroundPixels <= 0)
                return false;

            int imagePixels = imageWidth * imageHeight;
            if (imagePixels < HugeMaskMinImagePixels)
                return false;

            return foregroundPixels > imagePixels * HugeMaskForegroundFraction;
        }

        /// <summary>
        /// Morphological open then close with a square kernel. Pads so edge pixels are not eroded away.
        /// </summary>
        internal static byte[] CleanMask(byte[] maskData, int width, int height, int radius)
        {
            if (maskData is null || radius <= 0 || width <= 0 || height <= 0)
                return maskData;

            int pad = radius;
            int paddedWidth = width + (2 * pad);
            int paddedHeight = height + (2 * pad);
            bool[] padded = new bool[paddedWidth * paddedHeight];
            for (int y = 0; y < height; y++)
            {
                int sourceRow = y * width;
                int destRow = (y + pad) * paddedWidth + pad;
                for (int x = 0; x < width; x++)
                    padded[destRow + x] = maskData[sourceRow + x] > 0;
            }

            (int dx, int dy)[] kernel = BuildSquareKernel(radius);
            bool[] opened = Dilate(Erode(padded, paddedWidth, paddedHeight, kernel), paddedWidth, paddedHeight, kernel);
            bool[] closed = Erode(Dilate(opened, paddedWidth, paddedHeight, kernel), paddedWidth, paddedHeight, kernel);

            byte[] result = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                int destRow = y * width;
                int sourceRow = (y + pad) * paddedWidth + pad;
                for (int x = 0; x < width; x++)
                    result[destRow + x] = closed[sourceRow + x] ? (byte)255 : (byte)0;
            }

            return result;
        }

        /// <summary>
        /// Marching-squares the (possibly downsampled) mask, keeps the largest exterior, then maps to world.
        /// </summary>
        private static IReadOnlyList<GridPolygon> PolygonizeMask(
            byte[] maskData,
            int maskWidth,
            int maskHeight,
            int offsetX,
            int offsetY,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds,
            double holeDropFraction,
            IReadOnlyList<GridVector2> preserveHolesContainingWorldPoints,
            int scale = 1)
        {
            bool[] mask = Array.ConvertAll(maskData, value => value > 0);
            List<GridVector2[]> contours = [.. MarchingSquares.FindContours(mask, maskWidth, maskHeight)
                .Select(ring => NormalizeRing(ring, offsetX, offsetY, scale))
                .Where(ring => ring.Length >= 4)];

            if (contours.Count == 0)
                return [];

            GridVector2[] exteriorRing = contours
                .OrderByDescending(ring => Math.Abs(ring.PolygonArea()))
                .First();

            GridPolygon pixelPolygon;
            try
            {
                pixelPolygon = new GridPolygon(exteriorRing);
            }
            catch (ArgumentException)
            {
                return [];
            }

            GridVector2[] keepHolePoints = ToPixelPoints(
                preserveHolesContainingWorldPoints,
                imageWidth,
                imageHeight,
                viewportBounds);

            double exteriorArea = pixelPolygon.Area;
            foreach (GridVector2[] ring in contours.Where(ring => !ReferenceEquals(ring, exteriorRing)))
            {
                double ringArea = Math.Abs(ring.PolygonArea());
                if (ringArea <= 0)
                    continue;

                GridPolygon holePolygon;
                try
                {
                    holePolygon = new GridPolygon(ring);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                bool belowDropThreshold = ringArea / exteriorArea < holeDropFraction;
                if (belowDropThreshold && !ContainsAny(holePolygon, keepHolePoints))
                    continue;

                if (!pixelPolygon.Contains(ring[0]))
                    continue;

                try
                {
                    pixelPolygon.AddInteriorRing(ring);
                }
                catch (ArgumentException)
                {
                    // A contour touching another contour is not a valid interior ring.
                }
            }

            GridPolygon simplifiedPolygon = TrySimplify(pixelPolygon, scale);
            GridPolygon worldPolygon = TransformToWorld(
                simplifiedPolygon,
                imageWidth,
                imageHeight,
                viewportBounds);

            return [worldPolygon];
        }

        private static bool IsUsable(IReadOnlyList<GridPolygon> polygons) =>
            polygons is not null &&
            polygons.Count > 0 &&
            !polygons[0].ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED);

        private static int CountForeground(byte[] maskData)
        {
            int count = 0;
            for (int i = 0; i < maskData.Length; i++)
            {
                if (maskData[i] > 0)
                    count++;
            }

            return count;
        }

        private static (int dx, int dy)[] BuildSquareKernel(int radius)
        {
            List<(int dx, int dy)> offsets = [];
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                    offsets.Add((dx, dy));
            }

            return [.. offsets];
        }

        private static bool[] Erode(bool[] source, int width, int height, (int dx, int dy)[] kernel)
        {
            bool[] output = new bool[source.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!source[row + x])
                        continue;

                    bool keep = true;
                    foreach ((int dx, int dy) in kernel)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height || !source[(ny * width) + nx])
                        {
                            keep = false;
                            break;
                        }
                    }

                    output[row + x] = keep;
                }
            }

            return output;
        }

        private static bool[] Dilate(bool[] source, int width, int height, (int dx, int dy)[] kernel)
        {
            bool[] output = new bool[source.Length];
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (!source[row + x])
                        continue;

                    foreach ((int dx, int dy) in kernel)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height)
                            output[(ny * width) + nx] = true;
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Repeatedly halves the mask until it fits <paramref name="maxPixels"/>. Scale is applied in <see cref="NormalizeRing"/>.
        /// </summary>
        private static (byte[] data, int width, int height, int scale) DownsampleUntil(
            byte[] maskData,
            int width,
            int height,
            int maxPixels)
        {
            byte[] data = maskData;
            int scale = 1;
            while (width * height > maxPixels && width >= 2 && height >= 2)
            {
                data = DownsampleByTwo(data, width, height);
                width /= 2;
                height /= 2;
                scale *= 2;
            }

            return (data, width, height, scale);
        }

        /// <summary>
        /// 2×2 OR downsample so thin foreground is not dropped.
        /// </summary>
        internal static byte[] DownsampleByTwo(byte[] maskData, int width, int height)
        {
            int newWidth = width / 2;
            int newHeight = height / 2;
            byte[] output = new byte[newWidth * newHeight];
            for (int y = 0; y < newHeight; y++)
            {
                int sourceY = y * 2;
                int destRow = y * newWidth;
                for (int x = 0; x < newWidth; x++)
                {
                    int sourceX = x * 2;
                    bool on =
                        maskData[(sourceY * width) + sourceX] > 0 ||
                        maskData[(sourceY * width) + sourceX + 1] > 0 ||
                        maskData[((sourceY + 1) * width) + sourceX] > 0 ||
                        maskData[((sourceY + 1) * width) + sourceX + 1] > 0;
                    output[destRow + x] = on ? (byte)255 : (byte)0;
                }
            }

            return output;
        }

        /// <summary>
        /// Maps downsampled contour vertices back to full-mask pixel space, then closes the ring.
        /// </summary>
        private static GridVector2[] NormalizeRing(IEnumerable<GridVector2> ring, int offsetX, int offsetY, int scale)
        {
            int safeScale = scale < 1 ? 1 : scale;
            GridVector2[] translated = [.. ring.Select(point =>
                new GridVector2((point.X * safeScale) + offsetX, (point.Y * safeScale) + offsetY))];
            return [.. translated.RemoveAdjacentDuplicates().EnsureClosedRing()];
        }

        /// <summary>
        /// Douglas-Peucker in mask-pixel space. Tolerance scales with downsample so a 2px rule
        /// still collapses staircases after <see cref="NormalizeRing"/> multiplies vertices by <paramref name="scale"/>.
        /// Catmull-Rom <c>Simplify</c> is not used: it fits the interpolated staircase and leaves traces dense.
        /// </summary>
        private static GridPolygon TrySimplify(GridPolygon polygon, int scale)
        {
            double tolerance = SimplificationTolerancePixels * Math.Max(1, scale);
            return SimplifyRings(polygon, tolerance);
        }

        /// <summary>
        /// Reduces each ring with Douglas-Peucker, then rebuilds the polygon.
        /// Retries at half tolerance when the result is invalid so a dense original is not kept
        /// just because the first pass self-intersected.
        /// </summary>
        internal static GridPolygon SimplifyRings(GridPolygon polygon, double tolerance)
        {
            if (polygon is null || tolerance <= 0)
                return polygon;

            double current = tolerance;
            double minimum = Math.Max(tolerance * 0.125, 0.25);
            while (current >= minimum)
            {
                if (TryBuildSimplifiedPolygon(polygon, current, out GridPolygon simplified))
                    return simplified;

                current *= 0.5;
            }

            return polygon;
        }

        private static bool TryBuildSimplifiedPolygon(GridPolygon polygon, double tolerance, out GridPolygon simplified)
        {
            simplified = null;
            try
            {
                GridVector2[] exterior = SimplifyClosedRing(polygon.ExteriorRing, tolerance);
                if (exterior is null)
                    return false;

                GridPolygon output = new(exterior);
                foreach (GridVector2[] hole in polygon.InteriorRings)
                {
                    GridVector2[] simplifiedHole = SimplifyClosedRing(hole, tolerance);
                    try
                    {
                        output.AddInteriorRing(simplifiedHole ?? hole);
                    }
                    catch (ArgumentException)
                    {
                        try
                        {
                            output.AddInteriorRing(hole);
                        }
                        catch (ArgumentException)
                        {
                        }
                    }
                }

                if (output.ExteriorSegments.SelfIntersects(LineSetOrdering.CLOSED))
                    return false;

                simplified = output;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static GridVector2[] SimplifyClosedRing(IReadOnlyList<GridVector2> ring, double tolerance)
        {
            if (ring is null || ring.Count < 4)
                return null;

            List<GridVector2> reduced = ring.ToList().DouglasPeuckerReduction(tolerance);
            List<GridVector2> unique = ((ICollection<GridVector2>)reduced).RemoveAdjacentDuplicates();
            GridVector2[] closed = [.. unique.EnsureClosedRing()];
            return closed.IsValidClosedRing() ? closed : null;
        }

        /// <summary>
        /// Converts mask-pixel rings to world space. Pixel Y is top-origin; Viking world Y increases upward.
        /// Tests must assert Contains against the flipped Y (e.g. 300,500 not 300,300 for a 600-tall capture).
        /// </summary>
        private static GridPolygon TransformToWorld(
            GridPolygon pixelPolygon,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds)
        {
            GridVector2[] exterior = TransformRing(
                pixelPolygon.ExteriorRing,
                imageWidth,
                imageHeight,
                viewportBounds);
            GridPolygon output = new(exterior);

            foreach (GridVector2[] interior in pixelPolygon.InteriorRings)
            {
                try
                {
                    output.AddInteriorRing(TransformRing(interior, imageWidth, imageHeight, viewportBounds));
                }
                catch (ArgumentException)
                {
                    // Simplification can collapse a very narrow hole after coordinate conversion.
                }
            }

            return output;
        }

        private static GridVector2[] ToPixelPoints(
            IReadOnlyList<GridVector2> worldPoints,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds)
        {
            if (worldPoints is null || worldPoints.Count == 0 || imageWidth <= 0 || imageHeight <= 0)
                return [];

            return [.. worldPoints.Select(point => WorldToPixel(point, imageWidth, imageHeight, viewportBounds))];
        }

        private static bool ContainsAny(GridPolygon polygon, IReadOnlyList<GridVector2> points)
        {
            if (points is null || points.Count == 0)
                return false;

            foreach (GridVector2 point in points)
            {
                if (polygon.Contains(point))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Inverse of <see cref="TransformRing"/>: world-up to mask top-origin pixels.
        /// </summary>
        private static GridVector2 WorldToPixel(
            GridVector2 world,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds)
        {
            double normalizedX = (world.X - viewportBounds.Left) / viewportBounds.Width;
            double normalizedY = (world.Y - viewportBounds.Bottom) / viewportBounds.Height;
            return new GridVector2(
                normalizedX * imageWidth,
                imageHeight - (normalizedY * imageHeight));
        }

        private static GridVector2[] TransformRing(
            IEnumerable<GridVector2> ring,
            int imageWidth,
            int imageHeight,
            GridRectangle viewportBounds)
        {
            return [.. ring.Select(point =>
            {
                double normalizedX = point.X / imageWidth;
                double normalizedY = (imageHeight - point.Y) / imageHeight;
                return new GridVector2(
                    viewportBounds.Left + (normalizedX * viewportBounds.Width),
                    viewportBounds.Bottom + (normalizedY * viewportBounds.Height));
            })];
        }
    }
}
