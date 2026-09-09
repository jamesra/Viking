using Geometry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AnnotationVizLib
{
    /// <summary>
    /// Spatially varying residual registration field from unbranched Z-traveling process hops.
    /// Built once from an in-memory neighbor slab, then sampled at contour centroids.
    /// </summary>
    public sealed class NeighborHopField
    {
        public const double GridSizeNm = 2000.0;
        public const int MinSampleCount = 3;
        public const double MaxHopMagnitudeNm = 5000.0;

        readonly Dictionary<(int Z, int Gx, int Gy), Vector2> _cumulativeT;
        readonly Dictionary<(int Z, int Gx, int Gy), int> _sampleCounts;
        readonly int _pinSection;

        NeighborHopField(
            Dictionary<(int Z, int Gx, int Gy), Vector2> cumulativeT,
            Dictionary<(int Z, int Gx, int Gy), int> sampleCounts,
            int pinSection)
        {
            _cumulativeT = cumulativeT;
            _sampleCounts = sampleCounts;
            _pinSection = pinSection;
        }

        public int PinSection => _pinSection;
        public int CellCount => _cumulativeT.Count;

        /// <summary>
        /// Collect section-to-section hops from unbranched process chains on each cell.
        /// </summary>
        public static List<HopSample> CollectHops(IEnumerable<MorphologyGraph> cells)
        {
            List<HopSample> hops = [];
            foreach (MorphologyGraph cell in cells)
            {
                if (cell is null || cell.StructureID == 0)
                    continue;

                foreach (ulong[] process in cell.Processes())
                {
                    if (process.Length < 2)
                        continue;

                    MorphologyNode[] nodes = [.. process.Select(id => cell.Nodes[id]).OrderBy(n => n.UnscaledZ).ThenBy(n => n.Z)];
                    for (int i = 0; i < nodes.Length - 1; i++)
                    {
                        MorphologyNode lower = nodes[i];
                        MorphologyNode upper = nodes[i + 1];
                        if (!lower.IsUnbranchedProcess(cell) && i > 0)
                            continue;

                        int z0 = (int)Math.Round(lower.UnscaledZ);
                        int z1 = (int)Math.Round(upper.UnscaledZ);
                        if (z1 <= z0)
                            continue;

                        Vector2 c0 = lower.Center.XY();
                        Vector2 dxy = upper.Center.XY() - c0;
                        if (dxy.Magnitude > MaxHopMagnitudeNm)
                            continue;

                        // Multi-section gaps: attribute hop to the lower section only (PoC).
                        hops.Add(new HopSample(
                            cell.StructureID,
                            z0,
                            c0.X,
                            c0.Y,
                            dxy.X,
                            dxy.Y));
                    }
                }
            }

            return hops;
        }

        public static NeighborHopField Build(IEnumerable<HopSample> hops, ulong? excludeStructureId = null)
        {
            IEnumerable<HopSample> filtered = excludeStructureId is null
                ? hops
                : hops.Where(h => h.StructureID != excludeStructureId.Value);

            ConcurrentDictionary<(int Z, int Gx, int Gy), ConcurrentBag<Vector2>> bins = new();

            Parallel.ForEach(filtered, hop =>
            {
                int gx = (int)Math.Floor(hop.X / GridSizeNm);
                int gy = (int)Math.Floor(hop.Y / GridSizeNm);
                var key = (hop.SectionZ, gx, gy);
                ConcurrentBag<Vector2> bag = bins.GetOrAdd(key, _ => []);
                bag.Add(new Vector2(hop.DX, hop.DY));
            });

            Dictionary<(int Z, int Gx, int Gy), Vector2> medianHop = [];
            Dictionary<(int Z, int Gx, int Gy), int> counts = [];

            foreach (KeyValuePair<(int Z, int Gx, int Gy), ConcurrentBag<Vector2>> pair in bins)
            {
                Vector2[] samples = [.. pair.Value];
                if (samples.Length < MinSampleCount)
                    continue;

                Vector2 median = MedianVector(samples);
                medianHop[pair.Key] = median;
                counts[pair.Key] = samples.Length;
            }

            int pinSection = 0;
            if (medianHop.Count > 0)
            {
                int[] zs = [.. medianHop.Keys.Select(k => k.Z).Distinct().OrderBy(z => z)];
                pinSection = zs[zs.Length / 2];
            }

            Dictionary<(int Z, int Gx, int Gy), Vector2> cumulative =
                IntegrateCumulative(medianHop, pinSection);

            return new NeighborHopField(cumulative, counts, pinSection);
        }

        /// <summary>
        /// Cumulative registration offset at (xy, section). Null when no consensus near the query.
        /// Correction to apply is typically <c>-Sample(...)</c>.
        /// </summary>
        public Vector2? Sample(Vector2 xy, int sectionZ)
        {
            double fx = xy.X / GridSizeNm;
            double fy = xy.Y / GridSizeNm;
            int gx0 = (int)Math.Floor(fx);
            int gy0 = (int)Math.Floor(fy);
            double tx = fx - gx0;
            double ty = fy - gy0;

            bool TryGet(int gx, int gy, out Vector2 t, out int n)
            {
                var key = (sectionZ, gx, gy);
                if (_cumulativeT.TryGetValue(key, out t) &&
                    _sampleCounts.TryGetValue(key, out n) &&
                    n >= MinSampleCount)
                    return true;
                t = Vector2.Zero;
                n = 0;
                return false;
            }

            if (TryGet(gx0, gy0, out Vector2 t00, out _) &&
                TryGet(gx0 + 1, gy0, out Vector2 t10, out _) &&
                TryGet(gx0, gy0 + 1, out Vector2 t01, out _) &&
                TryGet(gx0 + 1, gy0 + 1, out Vector2 t11, out _))
            {
                Vector2 a = t00 * (1 - tx) + t10 * tx;
                Vector2 b = t01 * (1 - tx) + t11 * tx;
                return a * (1 - ty) + b * ty;
            }

            // Nearest valid cell within ~1.5 grid radii.
            Vector2? best = null;
            double bestDist2 = double.MaxValue;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!TryGet(gx0 + dx, gy0 + dy, out Vector2 t, out _))
                        continue;

                    double cx = (gx0 + dx + 0.5) * GridSizeNm;
                    double cy = (gy0 + dy + 0.5) * GridSizeNm;
                    double d2 = (xy.X - cx) * (xy.X - cx) + (xy.Y - cy) * (xy.Y - cy);
                    if (d2 < bestDist2)
                    {
                        bestDist2 = d2;
                        best = t;
                    }
                }
            }

            const double maxDist2 = (1.5 * GridSizeNm) * (1.5 * GridSizeNm);
            if (best is not null && bestDist2 <= maxDist2)
                return best;

            return null;
        }

        /// <summary>
        /// One line of consensus stats per occupied section for console A/B.
        /// </summary>
        public IEnumerable<string> DescribeSections()
        {
            foreach (IGrouping<int, KeyValuePair<(int Z, int Gx, int Gy), Vector2>> group in
                     _cumulativeT.GroupBy(kv => kv.Key.Z).OrderBy(g => g.Key))
            {
                int z = group.Key;
                int cells = group.Count();
                double[] mags = [.. group.Select(kv => kv.Value.Magnitude)];
                Array.Sort(mags);
                double medianMag = mags[mags.Length / 2];
                int samples = group.Sum(kv => _sampleCounts.TryGetValue(kv.Key, out int n) ? n : 0);
                yield return $"Z={z} gridCells={cells} hopSamples={samples} median|T|={medianMag:F1}nm pin={(z == _pinSection ? "yes" : "no")}";
            }
        }

        static Dictionary<(int Z, int Gx, int Gy), Vector2> IntegrateCumulative(
            Dictionary<(int Z, int Gx, int Gy), Vector2> medianHop,
            int pinSection)
        {
            Dictionary<(int Z, int Gx, int Gy), Vector2> cumulative = [];
            if (medianHop.Count == 0)
                return cumulative;

            int[] sections = [.. medianHop.Keys.Select(k => k.Z).Distinct().OrderBy(z => z)];
            HashSet<(int Gx, int Gy)> columns = [.. medianHop.Keys.Select(k => (k.Gx, k.Gy))];

            foreach ((int Gx, int Gy) col in columns)
            {
                Vector2 running = Vector2.Zero;
                Dictionary<int, Vector2> tAtZ = [];

                foreach (int z in sections)
                {
                    tAtZ[z] = running;
                    if (medianHop.TryGetValue((z, col.Gx, col.Gy), out Vector2 hop))
                        running += hop;
                }

                if (!tAtZ.TryGetValue(pinSection, out Vector2 pinT))
                {
                    // Pin to nearest section that has a value in this column.
                    int nearest = sections.OrderBy(z => Math.Abs(z - pinSection)).First();
                    pinT = tAtZ[nearest];
                }

                foreach (KeyValuePair<int, Vector2> pair in tAtZ)
                    cumulative[(pair.Key, col.Gx, col.Gy)] = pair.Value - pinT;
            }

            return cumulative;
        }

        static Vector2 MedianVector(Vector2[] samples)
        {
            double[] xs = [.. samples.Select(v => v.X)];
            double[] ys = [.. samples.Select(v => v.Y)];
            Array.Sort(xs);
            Array.Sort(ys);
            return new Vector2(xs[xs.Length / 2], ys[ys.Length / 2]);
        }

        public readonly struct HopSample(ulong structureId, int sectionZ, double x, double y, double dx, double dy)
        {
            public readonly ulong StructureID = structureId;
            public readonly int SectionZ = sectionZ;
            public readonly double X = x;
            public readonly double Y = y;
            public readonly double DX = dx;
            public readonly double DY = dy;
        }
    }

    public partial class MorphologyGraph
    {
        /// <summary>
        /// Apply a leave-one-out neighbor hop correction to each top-level cell under this factory root.
        /// Rigid XY translate of unbranched process nodes only; co-moves attached child subgraphs.
        /// Call after optional <see cref="SmoothProcesses"/> and before SliceGraph.Create.
        /// </summary>
        /// <param name="root">Factory root whose subgraphs are the correction targets (and hop sources).</param>
        /// <param name="additionalHopSources">
        /// Optional neighbor cells (e.g. auto-loaded within a distance). Used only for hop consensus; not meshed.
        /// </param>
        public static void ApplyNeighborCorrection(
            MorphologyGraph root,
            IEnumerable<MorphologyGraph> additionalHopSources = null)
        {
            if (root is null)
                return;

            List<MorphologyGraph> targets = [.. root.Subgraphs.Values.Where(sg => sg.StructureID != 0)];
            if (targets.Count == 0 && root.StructureID != 0 && root.Nodes.Count > 0)
                targets.Add(root);

            if (targets.Count == 0)
            {
                Console.WriteLine("Neighbor correction: no target cells; skipping.");
                return;
            }

            List<MorphologyGraph> hopSources = [.. targets];
            if (additionalHopSources != null)
            {
                HashSet<ulong> seen = [.. hopSources.Select(c => c.StructureID)];
                foreach (MorphologyGraph neighbor in additionalHopSources)
                {
                    if (neighbor is null || neighbor.StructureID == 0)
                        continue;
                    if (!seen.Add(neighbor.StructureID))
                        continue;
                    hopSources.Add(neighbor);
                }
            }

            if (hopSources.Count < 2)
            {
                Console.WriteLine("Neighbor correction: need at least two cells in the hop corpus (targets + neighbors); skipping.");
                return;
            }

            List<NeighborHopField.HopSample> hops = NeighborHopField.CollectHops(hopSources);
            Console.WriteLine($"Neighbor correction: {hops.Count} hops from {hopSources.Count} cells ({targets.Count} targets) (grid={NeighborHopField.GridSizeNm}nm, Nmin={NeighborHopField.MinSampleCount})");

            NeighborHopField overview = NeighborHopField.Build(hops);
            foreach (string line in overview.DescribeSections())
                Console.WriteLine($"  field {line}");

            int nodesMoved = 0;
            object moveLock = new();

            Parallel.ForEach(targets, cell =>
            {
                NeighborHopField field = NeighborHopField.Build(hops, excludeStructureId: cell.StructureID);
                int moved = ApplyNeighborCorrectionToCell(cell, field);
                lock (moveLock)
                    nodesMoved += moved;
            });

            root._RTree = null;
            root.ResetCachedMeasurements();
            Console.WriteLine($"Neighbor correction: translated {nodesMoved} process nodes");
        }

        static int ApplyNeighborCorrectionToCell(MorphologyGraph cell, NeighborHopField field)
        {
            int moved = 0;
            foreach (ulong[] process in cell.Processes())
            {
                if (process.Length < 3)
                    continue;

                foreach (ulong id in process)
                {
                    MorphologyNode node = cell.Nodes[id];
                    if (!node.IsUnbranchedProcess(cell))
                        continue;

                    int sectionZ = (int)Math.Round(node.UnscaledZ);
                    Vector2? registration = field.Sample(node.Center.XY(), sectionZ);
                    if (registration is null)
                        continue;

                    Vector2 offset = ClampProcessOffset(node, -registration.Value);
                    if (offset.Magnitude <= Tolerance.Epsilon)
                        continue;

                    TranslateNodeAndAttachedSubgraphs(cell, node, offset);
                    moved++;
                }
            }

            cell._RTree = null;
            cell.ResetCachedMeasurements();
            return moved;
        }
    }
}
