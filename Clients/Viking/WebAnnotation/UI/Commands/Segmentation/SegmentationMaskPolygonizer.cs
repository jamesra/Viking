using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Converts a SAM2 probability mask to world-space <see cref="Polygon"/>s.
    /// Each byte is a probability in 0–255. The contour is the logit-zero crossing
    /// at <see cref="SoftMaskIsoLevel"/>, interpolated between pixels.
    /// Callers reduce vertex count with CreatedShapeSimplify (mask pixel-travel).
    /// This type does not simplify.
    /// </summary>
    internal static class SegmentationMaskPolygonizer
    {
        /// <summary>
        /// Contour level for a uint8 probability mask. Probability 0.5, SAM2's logit zero, encodes as 127.5.
        /// </summary>
        internal const float SoftMaskIsoLevel = 127.5f;

        /// <summary>
        /// First uint8 value on the inside of <see cref="SoftMaskIsoLevel"/>.
        /// Rounded probability 0.5 is 128. Binary masks of 0 and 255 still count 255 as inside.
        /// </summary>
        internal const byte SoftMaskForeground = 128;
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
        /// Polygonizes a decoded mask. Returns empty when the mask is missing.
        /// If cleanup produces an unusable ring, retries on the original mask.
        /// </summary>
        public static IReadOnlyList<Polygon> CreatePolygons(
            byte[] maskData,
            int maskWidth,
            int maskHeight,
            int offsetX,
            int offsetY,
            int imageWidth,
            int imageHeight,
            Rectangle viewportBounds,
            double holeDropFraction,
            IReadOnlyList<Vector2> preserveHolesContainingWorldPoints,
            int edgeCleanupRadius,
            out CleanupStats cleanupStats,
            IReadOnlyList<Vector2> keepComponentsContainingWorldPoints = null)
        {
            cleanupStats = new CleanupStats(0, 0, 0);
            if (maskData is null || maskData.Length != maskWidth * maskHeight)
                return [];

            if (keepComponentsContainingWorldPoints is { Count: > 0 })
            {
                Vector2[] clickPixels = ToPixelPoints(
                    keepComponentsContainingWorldPoints,
                    imageWidth,
                    imageHeight,
                    viewportBounds);
                (maskData, maskWidth, maskHeight, offsetX, offsetY) = RestrictToClickedComponents(
                    maskData,
                    maskWidth,
                    maskHeight,
                    offsetX,
                    offsetY,
                    clickPixels,
                    Math.Max(0, edgeCleanupRadius) + 2);
            }

            int foregroundBefore = CountForeground(maskData);

            byte[] workingMask = maskData;
            long cleanupMs = 0;
            if (edgeCleanupRadius > 0)
            {
                Stopwatch cleanupTimer = Stopwatch.StartNew();
                workingMask = ApplyEdgeCleanup(maskData, maskWidth, maskHeight, edgeCleanupRadius);
                cleanupMs = cleanupTimer.ElapsedMilliseconds;
            }

            int foregroundAfter = CountForeground(workingMask);
            cleanupStats = new CleanupStats(cleanupMs, foregroundBefore, foregroundAfter);

            IReadOnlyList<Polygon> cleanedPolygons = PolygonizeMask(
                workingMask,
                maskWidth,
                maskHeight,
                offsetX,
                offsetY,
                imageWidth,
                imageHeight,
                viewportBounds,
                holeDropFraction,
                preserveHolesContainingWorldPoints);

            if (IsUsable(cleanedPolygons))
                return cleanedPolygons;

            if (edgeCleanupRadius > 0 && !ReferenceEquals(workingMask, maskData))
            {
                IReadOnlyList<Polygon> originalPolygons = PolygonizeMask(
                    maskData,
                    maskWidth,
                    maskHeight,
                    offsetX,
                    offsetY,
                    imageWidth,
                    imageHeight,
                    viewportBounds,
                    holeDropFraction,
                    preserveHolesContainingWorldPoints);
                if (IsUsable(originalPolygons))
                    return originalPolygons;
            }

            return cleanedPolygons;
        }

        /// <summary>
        /// Hit-tests the mask at the clicked pixels and keeps only the connected pieces (8-connected,
        /// foreground at <see cref="SoftMaskForeground"/>) that a click lands on, cropped to their bounding
        /// box plus <paramref name="pad"/>. The polygonizer otherwise keeps the largest contour, which is not
        /// always the clicked piece, and it scans every pixel of the mosaic for a mask that is often a small
        /// blob. Pixels touching a kept piece keep their soft values so the contour still interpolates the
        /// same edge; everything else becomes 0. Returns the input unchanged when no click lands on foreground,
        /// because the old largest-contour choice is a better guess than an empty mask.
        /// </summary>
        /// <param name="clickPixels">Clicks in capture-image pixels (top-origin), the frame <paramref name="offsetX"/> is in.</param>
        internal static (byte[] Mask, int Width, int Height, int OffsetX, int OffsetY) RestrictToClickedComponents(
            byte[] mask,
            int width,
            int height,
            int offsetX,
            int offsetY,
            IReadOnlyList<Vector2> clickPixels,
            int pad)
        {
            (byte[], int, int, int, int) unchanged = (mask, width, height, offsetX, offsetY);
            if (mask is null || clickPixels is null || clickPixels.Count == 0 || width <= 0 || height <= 0)
                return unchanged;

            bool[] keep = new bool[mask.Length];
            Stack<int> pending = new();
            foreach (Vector2 click in clickPixels)
            {
                int x = (int)Math.Floor(click.X) - offsetX;
                int y = (int)Math.Floor(click.Y) - offsetY;
                if (x < 0 || y < 0 || x >= width || y >= height)
                    continue;

                int seed = (y * width) + x;
                if (mask[seed] >= SoftMaskForeground && !keep[seed])
                {
                    keep[seed] = true;
                    pending.Push(seed);
                }
            }

            if (pending.Count == 0)
                return unchanged;

            int minX = width, minY = height, maxX = -1, maxY = -1;
            while (pending.Count > 0)
            {
                int index = pending.Pop();
                int px = index % width;
                int py = index / width;
                minX = Math.Min(minX, px);
                maxX = Math.Max(maxX, px);
                minY = Math.Min(minY, py);
                maxY = Math.Max(maxY, py);
                for (int ny = Math.Max(0, py - 1); ny <= Math.Min(height - 1, py + 1); ny++)
                {
                    for (int nx = Math.Max(0, px - 1); nx <= Math.Min(width - 1, px + 1); nx++)
                    {
                        int neighbor = (ny * width) + nx;
                        if (!keep[neighbor] && mask[neighbor] >= SoftMaskForeground)
                        {
                            keep[neighbor] = true;
                            pending.Push(neighbor);
                        }
                    }
                }
            }

            int cropLeft = Math.Max(0, minX - pad);
            int cropTop = Math.Max(0, minY - pad);
            int cropRight = Math.Min(width - 1, maxX + pad);
            int cropBottom = Math.Min(height - 1, maxY + pad);
            int cropWidth = cropRight - cropLeft + 1;
            int cropHeight = cropBottom - cropTop + 1;
            byte[] cropped = new byte[cropWidth * cropHeight];
            for (int y = cropTop; y <= cropBottom; y++)
            {
                for (int x = cropLeft; x <= cropRight; x++)
                {
                    if (!TouchesKept(keep, width, height, x, y))
                        continue;

                    cropped[((y - cropTop) * cropWidth) + (x - cropLeft)] = mask[(y * width) + x];
                }
            }

            return (cropped, cropWidth, cropHeight, offsetX + cropLeft, offsetY + cropTop);
        }

        private static bool TouchesKept(bool[] keep, int width, int height, int x, int y)
        {
            for (int ny = Math.Max(0, y - 1); ny <= Math.Min(height - 1, y + 1); ny++)
            {
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(width - 1, x + 1); nx++)
                {
                    if (keep[(ny * width) + nx])
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes wisps and fills notches on the decision boundary, then writes those edits
        /// back onto the probability field. Pixels that remain inside keep their original
        /// soft values so the contour still interpolates the SAM2 edge.
        /// </summary>
        internal static byte[] ApplyEdgeCleanup(byte[] field, int width, int height, int radius)
        {
            if (field is null || radius <= 0 || width <= 0 || height <= 0)
                return field;

            byte[] binary = new byte[field.Length];
            for (int i = 0; i < field.Length; i++)
                binary[i] = field[i] >= SoftMaskForeground ? (byte)255 : (byte)0;

            byte[] cleaned = CleanMask(binary, width, height, radius);
            byte[] output = new byte[field.Length];
            for (int i = 0; i < field.Length; i++)
            {
                if (cleaned[i] == 0)
                    output[i] = 0;
                else if (field[i] >= SoftMaskForeground)
                    output[i] = field[i];
                else
                    output[i] = 255;
            }

            return output;
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

            bool[] opened = Dilate(Erode(padded, paddedWidth, paddedHeight, radius), paddedWidth, paddedHeight, radius);
            bool[] closed = Erode(Dilate(opened, paddedWidth, paddedHeight, radius), paddedWidth, paddedHeight, radius);

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
        /// Interpolates the probability iso-contour, keeps the largest exterior, then maps to world.
        /// </summary>
        private static IReadOnlyList<Polygon> PolygonizeMask(
            byte[] maskData,
            int maskWidth,
            int maskHeight,
            int offsetX,
            int offsetY,
            int imageWidth,
            int imageHeight,
            Rectangle viewportBounds,
            double holeDropFraction,
            IReadOnlyList<Vector2> preserveHolesContainingWorldPoints)
        {
            float[] field = new float[maskData.Length];
            for (int i = 0; i < maskData.Length; i++)
                field[i] = maskData[i];

            List<Vector2[]> contours = [.. MarchingSquares.FindContours(field, maskWidth, maskHeight, SoftMaskIsoLevel)
                .Select(ring => NormalizeRing(ring, offsetX, offsetY))
                .Where(ring => ring.Length >= 4)];

            if (contours.Count == 0)
                return [];

            Vector2[] exteriorRing = contours
                .OrderByDescending(ring => Math.Abs(ring.PolygonArea()))
                .First();

            Polygon pixelPolygon;
            try
            {
                pixelPolygon = new Polygon(exteriorRing);
            }
            catch (ArgumentException)
            {
                return [];
            }

            Vector2[] keepHolePoints = ToPixelPoints(
                preserveHolesContainingWorldPoints,
                imageWidth,
                imageHeight,
                viewportBounds);

            double exteriorArea = pixelPolygon.Area;
            foreach (Vector2[] ring in contours.Where(ring => !ReferenceEquals(ring, exteriorRing)))
            {
                double ringArea = Math.Abs(ring.PolygonArea());
                if (ringArea <= 0)
                    continue;

                Polygon holePolygon;
                try
                {
                    holePolygon = new Polygon(ring);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                bool belowDropThreshold = ringArea / exteriorArea < holeDropFraction;
                if (belowDropThreshold && !ContainsAny(holePolygon, keepHolePoints))
                    continue;

                if (!pixelPolygon.Covers(ring[0]))
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

            Polygon worldPolygon = TransformToWorld(
                pixelPolygon,
                imageWidth,
                imageHeight,
                viewportBounds);

            return [worldPolygon];
        }

        private static bool IsUsable(IReadOnlyList<Polygon> polygons) =>
            polygons is not null &&
            polygons.Count > 0 &&
            !polygons[0].ExteriorSegments.SelfIntersects(LineSetOrdering.Closed);

        private static int CountForeground(byte[] maskData)
        {
            int count = 0;
            for (int i = 0; i < maskData.Length; i++)
            {
                if (maskData[i] >= SoftMaskForeground)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Erosion by a square of half-width <paramref name="radius"/>. A pixel survives only when every pixel in the
        /// square is on and the square lies fully inside the image (outside counts as off). A square is separable, so
        /// a horizontal pass then a vertical pass equals the 2D result in O(width x height) instead of
        /// O(width x height x (2r+1)^2).
        /// </summary>
        private static bool[] Erode(bool[] source, int width, int height, int radius) =>
            SquarePass(SquarePass(source, width, height, radius, horizontal: true, erode: true),
                width, height, radius, horizontal: false, erode: true);

        /// <summary>
        /// Dilation by a square of half-width <paramref name="radius"/>: a pixel is on when any in-bounds pixel in the
        /// square is on. Separable like <see cref="Erode"/>.
        /// </summary>
        private static bool[] Dilate(bool[] source, int width, int height, int radius) =>
            SquarePass(SquarePass(source, width, height, radius, horizontal: true, erode: false),
                width, height, radius, horizontal: false, erode: false);

        /// <summary>
        /// One 1-D pass of a separable square morphology along rows or columns, using a running count over each line
        /// so the cost does not depend on <paramref name="radius"/>.
        /// </summary>
        private static bool[] SquarePass(bool[] source, int width, int height, int radius, bool horizontal, bool erode)
        {
            bool[] output = new bool[source.Length];
            int lines = horizontal ? height : width;
            int length = horizontal ? width : height;
            int step = horizontal ? 1 : width;
            int[] prefix = new int[length + 1];
            for (int line = 0; line < lines; line++)
            {
                int start = horizontal ? line * width : line;

                // Counts off pixels when eroding (any off pixel kills the window), on pixels when dilating.
                for (int i = 0; i < length; i++)
                {
                    bool on = source[start + (i * step)];
                    prefix[i + 1] = prefix[i] + ((erode ? !on : on) ? 1 : 0);
                }

                for (int i = 0; i < length; i++)
                {
                    int low = i - radius;
                    int high = i + radius;
                    bool value = erode
                        ? low >= 0 && high < length && prefix[high + 1] - prefix[low] == 0
                        : prefix[Math.Min(high, length - 1) + 1] - prefix[Math.Max(low, 0)] > 0;
                    output[start + (i * step)] = value;
                }
            }

            return output;
        }

        /// <summary>
        /// Translates contour vertices by the mask offset and closes the ring.
        /// </summary>
        private static Vector2[] NormalizeRing(IEnumerable<Vector2> ring, int offsetX, int offsetY)
        {
            Vector2[] translated = [.. ring.Select(point =>
                new Vector2(point.X + offsetX, point.Y + offsetY))];
            return [.. translated.RemoveAdjacentDuplicates().EnsureClosedRing()];
        }

        /// <summary>
        /// Douglas-Peucker each ring in the polygon's own coordinates, then rebuilds it.
        /// Auto-polygonize and interactive segmentation pass one screen pixel in world units.
        /// Retries at half tolerance when the
        /// result self-intersects so a dense marching-squares ring is not kept only because the
        /// first pass was invalid.
        /// </summary>
        internal static Polygon SimplifyRings(Polygon polygon, double tolerance)
        {
            if (polygon is null || tolerance <= 0)
                return polygon;

            double current = tolerance;
            double minimum = Math.Max(tolerance * 0.125, 0.25);
            while (current >= minimum)
            {
                if (TryBuildSimplifiedPolygon(polygon, current, out Polygon simplified))
                    return simplified;

                current *= 0.5;
            }

            return polygon;
        }

        private static bool TryBuildSimplifiedPolygon(Polygon polygon, double tolerance, out Polygon simplified)
        {
            simplified = null;
            try
            {
                Vector2[] exterior = SimplifyClosedRing(polygon.ExteriorRing, tolerance);
                if (exterior is null)
                    return false;

                Polygon output = new(exterior);
                foreach (Vector2[] hole in polygon.InteriorRings)
                {
                    Vector2[] simplifiedHole = SimplifyClosedRing(hole, tolerance);
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

                if (output.ExteriorSegments.SelfIntersects(LineSetOrdering.Closed))
                    return false;

                simplified = output;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static Vector2[] SimplifyClosedRing(IReadOnlyList<Vector2> ring, double tolerance)
        {
            if (ring is null || ring.Count < 4)
                return null;

            List<Vector2> reduced = ring.ToList().DouglasPeuckerReduction(tolerance);
            List<Vector2> unique = ((ICollection<Vector2>)reduced).RemoveAdjacentDuplicates();
            Vector2[] closed = [.. unique.EnsureClosedRing()];
            return closed.IsValidClosedRing() ? closed : null;
        }

        /// <summary>
        /// Converts mask-pixel rings to world space. Pixel Y is top-origin; Viking world Y increases upward.
        /// Tests must assert Contains against the flipped Y (e.g. 300,500 not 300,300 for a 600-tall capture).
        /// </summary>
        private static Polygon TransformToWorld(
            Polygon pixelPolygon,
            int imageWidth,
            int imageHeight,
            Rectangle viewportBounds)
        {
            Vector2[] exterior = TransformRing(
                pixelPolygon.ExteriorRing,
                imageWidth,
                imageHeight,
                viewportBounds);
            Polygon output = new(exterior);

            foreach (Vector2[] interior in pixelPolygon.InteriorRings)
            {
                try
                {
                    output.AddInteriorRing(TransformRing(interior, imageWidth, imageHeight, viewportBounds));
                }
                catch (ArgumentException)
                {
                    // A ring that touches the exterior after the Y flip is not a valid hole.
                }
            }

            return output;
        }

        private static Vector2[] ToPixelPoints(
            IReadOnlyList<Vector2> worldPoints,
            int imageWidth,
            int imageHeight,
            Rectangle viewportBounds)
        {
            if (worldPoints is null || worldPoints.Count == 0 || imageWidth <= 0 || imageHeight <= 0)
                return [];

            return [.. worldPoints.Select(point => WorldToPixel(point, imageWidth, imageHeight, viewportBounds))];
        }

        private static bool ContainsAny(Polygon polygon, IReadOnlyList<Vector2> points)
        {
            if (points is null || points.Count == 0)
                return false;

            foreach (Vector2 point in points)
            {
                if (polygon.Covers(point))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Inverse of <see cref="TransformRing"/>: world-up to mask top-origin pixels.
        /// </summary>
        private static Vector2 WorldToPixel(
            Vector2 world,
            int imageWidth,
            int imageHeight,
            Rectangle viewportBounds)
        {
            double normalizedX = (world.X - viewportBounds.Left) / viewportBounds.Width;
            double normalizedY = (world.Y - viewportBounds.Bottom) / viewportBounds.Height;
            return new Vector2(
                normalizedX * imageWidth,
                imageHeight - (normalizedY * imageHeight));
        }

        private static Vector2[] TransformRing(
            IEnumerable<Vector2> ring,
            int imageWidth,
            int imageHeight,
            Rectangle viewportBounds)
        {
            return [.. ring.Select(point =>
            {
                double normalizedX = point.X / imageWidth;
                double normalizedY = (imageHeight - point.Y) / imageHeight;
                return new Vector2(
                    viewportBounds.Left + (normalizedX * viewportBounds.Width),
                    viewportBounds.Bottom + (normalizedY * viewportBounds.Height));
            })];
        }
    }
}
