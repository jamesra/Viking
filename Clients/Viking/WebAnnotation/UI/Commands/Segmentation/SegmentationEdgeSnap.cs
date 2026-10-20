using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Tuning of <see cref="SegmentationEdgeSnap"/>. Distances are in image pixels (world units divided by downsample).
    /// </summary>
    internal sealed record EdgeSnapOptions
    {
        /// <summary>How far a vertex may move away from the object (outward).</summary>
        public double OutwardPixels { get; init; } = 12;

        /// <summary>How far a vertex may move into the object (inward).</summary>
        public double InwardPixels { get; init; } = 3;

        /// <summary>Spacing of the offsets tried along each normal.</summary>
        public double StepPixels { get; init; } = 1;

        /// <summary>Spacing of the vertices the ring is resampled to before snapping.</summary>
        public double SpacingPixels { get; init; } = 4;

        /// <summary>Half-width of the strip along the contour that each brightness profile is averaged over.</summary>
        public double AcrossPixels { get; init; } = 4;

        /// <summary>
        /// A salient edge for this image, in brightness levels per pixel: its own 90th-percentile gradient
        /// (<see cref="TileImageSet.EdgeScale"/>). Edge strength is judged against it, not against the ring, so a ring
        /// with no real edge is not stretched onto grain. Zero or less disables the snap, because without a reference
        /// there is no way to tell an edge from texture.
        /// </summary>
        public double ReferenceEdgeLevelsPerPixel { get; init; }

        /// <summary>
        /// Fraction of <see cref="ReferenceEdgeLevelsPerPixel"/> below which a brightness drop earns no reward. EM is
        /// textured, so a weak drop is as likely grain as membrane. The reward then rises to one at the reference.
        /// </summary>
        public double EdgeFloorFraction { get; init; } = 0.4;

        /// <summary>Reward for a salient edge: <c>(drop - floor) / (reference - floor)</c>, capped at 1.5, times this.</summary>
        public double EdgeWeight { get; init; } = 1.0;

        /// <summary>
        /// Cost of moving away from the logit contour: <c>PriorWeight * (offset / PriorScalePixels)^2</c>. It is
        /// what keeps a vertex where it was when the image has no clear edge there.
        /// </summary>
        public double PriorWeight { get; init; } = 0.6;

        /// <summary>Offset at which the prior costs exactly <see cref="PriorWeight"/>.</summary>
        public double PriorScalePixels { get; init; } = 8;

        /// <summary>Cost of neighbouring vertices choosing offsets that differ: <c>SmoothWeight * difference^2</c> (pixels).</summary>
        public double SmoothWeight { get; init; } = 0.12;

        /// <summary>Most steps two neighbouring vertices' offsets may differ by.</summary>
        public int MaxStepChange { get; init; } = 3;

        /// <summary>Rings with fewer resampled vertices than this are left alone.</summary>
        public int MinVertices { get; init; } = 8;
    }

    /// <summary>What a refinement did, for the diagnostics log and for tests.</summary>
    /// <param name="Rings">Rings considered (the exterior and each hole).</param>
    /// <param name="Snapped">Rings that were moved.</param>
    /// <param name="MeanOffsetPixels">Mean signed offset over snapped vertices, positive away from the object.</param>
    /// <param name="MeanAbsoluteOffsetPixels">Mean absolute offset over snapped vertices.</param>
    /// <param name="ReducedRange">True when the first attempt made an invalid polygon and a smaller range was used.</param>
    internal readonly record struct EdgeSnapStats(
        int Rings,
        int Snapped,
        double MeanOffsetPixels,
        double MeanAbsoluteOffsetPixels,
        bool ReducedRange);

    /// <summary>
    /// Moves a traced contour onto the nearest strong bright-to-dark edge of the image.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SAM2's logits fall off slowly across a membrane (about 0.05 logit per pixel), so the contour at any one
    /// threshold sits several pixels from the membrane, and by a different amount in different places. The image
    /// itself is sharper: going outward from a cell's cytoplasm the brightness drops where the membrane begins.
    /// </para>
    /// <para>
    /// The snap resamples each ring, looks along the outward normal at every vertex, and picks one offset per vertex
    /// by dynamic programming around the ring. A vertex is rewarded for a brightness drop at its offset that is
    /// salient for this image (<see cref="EdgeSnapOptions.ReferenceEdgeLevelsPerPixel"/>, nothing below the floor) and
    /// charged for moving from the logit contour (<see cref="EdgeSnapOptions.PriorWeight"/>) and for differing from its
    /// neighbours (<see cref="EdgeSnapOptions.SmoothWeight"/>). Where the image has no salient edge the prior wins and
    /// the vertex stays put, so the logits remain the answer wherever the image is silent. EM is textured: on real
    /// tiles a strong edge is only about three times the median gradient, which is why salience is judged against the
    /// whole tile.
    /// </para>
    /// <para>
    /// Only a drop going outward counts as an edge, so the contour stops at the inner side of a membrane, not the
    /// outer side, and does not follow a dark organelle inside the cell.
    /// </para>
    /// </remarks>
    internal static class SegmentationEdgeSnap
    {
        /// <summary>
        /// The polygon with its exterior and hole rings moved onto image edges, or <paramref name="polygon"/> itself when
        /// nothing could be improved or the result would be invalid.
        /// </summary>
        /// <param name="polygon">The traced polygon in world units. Not modified.</param>
        /// <param name="brightness">Brightness at a world position (0 to 255), or NaN where the image is not known.</param>
        /// <param name="worldPerPixel">World units per image pixel (the downsample).</param>
        /// <param name="options">Tuning; null uses the defaults.</param>
        public static Polygon Refine(
            Polygon polygon,
            Func<Vector2, double> brightness,
            double worldPerPixel,
            EdgeSnapOptions? options = null) =>
            Refine(polygon, brightness, worldPerPixel, options, out _);

        /// <inheritdoc cref="Refine(Polygon, Func{Vector2, double}, double, EdgeSnapOptions?)"/>
        /// <param name="stats">What was done.</param>
        public static Polygon Refine(
            Polygon polygon,
            Func<Vector2, double> brightness,
            double worldPerPixel,
            EdgeSnapOptions? options,
            out EdgeSnapStats stats)
        {
            if (polygon is null)
                throw new ArgumentNullException(nameof(polygon));
            if (brightness is null)
                throw new ArgumentNullException(nameof(brightness));
            EdgeSnapOptions settings = options ?? new EdgeSnapOptions();
            stats = new EdgeSnapStats(1 + polygon.InteriorRings.Count, 0, 0, 0, false);
            if (!(worldPerPixel > 0) || !(settings.ReferenceEdgeLevelsPerPixel > 0))
                return polygon;

            double reduction = 1.0;
            for (int attempt = 0; attempt < 3; attempt++, reduction *= 0.5)
            {
                EdgeSnapOptions tried = attempt == 0
                    ? settings
                    : settings with
                    {
                        OutwardPixels = settings.OutwardPixels * reduction,
                        InwardPixels = settings.InwardPixels * reduction
                    };

                Polygon? refined = TryRefine(polygon, brightness, worldPerPixel, tried, out EdgeSnapStats attemptStats);
                if (refined is not null)
                {
                    stats = attemptStats with { ReducedRange = attempt > 0 };
                    return refined;
                }
            }

            return polygon;
        }

        private static Polygon? TryRefine(
            Polygon polygon,
            Func<Vector2, double> brightness,
            double worldPerPixel,
            EdgeSnapOptions options,
            out EdgeSnapStats stats)
        {
            double sumOffset = 0;
            double sumAbsolute = 0;
            int vertices = 0;
            int snapped = 0;
            int rings = 1 + polygon.InteriorRings.Count;

            Vector2[]? exterior = SnapRing(polygon.ExteriorRing, isHole: false, brightness, worldPerPixel, options, out RingSnap exteriorSnap);
            if (exterior is null)
            {
                stats = new EdgeSnapStats(rings, 0, 0, 0, false);
                return null;
            }

            Polygon result;
            try
            {
                result = new Polygon(exterior);
            }
            catch (ArgumentException)
            {
                stats = new EdgeSnapStats(rings, 0, 0, 0, false);
                return null;
            }

            snapped++;
            sumOffset += exteriorSnap.SumOffset;
            sumAbsolute += exteriorSnap.SumAbsolute;
            vertices += exteriorSnap.Count;

            foreach (Vector2[] hole in polygon.InteriorRings)
            {
                Vector2[]? snappedHole = SnapRing(hole, isHole: true, brightness, worldPerPixel, options, out RingSnap holeSnap);
                if (snappedHole is not null && TryAddHole(result, snappedHole))
                {
                    snapped++;
                    sumOffset += holeSnap.SumOffset;
                    sumAbsolute += holeSnap.SumAbsolute;
                    vertices += holeSnap.Count;
                }
                else
                {
                    TryAddHole(result, hole);
                }
            }

            stats = new EdgeSnapStats(
                rings,
                snapped,
                vertices == 0 ? 0 : sumOffset / vertices,
                vertices == 0 ? 0 : sumAbsolute / vertices,
                false);
            return result;
        }

        private static bool TryAddHole(Polygon polygon, Vector2[] ring)
        {
            try
            {
                polygon.AddInteriorRing(ring);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private readonly record struct RingSnap(double SumOffset, double SumAbsolute, int Count);

        /// <summary>
        /// One ring moved onto image edges, closed (first point repeated last), or null when the ring is too small,
        /// has no usable edge, or the result would be degenerate.
        /// </summary>
        private static Vector2[]? SnapRing(
            IReadOnlyList<Vector2> closedRing,
            bool isHole,
            Func<Vector2, double> brightness,
            double worldPerPixel,
            EdgeSnapOptions options,
            out RingSnap snap)
        {
            snap = default;
            Vector2[] points = Resample(closedRing, options.SpacingPixels * worldPerPixel);
            int count = points.Length;
            if (count < options.MinVertices)
                return null;

            Vector2[] outward = OutwardNormals(points, isHole, out Vector2[] tangents);
            int levels = Math.Max(2, (int)Math.Round((options.InwardPixels + options.OutwardPixels) / options.StepPixels) + 1);
            double[] offsets = new double[levels];
            for (int k = 0; k < levels; k++)
                offsets[k] = -options.InwardPixels + (k * options.StepPixels);

            double[,] edge = EdgeStrength(points, outward, tangents, offsets, brightness, worldPerPixel, options);
            double floor = options.EdgeFloorFraction * options.ReferenceEdgeLevelsPerPixel;
            double span = Math.Max(1e-9, options.ReferenceEdgeLevelsPerPixel - floor);
            double[,] cost = new double[count, levels];
            bool anySalientEdge = false;
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < levels; k++)
                {
                    double reward = Math.Min(1.5, Math.Max(0.0, (edge[i, k] - floor) / span));
                    anySalientEdge |= reward > 0;
                    double prior = offsets[k] / options.PriorScalePixels;
                    cost[i, k] = (-options.EdgeWeight * reward) + (options.PriorWeight * prior * prior);
                }
            }

            if (!anySalientEdge)
                return null;

            int[] choice = ClosedViterbi(cost, offsets, options);
            double[] chosen = new double[count];
            for (int i = 0; i < count; i++)
                chosen[i] = offsets[choice[i]];

            chosen = SmoothCircular(chosen);
            if (chosen.All(offset => Math.Abs(offset) < 0.5 * options.StepPixels))
                return null;

            Vector2[] moved = new Vector2[count + 1];
            double sumOffset = 0;
            double sumAbsolute = 0;
            for (int i = 0; i < count; i++)
            {
                moved[i] = points[i] + (outward[i] * (chosen[i] * worldPerPixel));
                sumOffset += chosen[i];
                sumAbsolute += Math.Abs(chosen[i]);
            }

            moved[count] = moved[0];
            Vector2[] cleaned = [.. ((ICollection<Vector2>)moved).RemoveAdjacentDuplicates().EnsureClosedRing()];
            if (cleaned.Length < 4)
                return null;

            snap = new RingSnap(sumOffset, sumAbsolute, count);
            return cleaned;
        }

        /// <summary>
        /// The ring as <c>n</c> points at equal arc length around it (the closing point is not repeated).
        /// </summary>
        internal static Vector2[] Resample(IReadOnlyList<Vector2> closedRing, double spacing)
        {
            int n = closedRing.Count;
            if (n < 4 || !(spacing > 0))
                return [];

            // Cumulative length at every vertex, the last being the closing vertex.
            double[] length = new double[n];
            for (int i = 1; i < n; i++)
                length[i] = length[i - 1] + Vector2.Distance(closedRing[i - 1], closedRing[i]);

            double perimeter = length[n - 1];
            int count = (int)Math.Round(perimeter / spacing);
            if (count < 3 || !(perimeter > 0))
                return [];

            Vector2[] output = new Vector2[count];
            int segment = 1;
            for (int i = 0; i < count; i++)
            {
                double at = perimeter * i / count;
                while (segment < n - 1 && length[segment] < at)
                    segment++;

                double span = length[segment] - length[segment - 1];
                double t = span > 0 ? (at - length[segment - 1]) / span : 0;
                output[i] = closedRing[segment - 1] + ((closedRing[segment] - closedRing[segment - 1]) * t);
            }

            return output;
        }

        /// <summary>
        /// Unit normals pointing away from the object at each point of a ring, and the unit tangents.
        /// </summary>
        /// <remarks>
        /// The tangent is the chord over two points either side, which averages out the stair-step of a traced
        /// contour. For a counter-clockwise ring the outward side is to the right of travel. A hole is the opposite:
        /// the object lies outside it, so away from the object is into the hole.
        /// </remarks>
        internal static Vector2[] OutwardNormals(Vector2[] points, bool isHole, out Vector2[] tangents)
        {
            int n = points.Length;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % n];
                area += (a.X * b.Y) - (b.X * a.Y);
            }

            bool counterClockwise = area > 0;
            bool rightIsOutward = counterClockwise != isHole;
            Vector2[] normals = new Vector2[n];
            tangents = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 chord = points[(i + 2) % n] - points[(i - 2 + (2 * n)) % n];
                double norm = Math.Sqrt((chord.X * chord.X) + (chord.Y * chord.Y));
                Vector2 tangent = norm > 0 ? chord / norm : new Vector2(1, 0);
                tangents[i] = tangent;
                normals[i] = rightIsOutward ? new Vector2(tangent.Y, -tangent.X) : new Vector2(-tangent.Y, tangent.X);
            }

            return normals;
        }

        /// <summary>
        /// For each vertex and offset, how strongly the brightness falls going outward, in levels per pixel.
        /// </summary>
        /// <remarks>
        /// The brightness at an offset is the mean over a strip along the contour. The profile along the normal is
        /// smoothed with 1-2-1 and differentiated, so a positive value is a drop going outward. A profile with an
        /// unknown sample has no edge at the offsets that need it.
        /// </remarks>
        private static double[,] EdgeStrength(
            Vector2[] points,
            Vector2[] outward,
            Vector2[] tangents,
            double[] offsets,
            Func<Vector2, double> brightness,
            double worldPerPixel,
            EdgeSnapOptions options)
        {
            int count = points.Length;
            int levels = offsets.Length;
            double[,] profile = new double[count, levels];
            double[] across = options.AcrossPixels > 0
                ? [-options.AcrossPixels, 0, options.AcrossPixels]
                : [0];
            for (int i = 0; i < count; i++)
            {
                for (int k = 0; k < levels; k++)
                {
                    Vector2 centre = points[i] + (outward[i] * (offsets[k] * worldPerPixel));
                    double sum = 0;
                    bool known = true;
                    foreach (double t in across)
                    {
                        double value = brightness(centre + (tangents[i] * (t * worldPerPixel)));
                        if (double.IsNaN(value))
                        {
                            known = false;
                            break;
                        }

                        sum += value;
                    }

                    profile[i, k] = known ? sum / across.Length : double.NaN;
                }
            }

            double[,] edge = new double[count, levels];
            for (int i = 0; i < count; i++)
            {
                double[] smooth = new double[levels];
                for (int k = 0; k < levels; k++)
                {
                    double here = profile[i, k];
                    double before = profile[i, Math.Max(0, k - 1)];
                    double after = profile[i, Math.Min(levels - 1, k + 1)];
                    smooth[k] = double.IsNaN(here) || double.IsNaN(before) || double.IsNaN(after)
                        ? double.NaN
                        : ((before + (2 * here) + after) / 4);
                }

                for (int k = 0; k < levels; k++)
                {
                    double before = smooth[Math.Max(0, k - 1)];
                    double after = smooth[Math.Min(levels - 1, k + 1)];
                    double steps = Math.Min(levels - 1, k + 1) - Math.Max(0, k - 1);
                    double drop = double.IsNaN(before) || double.IsNaN(after) || steps <= 0
                        ? 0
                        : -(after - before) / (steps * options.StepPixels);
                    edge[i, k] = drop;
                }
            }

            return edge;
        }

        /// <summary>
        /// The cheapest choice of one level per vertex around a closed ring, where moving between neighbours costs the
        /// squared offset difference and may change at most <see cref="EdgeSnapOptions.MaxStepChange"/> levels.
        /// </summary>
        /// <remarks>
        /// A ring has no start, so the sequence is run twice around and the second lap is read: by then the path has
        /// settled into the best cycle, whatever vertex the lap began at. O(vertices x levels x steps).
        /// </remarks>
        internal static int[] ClosedViterbi(double[,] cost, double[] offsets, EdgeSnapOptions options)
        {
            int count = cost.GetLength(0);
            int levels = cost.GetLength(1);
            int length = 2 * count;
            double[,] total = new double[length, levels];
            int[,] from = new int[length, levels];
            for (int k = 0; k < levels; k++)
                total[0, k] = cost[0, k];

            for (int step = 1; step < length; step++)
            {
                int i = step % count;
                for (int k = 0; k < levels; k++)
                {
                    double best = double.PositiveInfinity;
                    int bestFrom = k;
                    int low = Math.Max(0, k - options.MaxStepChange);
                    int high = Math.Min(levels - 1, k + options.MaxStepChange);
                    for (int j = low; j <= high; j++)
                    {
                        double difference = offsets[k] - offsets[j];
                        double candidate = total[step - 1, j] + (options.SmoothWeight * difference * difference);
                        if (candidate < best)
                        {
                            best = candidate;
                            bestFrom = j;
                        }
                    }

                    total[step, k] = best + cost[i, k];
                    from[step, k] = bestFrom;
                }
            }

            int level = 0;
            for (int k = 1; k < levels; k++)
            {
                if (total[length - 1, k] < total[length - 1, level])
                    level = k;
            }

            int[] path = new int[length];
            for (int step = length - 1; step >= 0; step--)
            {
                path[step] = level;
                level = from[step, level];
            }

            int[] choice = new int[count];
            for (int i = 0; i < count; i++)
                choice[i] = path[count + i];

            return choice;
        }

        /// <summary>One pass of 1-2-1 smoothing around a closed ring of values.</summary>
        internal static double[] SmoothCircular(double[] values)
        {
            int n = values.Length;
            double[] output = new double[n];
            for (int i = 0; i < n; i++)
                output[i] = (values[(i - 1 + n) % n] + (2 * values[i]) + values[(i + 1) % n]) / 4;

            return output;
        }
    }
}
