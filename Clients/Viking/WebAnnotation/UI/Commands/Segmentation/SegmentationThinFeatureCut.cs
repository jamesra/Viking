using System;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Cuts the parts of a SAM2 soft mask that are thinner than a minimum width, without moving the edge of anything
    /// wider: erode by half the width, keep only the pieces that hold a click, dilate by the same amount, put back
    /// everything within one sample of the result, and push the rest below the object boundary.
    /// </summary>
    /// <remarks>
    /// A cell membrane is at least 7 to 10 nm thick, so a mask connection narrower than that cannot be cytoplasm. SAM2
    /// leaves such connections: single samples and one-sample chains that join the cell to a neighbor or to a
    /// speck. Eroding the whole mask removes them but also pulls the line off the membrane. Erode, drop the isolated
    /// pieces, dilate does not: a region wider than twice the radius gets back exactly the boundary it had, apart from
    /// rounding of sharp convex corners, and restoring a one-sample band around the result gives those corners
    /// back. A piece wide enough to survive erosion but no longer joined to the clicked one (a lobe whose only link
    /// was a thin bridge) is dropped along with the bridge. The edits are
    /// made to the soft field, so the contour that is traced afterwards still sits where the logits cross the
    /// threshold. Stateless and safe to call from any thread.
    /// </remarks>
    internal static class SegmentationThinFeatureCut
    {
        /// <summary>
        /// Upper bound on samples in the working grid, so a very large mask cannot allocate without limit.
        /// </summary>
        private const int MaxWorkingPixels = 12_000_000;

        /// <summary>
        /// Returns <paramref name="soft"/> with its sub-minimum-width features pushed below the object boundary, or
        /// <paramref name="soft"/> itself when nothing was cut.
        /// </summary>
        /// <param name="soft">Soft bytes with the object boundary at <see cref="SegmentationMaskPolygonizer.SoftMaskIsoLevel"/>. Not modified.</param>
        /// <param name="width">Samples across.</param>
        /// <param name="height">Samples down.</param>
        /// <param name="minFeatureSamples">
        /// Narrowest feature to keep, in samples. A value at or below 0.5 cuts nothing, since the working grid cannot
        /// resolve it.
        /// </param>
        /// <param name="keepSamples">
        /// Positions, in samples of this grid, of the clicks that say which piece is the object. After erosion the pieces
        /// holding one are kept and every other is removed. With none, or when none lands on a surviving piece, the
        /// largest piece is kept.
        /// </param>
        /// <param name="cutSamples">How many samples were pushed below the boundary.</param>
        /// <remarks>
        /// The mask is never emptied: when no part of it is wide enough to survive, it is returned unchanged.
        /// </remarks>
        public static byte[] Cut(
            byte[] soft,
            int width,
            int height,
            double minFeatureSamples,
            System.Collections.Generic.IReadOnlyList<(double X, double Y)>? keepSamples,
            out int cutSamples)
        {
            cutSamples = 0;
            if (soft is null)
                throw new ArgumentNullException(nameof(soft));
            if (width <= 0 || height <= 0 || soft.Length != width * height || !(minFeatureSamples > 0.5))
                return soft;

            double radiusSamples = minFeatureSamples / 2.0;
            int scale = radiusSamples >= 2 ? 1 : radiusSamples >= 1 ? 2 : 4;
            while (scale > 1 && (long)width * height * scale * scale > MaxWorkingPixels)
                scale /= 2;

            int wide = width * scale;
            int tall = height * scale;
            double radius = radiusSamples * scale;
            bool[] region = Upsample(soft, width, height, scale);

            float[] insideDistance = SquaredDistanceTo(region, wide, tall, seedValue: false);
            bool[] eroded = new bool[region.Length];
            double radiusSquared = radius * radius;
            bool anySurvivor = false;
            for (int i = 0; i < eroded.Length; i++)
            {
                if (region[i] && insideDistance[i] > radiusSquared)
                {
                    eroded[i] = true;
                    anySurvivor = true;
                }
            }

            if (!anySurvivor)
                return soft;

            eroded = KeepClickedPieces(eroded, wide, tall, scale, keepSamples);
            float[] fromEroded = SquaredDistanceTo(eroded, wide, tall, seedValue: true);
            bool[] opened = new bool[region.Length];
            for (int i = 0; i < opened.Length; i++)
                opened[i] = fromEroded[i] <= radiusSquared;

            float[] fromOpened = SquaredDistanceTo(opened, wide, tall, seedValue: true);
            double restoreSquared = (double)scale * scale;
            byte[] result = null;
            int blockArea = scale * scale;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (soft[index] < SegmentationMaskPolygonizer.SoftMaskForeground)
                        continue;

                    int cut = 0;
                    for (int dy = 0; dy < scale; dy++)
                    {
                        int row = (((y * scale) + dy) * wide) + (x * scale);
                        for (int dx = 0; dx < scale; dx++)
                        {
                            if (region[row + dx] && fromOpened[row + dx] > restoreSquared)
                                cut++;
                        }
                    }

                    if (cut * 2 < blockArea)
                        continue;

                    result ??= (byte[])soft.Clone();
                    result[index] = (byte)(255 - soft[index]);
                    cutSamples++;
                }
            }

            return result ?? soft;
        }

        /// <summary>
        /// Keeps only the 8-connected pieces of <paramref name="pieces"/> that contain a click, or the largest piece when
        /// there are no clicks or none lands on a piece.
        /// </summary>
        private static bool[] KeepClickedPieces(
            bool[] pieces,
            int wide,
            int tall,
            int scale,
            System.Collections.Generic.IReadOnlyList<(double X, double Y)>? keepSamples)
        {
            int[] label = new int[pieces.Length];
            System.Collections.Generic.List<int> sizes = [0];
            int[] stack = new int[pieces.Length];
            for (int start = 0; start < pieces.Length; start++)
            {
                if (!pieces[start] || label[start] != 0)
                    continue;

                int id = sizes.Count;
                int size = 0;
                int top = 0;
                stack[top++] = start;
                label[start] = id;
                while (top > 0)
                {
                    int index = stack[--top];
                    size++;
                    int px = index % wide;
                    int py = index / wide;
                    for (int ny = Math.Max(0, py - 1); ny <= Math.Min(tall - 1, py + 1); ny++)
                    {
                        for (int nx = Math.Max(0, px - 1); nx <= Math.Min(wide - 1, px + 1); nx++)
                        {
                            int neighbor = (ny * wide) + nx;
                            if (pieces[neighbor] && label[neighbor] == 0)
                            {
                                label[neighbor] = id;
                                stack[top++] = neighbor;
                            }
                        }
                    }
                }

                sizes.Add(size);
            }

            System.Collections.Generic.HashSet<int> kept = [];
            if (keepSamples is not null)
            {
                foreach ((double x, double y) in keepSamples)
                {
                    int px = (int)Math.Floor(x * scale);
                    int py = (int)Math.Floor(y * scale);
                    if (px >= 0 && py >= 0 && px < wide && py < tall && label[(py * wide) + px] != 0)
                        kept.Add(label[(py * wide) + px]);
                }
            }

            if (kept.Count == 0)
            {
                int largest = 1;
                for (int id = 2; id < sizes.Count; id++)
                {
                    if (sizes[id] > sizes[largest])
                        largest = id;
                }

                kept.Add(largest);
            }

            bool[] result = new bool[pieces.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = label[i] != 0 && kept.Contains(label[i]);

            return result;
        }

        /// <summary>
        /// The soft field bilinearly resampled <paramref name="scale"/> times finer and thresholded at the object boundary.
        /// Sample centers line up with the native grid, so a native sample sits at the middle of its block.
        /// </summary>
        private static bool[] Upsample(byte[] soft, int width, int height, int scale)
        {
            int wide = width * scale;
            int tall = height * scale;
            bool[] region = new bool[wide * tall];
            for (int y = 0; y < tall; y++)
            {
                double sourceY = ((y + 0.5) / scale) - 0.5;
                int y0 = (int)Math.Floor(sourceY);
                double fy = sourceY - y0;
                int ya = Math.Max(0, Math.Min(height - 1, y0));
                int yb = Math.Max(0, Math.Min(height - 1, y0 + 1));
                for (int x = 0; x < wide; x++)
                {
                    double sourceX = ((x + 0.5) / scale) - 0.5;
                    int x0 = (int)Math.Floor(sourceX);
                    double fx = sourceX - x0;
                    int xa = Math.Max(0, Math.Min(width - 1, x0));
                    int xb = Math.Max(0, Math.Min(width - 1, x0 + 1));
                    double top = (soft[(ya * width) + xa] * (1 - fx)) + (soft[(ya * width) + xb] * fx);
                    double bottom = (soft[(yb * width) + xa] * (1 - fx)) + (soft[(yb * width) + xb] * fx);
                    region[(y * wide) + x] = (top * (1 - fy)) + (bottom * fy) >= SegmentationMaskPolygonizer.SoftMaskForeground;
                }
            }

            return region;
        }

        /// <summary>
        /// For every pixel, the squared distance in pixels to the nearest pixel whose value equals
        /// <paramref name="seedValue"/> (exact Euclidean distance transform, Felzenszwalb and Huttenlocher). A grid with no
        /// seed gets a very large distance everywhere.
        /// </summary>
        internal static float[] SquaredDistanceTo(bool[] grid, int width, int height, bool seedValue)
        {
            const float Far = 1e9f;
            float[] distance = new float[grid.Length];
            for (int i = 0; i < distance.Length; i++)
                distance[i] = grid[i] == seedValue ? 0f : Far;

            int longest = Math.Max(width, height);
            float[] line = new float[longest];
            float[] output = new float[longest];
            int[] parabola = new int[longest];
            float[] boundary = new float[longest + 1];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    line[y] = distance[(y * width) + x];

                Transform1D(line, output, height, parabola, boundary);
                for (int y = 0; y < height; y++)
                    distance[(y * width) + x] = output[y];
            }

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                    line[x] = distance[row + x];

                Transform1D(line, output, width, parabola, boundary);
                for (int x = 0; x < width; x++)
                    distance[row + x] = output[x];
            }

            return distance;
        }

        /// <summary>Where the parabola rooted at <paramref name="q"/> crosses the one rooted at <paramref name="p"/>.</summary>
        private static float Intersection(float[] input, int q, int p) =>
            ((input[q] + ((float)q * q)) - (input[p] + ((float)p * p))) / (2f * (q - p));

        /// <summary>
        /// One-dimensional lower envelope of parabolas: <c>output[q] = min over p of (q - p)^2 + input[p]</c>.
        /// </summary>
        private static void Transform1D(float[] input, float[] output, int count, int[] parabola, float[] boundary)
        {
            int k = 0;
            parabola[0] = 0;
            boundary[0] = float.NegativeInfinity;
            boundary[1] = float.PositiveInfinity;
            for (int q = 1; q < count; q++)
            {
                // boundary[0] is -infinity, so k never drops below 0.
                float intersection = Intersection(input, q, parabola[k]);
                while (intersection <= boundary[k])
                {
                    k--;
                    intersection = Intersection(input, q, parabola[k]);
                }

                k++;
                parabola[k] = q;
                boundary[k] = intersection;
                boundary[k + 1] = float.PositiveInfinity;
            }

            k = 0;
            for (int q = 0; q < count; q++)
            {
                while (boundary[k + 1] < q)
                    k++;

                int p = parabola[k];
                output[q] = ((float)(q - p) * (q - p)) + input[p];
            }
        }
    }
}
