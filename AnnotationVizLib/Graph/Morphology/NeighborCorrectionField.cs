using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    /// <summary>
    /// Spatially varying residual registration field from leave-one-out process curve residuals
    /// (predicted − actual). Built once from an in-memory neighbor slab, then sampled at contour centroids.
    /// Replaces the former section-to-section hop integrator: residuals already live at Z, so there is no
    /// cumulative T or pin section.
    /// </summary>
    public sealed class NeighborResidualField
    {
        public const double GridSizeNm = 2000.0;
        public const double KernelRadiusNm = 8000.0;
        public const int MinAnnotationVotes = 3;
        public const double MaxResidualMagnitudeNm = 5000.0;

        readonly Dictionary<(int Z, int Gx, int Gy), Vector2> _offset;
        readonly Dictionary<(int Z, int Gx, int Gy), int> _annotationVotes;
        readonly double _pitchNm;
        readonly double _kernelRadiusNm;

        NeighborResidualField(
            Dictionary<(int Z, int Gx, int Gy), Vector2> offset,
            Dictionary<(int Z, int Gx, int Gy), int> annotationVotes,
            double pitchNm = GridSizeNm,
            double kernelRadiusNm = KernelRadiusNm)
        {
            _offset = offset;
            _annotationVotes = annotationVotes;
            _pitchNm = pitchNm > 0 ? pitchNm : GridSizeNm;
            _kernelRadiusNm = kernelRadiusNm > 0 ? kernelRadiusNm : KernelRadiusNm;
        }

        public int CellCount => _offset.Count;

        public double PitchNm => _pitchNm;

        public double PublishedKernelRadiusNm => _kernelRadiusNm;

        public IEnumerable<(int Z, int Gx, int Gy, Vector2 Offset, int Votes)> EnumerateNodes()
        {
            foreach (KeyValuePair<(int Z, int Gx, int Gy), Vector2> kv in _offset)
            {
                int n = _annotationVotes.TryGetValue(kv.Key, out int votes) ? votes : 0;
                yield return (kv.Key.Z, kv.Key.Gx, kv.Key.Gy, kv.Value, n);
            }
        }

        /// <summary>
        /// Rebuild a field from published lattice nodes. Sampling uses <paramref name="pitchNm"/> and
        /// <paramref name="kernelRadiusNm"/>; pooling constants are not applied again.
        /// </summary>
        public static NeighborResidualField FromLattice(
            IEnumerable<(int Z, int Gx, int Gy, Vector2 Offset, int Votes)> nodes,
            double pitchNm = GridSizeNm,
            double kernelRadiusNm = KernelRadiusNm)
        {
            Dictionary<(int Z, int Gx, int Gy), Vector2> offset = [];
            Dictionary<(int Z, int Gx, int Gy), int> votes = [];
            if (nodes != null)
            {
                foreach ((int Z, int Gx, int Gy, Vector2 Offset, int Votes) node in nodes)
                {
                    if (node.Votes < MinAnnotationVotes)
                        continue;
                    var key = (node.Z, node.Gx, node.Gy);
                    offset[key] = node.Offset;
                    votes[key] = node.Votes;
                }
            }

            return new NeighborResidualField(offset, votes, pitchNm, kernelRadiusNm);
        }

        /// <summary>
        /// Leave-one-out curve residual at a movable process location: predicted − actual.
        /// </summary>
        public readonly struct ResidualSample(
            ulong structureId,
            ulong locationId,
            int sectionZ,
            double x,
            double y,
            double dx,
            double dy,
            bool oneSided)
        {
            public readonly ulong StructureID = structureId;
            public readonly ulong LocationID = locationId;
            public readonly int SectionZ = sectionZ;
            public readonly double X = x;
            public readonly double Y = y;
            public readonly double DX = dx;
            public readonly double DY = dy;
            public readonly bool OneSided = oneSided;
        }

        /// <summary>
        /// Collect admitted curve residuals from unbranched process chains on each cell.
        /// </summary>
        public static List<ResidualSample> CollectResiduals(
            IEnumerable<MorphologyGraph> cells,
            MorphologyGraph.ResidualWindowOptions window = default)
        {
            if (window.MinBoth < 1 && window.MaxBoth < 1 && window.MinOne < 1 && window.MaxOne < 1)
                window = MorphologyGraph.ResidualWindowOptions.Default;

            List<ResidualSample> residuals = [];
            foreach (MorphologyGraph cell in cells)
            {
                if (cell is null || cell.StructureID == 0)
                    continue;

                foreach (ulong[] process in cell.Processes())
                {
                    if (process.Length < 3)
                        continue;

                    MorphologyNode[] nodes = [.. process.Select(id => cell.Nodes[id])];
                    Vector2[] centroids = [.. nodes.Select(n => n.Center.XY())];
                    double[] z = [.. nodes.Select(n => n.Z)];

                    for (int i = 0; i < nodes.Length; i++)
                    {
                        MorphologyNode node = nodes[i];
                        if (!MorphologyGraph.IsCurveFitMovable(node, cell))
                            continue;

                        Vector2? predicted = MorphologyGraph.TryEvaluateAsymmetricLeaveOneOut(
                            centroids, z, i, window);
                        if (predicted is null)
                            continue;

                        Vector2 actual = centroids[i];
                        Vector2 residual = predicted.Value - actual;
                        if (residual.Magnitude > MaxResidualMagnitudeNm)
                            continue;

                        int below = i;
                        int above = nodes.Length - 1 - i;
                        bool oneSided = below == 0 || above == 0;

                        residuals.Add(new ResidualSample(
                            cell.StructureID,
                            node.Key,
                            (int)Math.Round(node.UnscaledZ),
                            actual.X,
                            actual.Y,
                            residual.X,
                            residual.Y,
                            oneSided));
                    }
                }
            }

            return residuals;
        }

        /// <summary>
        /// Pool residuals into a sparse 2 µm grid. Every admitted annotation inside the kernel votes; robust
        /// distance-weighted median. Re-pool with <paramref name="excludeStructureId"/> for leave-one-out
        /// without recomputing curves.
        /// </summary>
        public static NeighborResidualField Build(
            IEnumerable<ResidualSample> residuals,
            ulong? excludeStructureId = null)
        {
            List<ResidualSample> filtered = [];
            ResidualSample[] source = residuals as ResidualSample[] ?? [.. residuals];
            for (int i = 0; i < source.Length; i++)
            {
                ref readonly ResidualSample r = ref source[i];
                if (excludeStructureId is not null && r.StructureID == excludeStructureId.Value)
                    continue;
                filtered.Add(r);
            }

            if (filtered.Count == 0)
                return new NeighborResidualField([], []);

            Dictionary<(int Z, int Gx, int Gy), Vector2> offset = [];
            Dictionary<(int Z, int Gx, int Gy), int> votes = [];

            foreach (IGrouping<int, ResidualSample> sectionGroup in filtered.GroupBy(r => r.SectionZ))
            {
                int z = sectionGroup.Key;
                ResidualSample[] sectionResiduals = [.. sectionGroup];

                double minX = sectionResiduals.Min(r => r.X) - KernelRadiusNm;
                double maxX = sectionResiduals.Max(r => r.X) + KernelRadiusNm;
                double minY = sectionResiduals.Min(r => r.Y) - KernelRadiusNm;
                double maxY = sectionResiduals.Max(r => r.Y) + KernelRadiusNm;

                int gx0 = (int)Math.Floor(minX / GridSizeNm);
                int gx1 = (int)Math.Floor(maxX / GridSizeNm);
                int gy0 = (int)Math.Floor(minY / GridSizeNm);
                int gy1 = (int)Math.Floor(maxY / GridSizeNm);

                for (int gx = gx0; gx <= gx1; gx++)
                {
                    for (int gy = gy0; gy <= gy1; gy++)
                    {
                        double cx = (gx + 0.5) * GridSizeNm;
                        double cy = (gy + 0.5) * GridSizeNm;
                        if (!TryPoolSquare(sectionResiduals, cx, cy, out Vector2 pooled, out int annotationCount))
                            continue;

                        offset[(z, gx, gy)] = pooled;
                        votes[(z, gx, gy)] = annotationCount;
                    }
                }
            }

            return new NeighborResidualField(offset, votes);
        }

        /// <summary>
        /// Consensus correction offset (predicted − actual) at (xy, section).
        /// Null when the query is outside compact support. Apply the returned vector directly.
        /// </summary>
        public Vector2? Sample(Vector2 xy, int sectionZ)
        {
            (Vector2 offset, bool trusted) = SampleAlways(xy, sectionZ);
            return trusted ? offset : null;
        }

        /// <summary>
        /// Always returns an offset. <c>trusted</c> is true inside compact support (occupied 2×2 cell or IDW within the kernel).
        /// Outside support the offset is identity (0, 0).
        /// </summary>
        public (Vector2 Offset, bool Trusted) SampleAlways(Vector2 xy, int sectionZ)
        {
            double fx = xy.X / _pitchNm;
            double fy = xy.Y / _pitchNm;
            int gx0 = (int)Math.Floor(fx);
            int gy0 = (int)Math.Floor(fy);
            double tx = fx - gx0;
            double ty = fy - gy0;

            bool TryGet(int gx, int gy, out Vector2 t)
            {
                var key = (sectionZ, gx, gy);
                if (_offset.TryGetValue(key, out t) &&
                    _annotationVotes.TryGetValue(key, out int n) &&
                    n >= MinAnnotationVotes)
                    return true;
                t = Vector2.Zero;
                return false;
            }

            if (TryGet(gx0, gy0, out Vector2 t00) &&
                TryGet(gx0 + 1, gy0, out Vector2 t10) &&
                TryGet(gx0, gy0 + 1, out Vector2 t01) &&
                TryGet(gx0 + 1, gy0 + 1, out Vector2 t11))
            {
                Vector2 a = t00 * (1 - tx) + t10 * tx;
                Vector2 b = t01 * (1 - tx) + t11 * tx;
                return (a * (1 - ty) + b * ty, true);
            }

            int reach = (int)Math.Ceiling(_kernelRadiusNm / _pitchNm) + 1;
            double weightSum = 0;
            Vector2 weighted = Vector2.Zero;
            int annotations = 0;
            const double eps2 = 1.0;
            double r2 = _kernelRadiusNm * _kernelRadiusNm;

            for (int dx = -reach; dx <= reach; dx++)
            {
                for (int dy = -reach; dy <= reach; dy++)
                {
                    int gx = gx0 + dx;
                    int gy = gy0 + dy;
                    if (!TryGet(gx, gy, out Vector2 t))
                        continue;

                    double cx = (gx + 0.5) * _pitchNm;
                    double cy = (gy + 0.5) * _pitchNm;
                    double d2 = (xy.X - cx) * (xy.X - cx) + (xy.Y - cy) * (xy.Y - cy);
                    if (d2 > r2)
                        continue;

                    double w = 1.0 / (d2 + eps2);
                    weighted += t * w;
                    weightSum += w;
                    if (_annotationVotes.TryGetValue((sectionZ, gx, gy), out int n))
                        annotations = Math.Max(annotations, n);
                }
            }

            if (weightSum <= Tolerance.Epsilon || annotations < MinAnnotationVotes)
                return (Vector2.Zero, false);

            return (weighted * (1.0 / weightSum), true);
        }

        /// <summary>
        /// One line of consensus stats per occupied section for console A/B.
        /// </summary>
        public IEnumerable<string> DescribeSections()
        {
            foreach (IGrouping<int, KeyValuePair<(int Z, int Gx, int Gy), Vector2>> group in
                     _offset.GroupBy(kv => kv.Key.Z).OrderBy(g => g.Key))
            {
                int z = group.Key;
                int cells = group.Count();
                double[] mags = [.. group.Select(kv => kv.Value.Magnitude)];
                Array.Sort(mags);
                double medianMag = mags[mags.Length / 2];
                int annotationVotes = group.Sum(kv => _annotationVotes.TryGetValue(kv.Key, out int n) ? n : 0);
                yield return $"Z={z} gridSquares={cells} annotationVotes={annotationVotes} median|r|={medianMag:F1}nm";
            }
        }

        /// <summary>
        /// Distance-weighted robust pool at a grid-square center. Every admitted annotation is one vote.
        /// </summary>
        static bool TryPoolSquare(
            ResidualSample[] sectionResiduals,
            double cx,
            double cy,
            out Vector2 pooled,
            out int annotationCount)
        {
            pooled = Vector2.Zero;
            annotationCount = 0;
            double r2 = KernelRadiusNm * KernelRadiusNm;

            List<(Vector2 Vote, double Weight)> votes = [];

            for (int i = 0; i < sectionResiduals.Length; i++)
            {
                ref readonly ResidualSample sample = ref sectionResiduals[i];
                double dx = sample.X - cx;
                double dy = sample.Y - cy;
                double d2 = dx * dx + dy * dy;
                if (d2 > r2)
                    continue;

                // Gaussian weight; one-sided extrapolations get half weight.
                double sigma = KernelRadiusNm * 0.5;
                double w = Math.Exp(-d2 / (2.0 * sigma * sigma));
                if (sample.OneSided)
                    w *= 0.5;
                if (w <= Tolerance.Epsilon)
                    continue;

                votes.Add((new Vector2(sample.DX, sample.DY), w));
            }

            if (votes.Count < MinAnnotationVotes)
                return false;

            pooled = WeightedMedianVector(votes);
            annotationCount = votes.Count;
            return true;
        }

        /// <summary>
        /// Component-wise weighted median of annotation votes (robust to a bent process outlier).
        /// </summary>
        static Vector2 WeightedMedianVector(List<(Vector2 Vote, double Weight)> votes)
        {
            double[] xs = [.. votes.Select(v => v.Vote.X)];
            double[] ys = [.. votes.Select(v => v.Vote.Y)];
            double[] ws = [.. votes.Select(v => v.Weight)];
            return new Vector2(WeightedMedian1D(xs, ws), WeightedMedian1D(ys, ws));
        }

        static double WeightedMedian1D(double[] values, double[] weights)
        {
            int n = values.Length;
            int[] order = [.. Enumerable.Range(0, n).OrderBy(i => values[i])];
            double total = weights.Sum();
            double half = total * 0.5;
            double running = 0;
            for (int k = 0; k < n; k++)
            {
                int i = order[k];
                running += weights[i];
                if (running >= half)
                    return values[i];
            }

            return values[order[order.Length - 1]];
        }
    }

    /// <summary>
    /// Outcome of <see cref="MorphologyGraph.ApplyNeighborCorrection"/>: which Location IDs had a successful
    /// field sample (including a true zero residual).
    /// </summary>
    public sealed class NeighborCorrectionResult
    {
        public static NeighborCorrectionResult Skipped { get; } = new(false, []);

        public NeighborCorrectionResult(bool applied, HashSet<ulong> handledLocationIds)
        {
            Applied = applied;
            HandledLocationIds = handledLocationIds ?? [];
        }

        /// <summary>False when the residual corpus was too small and no sampling ran.</summary>
        public bool Applied { get; }

        /// <summary>Location IDs where <c>Sample</c> succeeded (zero residual still counts as handled).</summary>
        public HashSet<ulong> HandledLocationIds { get; }
    }

    partial class MorphologyGraph
    {
        /// <summary>
        /// Apply the spatial residual-field correction to each top-level cell under this factory root.
        /// Rigid XY translate of every cell annotation sampled at its centroid; co-moves child subgraphs attached
        /// to that location. This is the Neighbor flag only. CurveFit is a separate later outlier pass, not a
        /// fallback for unsampled points.
        /// </summary>
        /// <param name="root">Factory root whose subgraphs are the correction targets (and residual sources).</param>
        /// <param name="additionalHopSources">
        /// Optional neighbor cells (e.g. auto-loaded within a distance). Used only for residual consensus; not meshed.
        /// </param>
        /// <param name="maxOffsetNm">When set, cap each translation magnitude; omitted means no magnitude clamp.</param>
        public static NeighborCorrectionResult ApplyNeighborCorrection(
            MorphologyGraph root,
            IEnumerable<MorphologyGraph> additionalHopSources = null,
            double? maxOffsetNm = null)
        {
            if (root is null)
                return NeighborCorrectionResult.Skipped;

            List<MorphologyGraph> targets = [.. root.Subgraphs.Values.Where(sg => sg.StructureID != 0)];
            if (targets.Count == 0 && root.StructureID != 0 && root.Nodes.Count > 0)
                targets.Add(root);

            if (targets.Count == 0)
            {
                Console.WriteLine("Residual-field correction: no target cells; skipping.");
                return NeighborCorrectionResult.Skipped;
            }

            List<MorphologyGraph> residualSources = [.. targets];
            if (additionalHopSources != null)
            {
                HashSet<ulong> seen = [.. residualSources.Select(c => c.StructureID)];
                foreach (MorphologyGraph neighbor in additionalHopSources)
                {
                    if (neighbor is null || neighbor.StructureID == 0)
                        continue;
                    if (!seen.Add(neighbor.StructureID))
                        continue;
                    residualSources.Add(neighbor);
                }
            }

            if (residualSources.Count < 2)
            {
                Console.WriteLine("Residual-field correction: need at least two cells in the corpus (targets + neighbors); skipping.");
                return NeighborCorrectionResult.Skipped;
            }

            List<NeighborResidualField.ResidualSample> residuals =
                NeighborResidualField.CollectResiduals(residualSources);
            Console.WriteLine(
                $"Residual-field correction: {residuals.Count} residuals from {residualSources.Count} cells " +
                $"({targets.Count} targets) (grid={NeighborResidualField.GridSizeNm}nm, " +
                $"R={NeighborResidualField.KernelRadiusNm}nm, Nmin={NeighborResidualField.MinAnnotationVotes})");

            NeighborResidualField overview = NeighborResidualField.Build(residuals);
            foreach (string line in overview.DescribeSections())
                Console.WriteLine($"  field {line}");

            ConcurrentBag<ulong> handled = [];
            int nodesTranslated = 0;

            Parallel.ForEach(targets, cell =>
            {
                NeighborResidualField field = NeighborResidualField.Build(
                    residuals, excludeStructureId: cell.StructureID);
                int translated = ApplyNeighborCorrectionToCell(cell, field, maxOffsetNm, handled);
                Interlocked.Add(ref nodesTranslated, translated);
            });

            root._RTree = null;
            root.ResetCachedMeasurements();
            Console.WriteLine($"Residual-field correction: handled {handled.Count} annotations ({nodesTranslated} translated)");
            return new NeighborCorrectionResult(true, [.. handled]);
        }

        /// <summary>
        /// Apply a prebuilt field (published map or in-process lattice) to each target cell.
        /// Does not leave-one-out the target structure — the stored map already includes everyone.
        /// </summary>
        public static NeighborCorrectionResult ApplyResidualField(
            MorphologyGraph root,
            NeighborResidualField field,
            double? maxOffsetNm = null)
        {
            if (root is null || field is null || field.CellCount == 0)
                return NeighborCorrectionResult.Skipped;

            List<MorphologyGraph> targets = [.. root.Subgraphs.Values.Where(sg => sg.StructureID != 0)];
            if (targets.Count == 0 && root.StructureID != 0 && root.Nodes.Count > 0)
                targets.Add(root);

            if (targets.Count == 0)
                return NeighborCorrectionResult.Skipped;

            ConcurrentBag<ulong> handled = [];
            int nodesTranslated = 0;
            Parallel.ForEach(targets, cell =>
            {
                int translated = ApplyNeighborCorrectionToCell(cell, field, maxOffsetNm, handled);
                Interlocked.Add(ref nodesTranslated, translated);
            });

            root._RTree = null;
            root.ResetCachedMeasurements();
            Console.WriteLine($"Residual-field correction (published): handled {handled.Count} annotations ({nodesTranslated} translated)");
            return new NeighborCorrectionResult(true, [.. handled]);
        }

        /// <summary>
        /// Sample the residual field at each cell location centroid and rigidly translate that annotation
        /// plus child subgraphs whose nearest parent location is that node.
        /// Adds every successfully sampled Location ID to <paramref name="handled"/> (including zero residual).
        /// </summary>
        static int ApplyNeighborCorrectionToCell(
            MorphologyGraph cell,
            NeighborResidualField field,
            double? maxOffsetNm,
            ConcurrentBag<ulong> handled)
        {
            int translated = 0;
            foreach (MorphologyNode node in cell.Nodes.Values)
            {
                int sectionZ = (int)Math.Round(node.UnscaledZ);
                // Sample returns consensus (predicted − actual); apply directly.
                Vector2? correction = field.Sample(node.Center.XY(), sectionZ);
                if (correction is null)
                    continue;

                handled.Add(node.Key);

                Vector2 offset = ClampProcessOffset(node, correction.Value, maxOffsetNm);
                if (offset.Magnitude <= Tolerance.Epsilon)
                    continue;

                TranslateNodeAndAttachedSubgraphs(cell, node, offset);
                translated++;
            }

            cell._RTree = null;
            cell.ResetCachedMeasurements();
            return translated;
        }
    }
}
