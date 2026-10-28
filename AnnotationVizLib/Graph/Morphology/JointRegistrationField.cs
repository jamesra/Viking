using Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnnotationVizLib
{
    /// <summary>
    /// Options for <see cref="JointRegistrationSolver"/>. Defaults were chosen on RPC1, RPC2, RC1 and RC2
    /// (Oct 2026) by scoring structures held out of the fit.
    /// </summary>
    public sealed class JointRegistrationOptions
    {
        /// <summary>Lattice spacing of the unknown per-section shifts. Nodes sit at (gx * Pitch, gy * Pitch).</summary>
        public double PitchNm { get; set; } = 4000.0;

        /// <summary>
        /// Weights of the membrane penalty between neighbouring lattice nodes on one section, tried per volume
        /// (larger: smoother registration across XY).
        /// </summary>
        public double[] SpatialCandidates { get; set; } = [0.3, 3.0, 30.0];

        /// <summary>
        /// Ridge weights tried per volume (larger: assumes smaller registration errors). Every spatial x ridge pair is
        /// fit on the non-held-out structures; the pair that best smooths the held-out structures is used.
        /// </summary>
        public double[] RidgeCandidates { get; set; } = [0.0001, 0.001, 0.01, 0.1, 1.0];

        /// <summary>Huber threshold on a process's second difference (nm) for the robust reweighting.</summary>
        public double HuberNm { get; set; } = 150.0;

        public int IrlsIterations { get; set; } = 3;

        /// <summary>Largest section gap between consecutive annotations of a process that still forms a triple.</summary>
        public int MaxSectionGap { get; set; } = 2;

        /// <summary>Triples whose raw second difference exceeds this are not used (mis-links, branches).</summary>
        public double MaxSecondDifferenceNm { get; set; } = 5000.0;

        /// <summary>Share of structures (by ID hash) held out to choose the ridge and to check the fit helps.</summary>
        public int HeldOutPercent { get; set; } = 15;

        /// <summary>Held-out triples needed before the held-out check is trusted; below it the middle ridge is used.</summary>
        public int MinHeldOutTriples { get; set; } = 500;

        /// <summary>A lattice node is published when at least this many triples touch it.</summary>
        public int MinSupport { get; set; } = 1;

        public int MaxCgIterations { get; set; } = 4000;

        public double CgRelativeTolerance { get; set; } = 1e-7;

        /// <summary>Section held at zero shift (the identity). Null: the median section of the data.</summary>
        public int? PinSection { get; set; }

        /// <summary>Published IDW reach for samples outside the bilinear support.</summary>
        public double KernelRadiusNm => 2.0 * PitchNm;

        /// <summary>
        /// Damaged sections (the volume block's <c>NonStosSectionNumbers</c>: torn, folded or otherwise left out of
        /// slice-to-volume registration). A damaged section never corrects another section: the main fit uses no
        /// annotation on it and bridges the sections either side. Each damaged section's own shift is fitted
        /// afterwards from its annotations against the corrected sections either side. Empty: no section is damaged.
        /// </summary>
        public IReadOnlyCollection<int> DamagedSections { get; set; } = [];

        /// <summary>A copy that can be given a volume's own <see cref="DamagedSections"/> without changing this one.</summary>
        public JointRegistrationOptions Clone() => (JointRegistrationOptions)MemberwiseClone();
    }

    /// <summary>Score of one (spatial weight, ridge) candidate on the held-out structures.</summary>
    public readonly struct RidgeScore(double spatial, double ridge, double heldOutMedianNm)
    {
        public readonly double Spatial = spatial;
        public readonly double Ridge = ridge;
        public readonly double HeldOutMedianNm = heldOutMedianNm;
    }

    public sealed class JointRegistrationResult
    {
        /// <summary>The correction field. Empty (identity) when the fit did not improve the held-out structures.</summary>
        public NeighborResidualField Field { get; set; }
        public double ChosenRidge { get; set; }
        public double ChosenSpatial { get; set; }
        public bool Identity { get; set; }
        public int PinSection { get; set; }
        public int Triples { get; set; }
        public int HeldOutTriples { get; set; }
        public double HeldOutRawMedianNm { get; set; }
        public double HeldOutCorrectedMedianNm { get; set; }
        public IReadOnlyList<RidgeScore> Scores { get; set; }

        /// <summary>Damaged sections the fit was told about (<see cref="JointRegistrationOptions.DamagedSections"/>), ascending.</summary>
        public IReadOnlyList<int> DamagedSections { get; set; } = [];

        /// <summary>Triples centred on a damaged section, which fit the damaged sections' own shifts.</summary>
        public int DamagedTriples { get; set; }

        /// <summary>
        /// Median second difference of the damaged-section triples, against the corrected sections either side, before
        /// and after the damaged sections' own shifts (NaN when there were none). The damaged shifts are published only
        /// when they lower it.
        /// </summary>
        public double DamagedRawMedianNm { get; set; } = double.NaN;
        public double DamagedCorrectedMedianNm { get; set; } = double.NaN;

        /// <summary>
        /// The fit with the chosen ridge on the non-held-out structures only (null when no selection ran). Scoring it
        /// on the held-out structures is an honest estimate of how the published field behaves on new annotations.
        /// </summary>
        public NeighborResidualField SelectionField { get; set; }
    }

    /// <summary>
    /// Three consecutive annotations of one unbranched process: B sits between A (below) and C (above).
    /// </summary>
    public readonly struct ProcessTriple(ulong structureId, int za, Vector2 a, int zb, Vector2 b, int zc, Vector2 c, double alpha)
    {
        public readonly ulong StructureID = structureId;
        public readonly int ZA = za, ZB = zb, ZC = zc;
        public readonly Vector2 A = a, B = b, C = c;

        /// <summary>(zB - zA) / (zC - zA): where B lies between A and C.</summary>
        public readonly double Alpha = alpha;

        /// <summary>B minus the straight line from A to C at B's section (nm).</summary>
        public Vector2 SecondDifference => B - (A * (1 - Alpha)) - (C * Alpha);
    }

    /// <summary>
    /// Per-section registration shifts solved jointly over every section, so that every process is as smooth in Z as
    /// possible after correction.
    /// <para>
    /// Unknowns: one shift e(z, gx, gy) per lattice node; a point's shift is the bilinear blend of its four corner
    /// nodes, the same blend <see cref="NeighborResidualField.SampleAlways"/> uses. For each triple the corrected
    /// second difference (B + eB) - (1-a)(A + eA) - a(C + eC) should be zero. A membrane penalty ties neighbouring
    /// nodes of a section, a ridge keeps shifts small where the data are weak, and the pin section is held at zero.
    /// Second differences cannot see a shift that changes linearly with Z, so slow drift through the volume (which
    /// is indistinguishable from real tissue tilt) is left alone. Solved by Huber-reweighted least squares
    /// (Jacobi-preconditioned conjugate gradients on the normal equations); X and Y are independent.
    /// </para>
    /// <para>
    /// Replaces the per-section leave-one-out residual field (<see cref="NeighborResidualField.Build"/>), which
    /// measured each section against curves through uncorrected neighbouring sections. With registration error on
    /// every section that estimate is e(z) minus the mean of its neighbours' errors: adjacent sections come out
    /// anti-correlated (about -0.5 on RPC1, RPC2, RC2) and the published field made held-out structures less smooth.
    /// </para>
    /// </summary>
    public static class JointRegistrationSolver
    {
        /// <summary>
        /// Triples of consecutive annotations along each unbranched process, none of them on a damaged section. A damaged
        /// section is skipped as the slice-to-volume registration skips it: the annotations either side form the triple,
        /// and the sections skipped do not count toward <paramref name="maxSectionGap"/>.
        /// </summary>
        public static List<ProcessTriple> CollectTriples(IEnumerable<MorphologyGraph> cells, int maxSectionGap = 2,
            IReadOnlyCollection<int> damagedSections = null)
        {
            ISet<int> damaged = AsSet(damagedSections);
            List<ProcessTriple> triples = [];
            foreach ((MorphologyGraph cell, MorphologyNode[] nodes) in ProcessChains(cells))
            {
                MorphologyNode[] kept = damaged.Count == 0 ? nodes : [.. nodes.Where(n => !damaged.Contains(SectionOf(n)))];
                for (int i = 1; i + 1 < kept.Length; i++)
                {
                    if (TryTriple(cell, kept[i - 1], kept[i], kept[i + 1], maxSectionGap, damaged, out ProcessTriple t))
                        triples.Add(t);
                }
            }
            return triples;
        }

        /// <summary>
        /// Triples centred on an annotation on a damaged section, with A and C the nearest annotations of the process
        /// below and above it on undamaged sections. They fit the damaged sections' own shifts after the main fit.
        /// </summary>
        public static List<ProcessTriple> CollectDamagedTriples(IEnumerable<MorphologyGraph> cells, IReadOnlyCollection<int> damagedSections,
            int maxSectionGap = 2)
        {
            ISet<int> damaged = AsSet(damagedSections);
            List<ProcessTriple> triples = [];
            if (damaged.Count == 0)
                return triples;
            foreach ((MorphologyGraph cell, MorphologyNode[] nodes) in ProcessChains(cells))
            {
                for (int i = 1; i + 1 < nodes.Length; i++)
                {
                    if (!damaged.Contains(SectionOf(nodes[i])))
                        continue;
                    int below = i - 1, above = i + 1;
                    while (below >= 0 && damaged.Contains(SectionOf(nodes[below])))
                        below--;
                    while (above < nodes.Length && damaged.Contains(SectionOf(nodes[above])))
                        above++;
                    if (below < 0 || above >= nodes.Length)
                        continue;
                    if (TryTriple(cell, nodes[below], nodes[i], nodes[above], maxSectionGap, damaged, out ProcessTriple t))
                        triples.Add(t);
                }
            }
            return triples;
        }

        static IEnumerable<(MorphologyGraph Cell, MorphologyNode[] Nodes)> ProcessChains(IEnumerable<MorphologyGraph> cells)
        {
            foreach (MorphologyGraph cell in cells)
            {
                if (cell is null || cell.StructureID == 0)
                    continue;
                foreach (ulong[] process in cell.Processes())
                {
                    if (process.Length >= 3)
                        yield return (cell, [.. process.Select(id => cell.Nodes[id])]);
                }
            }
        }

        static bool TryTriple(MorphologyGraph cell, MorphologyNode a, MorphologyNode b, MorphologyNode c, int maxSectionGap, ISet<int> damaged,
            out ProcessTriple triple)
        {
            triple = default;
            if (IsBranchTriple(cell, a, b, c))
                return false;
            int za = SectionOf(a), zb = SectionOf(b), zc = SectionOf(c);
            if (!WithinGap(za, zb, maxSectionGap, damaged) || !WithinGap(zb, zc, maxSectionGap, damaged))
                return false;
            double alpha = (b.Z - a.Z) / (c.Z - a.Z);
            triple = new ProcessTriple(cell.StructureID, za, a.Center.XY(), zb, b.Center.XY(), zc, c.Center.XY(), alpha);
            return true;
        }

        /// <summary>True when <paramref name="z1"/> is above <paramref name="z0"/> by at most <paramref name="maxSectionGap"/> undamaged sections.</summary>
        static bool WithinGap(int z0, int z1, int maxSectionGap, ISet<int> damaged)
        {
            if (z1 <= z0)
                return false;
            int skipped = 0;
            if (damaged.Count > 0)
            {
                for (int z = z0 + 1; z < z1; z++)
                {
                    if (damaged.Contains(z))
                        skipped++;
                }
            }
            return z1 - z0 - skipped <= maxSectionGap;
        }

        static int SectionOf(MorphologyNode node) => (int)Math.Round(node.UnscaledZ);

        static ISet<int> AsSet(IReadOnlyCollection<int> sections) =>
            sections is null || sections.Count == 0 ? new HashSet<int>() : sections as ISet<int> ?? new HashSet<int>(sections);

        /// <summary>
        /// True when any point of the triple is a branch. <see cref="MorphologyGraph.Processes"/> ends each chain on
        /// the branch it runs into, so a branch is never B but was A or C of the triple next to it.
        /// <para>
        /// Daughter side (branch, d1, d2): the branch centroid is the middle of the merged area, off to one side of
        /// each daughter's centre line, so d1 read as misregistered by about half the gap between the daughters, in
        /// opposite directions for the two daughters.  That is what made the published shift jump at a split.
        /// </para>
        /// <para>
        /// Parent side (p2, p1, branch): the branch is the same object as p1 one section up, so this triple is usually
        /// valid and could be kept.  It is dropped because the merged area often widens or forms two lobes before it
        /// separates, which moves its centroid off the parent's centre line for reasons of shape, not registration;
        /// and because "no branch is ever A, B or C" is a rule a property test can check.  It costs one triple per
        /// split.
        /// </para>
        /// <para>
        /// Curvefit deliberately keeps the branch on its one-link side (<see cref="MorphologyGraph.CurveFitControlMask"/>).
        /// The difference is intended; do not "unify" the two.  This field averages many cells and can afford to lose
        /// that sample to avoid bias, while curvefit fits one chain and would otherwise lose the parent's only fitting
        /// point above its last node.
        /// </para>
        /// </summary>
        internal static bool IsBranchTriple(MorphologyGraph cell, MorphologyNode a, MorphologyNode b, MorphologyNode c) =>
            a.IsBranchAnchor(cell) || b.IsBranchAnchor(cell) || c.IsBranchAnchor(cell);

        /// <summary>True for structures held out of the ridge-selection fit.</summary>
        public static bool IsHeldOut(ulong structureId, int percent) => (structureId * 2654435761UL) % 100UL < (ulong)percent;

        /// <summary>
        /// The field for <paramref name="cells"/>. Damaged sections (<see cref="JointRegistrationOptions.DamagedSections"/>)
        /// are solved in a second pass so a tear or fold on one cannot pull its neighbours: the main fit runs on triples
        /// with no point on a damaged section; then each damaged section's shift is fitted from triples centred on it,
        /// with A and C corrected by the main fit and held fixed. The damaged shifts are kept only when they lower those
        /// triples' median second difference.
        /// </summary>
        public static JointRegistrationResult Solve(IReadOnlyList<MorphologyGraph> cells, JointRegistrationOptions options = null, Action<string> log = null)
        {
            options ??= new JointRegistrationOptions();
            log ??= _ => { };
            IReadOnlyCollection<int> damaged = options.DamagedSections ?? [];
            JointRegistrationResult result = Solve(CollectTriples(cells, options.MaxSectionGap, damaged), options, log);
            result.DamagedSections = [.. damaged.OrderBy(z => z)];
            if (damaged.Count == 0)
                return result;

            List<ProcessTriple> damagedTriples = [.. CollectDamagedTriples(cells, damaged, options.MaxSectionGap)
                .Select(t => Corrected(t, result.Field))
                .Where(t => t.SecondDifference.Magnitude <= options.MaxSecondDifferenceNm)];
            result.DamagedTriples = damagedTriples.Count;
            if (damagedTriples.Count == 0)
            {
                log($"joint registration: {damaged.Count} damaged sections, no triples centred on them");
                return result;
            }

            ISet<int> damagedSet = AsSet(damaged);
            NeighborResidualField own = Fit(damagedTriples, result.ChosenSpatial, result.ChosenRidge, int.MinValue, options, log,
                isFixed: z => !damagedSet.Contains(z));
            double before = Median([.. damagedTriples.Select(t => t.SecondDifference.Magnitude)]);
            double after = HeldOutMedian(own, damagedTriples);
            result.DamagedRawMedianNm = before;
            result.DamagedCorrectedMedianNm = after;
            log($"joint registration: {damaged.Count} damaged sections, {damagedTriples.Count} triples centred on them: " +
                $"median second difference {before:F1} -> {after:F1} nm against the corrected sections either side");
            if (!(after < before))
            {
                log("joint registration: the damaged-section shifts do not help; damaged sections get no shift");
                return result;
            }

            result.Field = NeighborResidualField.FromLattice(
                [.. result.Field.EnumerateNodes(), .. own.EnumerateNodes().Where(n => damagedSet.Contains(n.Z))],
                options.PitchNm, options.KernelRadiusNm);
            return result;
        }

        /// <summary><paramref name="t"/> with A and C moved by <paramref name="field"/>, B unchanged.</summary>
        static ProcessTriple Corrected(ProcessTriple t, NeighborResidualField field) => new(
            t.StructureID,
            t.ZA, t.A + (field.Sample(t.A, t.ZA) ?? Vector2.Zero),
            t.ZB, t.B,
            t.ZC, t.C + (field.Sample(t.C, t.ZC) ?? Vector2.Zero),
            t.Alpha);

        public static JointRegistrationResult Solve(IReadOnlyList<ProcessTriple> allTriples, JointRegistrationOptions options = null, Action<string> log = null)
        {
            options ??= new JointRegistrationOptions();
            log ??= _ => { };
            List<ProcessTriple> triples = [.. allTriples.Where(t => t.SecondDifference.Magnitude <= options.MaxSecondDifferenceNm)];
            if (triples.Count == 0)
            {
                log("joint registration: no process triples; publishing the identity");
                return new JointRegistrationResult { Field = NeighborResidualField.FromLattice([], options.PitchNm, options.KernelRadiusNm), Identity = true, Scores = [] };
            }

            int pin = options.PinSection ?? Median(triples.Select(t => (double)t.ZB).ToArray(), round: true);
            List<ProcessTriple> train = [.. triples.Where(t => !IsHeldOut(t.StructureID, options.HeldOutPercent))];
            List<ProcessTriple> held = [.. triples.Where(t => IsHeldOut(t.StructureID, options.HeldOutPercent))];
            double rawHeld = held.Count > 0 ? Median(held.Select(t => t.SecondDifference.Magnitude).ToArray()) : double.NaN;
            log($"joint registration: {triples.Count} triples ({train.Count} fit, {held.Count} held out), pin section {pin}, " +
                $"pitch {options.PitchNm} nm");

            double[] ridges = options.RidgeCandidates is { Length: > 0 } ? options.RidgeCandidates : [0.01];
            double[] spatials = options.SpatialCandidates is { Length: > 0 } ? options.SpatialCandidates : [30.0];
            List<RidgeScore> scores = [];
            double chosenRidge = ridges[ridges.Length / 2], chosenSpatial = spatials[spatials.Length - 1];
            double bestHeld = double.NaN;
            NeighborResidualField selection = null;
            // Even with a single candidate the held-out fit runs: it is the do-no-harm check.
            if (held.Count >= options.MinHeldOutTriples)
            {
                (double Spatial, double Ridge)[] grid = [.. spatials.SelectMany(s => ridges.Select(r => (s, r)))];
                NeighborResidualField[] trials = new NeighborResidualField[grid.Length];
                double[] medians = new double[grid.Length];
                object logLock = new();
                void Locked(string msg)
                {
                    lock (logLock)
                        log(msg);
                }
                int parallel = Math.Max(1, Math.Min(grid.Length, Environment.ProcessorCount / 2));
                System.Threading.Tasks.Parallel.For(0, grid.Length, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = parallel }, k =>
                {
                    trials[k] = Fit(train, grid[k].Spatial, grid[k].Ridge, pin, options, Locked);
                    medians[k] = HeldOutMedian(trials[k], held);
                });
                for (int k = 0; k < grid.Length; k++)
                {
                    scores.Add(new RidgeScore(grid[k].Spatial, grid[k].Ridge, medians[k]));
                    log($"joint registration: spatial {grid[k].Spatial} ridge {grid[k].Ridge}: held-out second difference median {medians[k]:F1} nm (uncorrected {rawHeld:F1} nm)");
                    if (double.IsNaN(bestHeld) || medians[k] < bestHeld)
                    {
                        bestHeld = medians[k];
                        chosenRidge = grid[k].Ridge;
                        chosenSpatial = grid[k].Spatial;
                        selection = trials[k];
                    }
                }
                if (!(bestHeld < rawHeld))
                {
                    log("joint registration: no candidate improves the held-out structures; publishing the identity");
                    return new JointRegistrationResult
                    {
                        Field = NeighborResidualField.FromLattice([], options.PitchNm, options.KernelRadiusNm),
                        Identity = true, ChosenRidge = chosenRidge, ChosenSpatial = chosenSpatial, PinSection = pin, Triples = triples.Count,
                        HeldOutTriples = held.Count, HeldOutRawMedianNm = rawHeld, HeldOutCorrectedMedianNm = bestHeld, Scores = scores,
                        SelectionField = selection
                    };
                }
            }
            else
            {
                log($"joint registration: {held.Count} held-out triples is too few to choose the weights; using spatial {chosenSpatial} ridge {chosenRidge}");
            }

            NeighborResidualField field = Fit(triples, chosenSpatial, chosenRidge, pin, options, log);
            log($"joint registration: final fit on all triples, spatial {chosenSpatial} ridge {chosenRidge}, {field.CellCount} lattice nodes");
            return new JointRegistrationResult
            {
                Field = field, Identity = false, ChosenRidge = chosenRidge, ChosenSpatial = chosenSpatial, PinSection = pin, Triples = triples.Count,
                HeldOutTriples = held.Count, HeldOutRawMedianNm = rawHeld, HeldOutCorrectedMedianNm = bestHeld, Scores = scores,
                SelectionField = selection
            };
        }

        /// <summary>Median corrected second difference of <paramref name="triples"/> under <paramref name="field"/>.</summary>
        public static double HeldOutMedian(NeighborResidualField field, IReadOnlyList<ProcessTriple> triples)
        {
            if (triples.Count == 0)
                return double.NaN;
            double[] m = new double[triples.Count];
            for (int i = 0; i < triples.Count; i++)
            {
                ProcessTriple t = triples[i];
                Vector2 a = t.A + (field.Sample(t.A, t.ZA) ?? Vector2.Zero);
                Vector2 b = t.B + (field.Sample(t.B, t.ZB) ?? Vector2.Zero);
                Vector2 c = t.C + (field.Sample(t.C, t.ZC) ?? Vector2.Zero);
                m[i] = (b - (a * (1 - t.Alpha)) - (c * t.Alpha)).Magnitude;
            }
            return Median(m);
        }

        static int Median(double[] v, bool round)
        {
            Array.Sort(v);
            return (int)Math.Round(v[v.Length / 2]);
        }

        static double Median(double[] v)
        {
            if (v.Length == 0)
                return double.NaN;
            Array.Sort(v);
            return v.Length % 2 == 1 ? v[v.Length / 2] : 0.5 * (v[v.Length / 2 - 1] + v[v.Length / 2]);
        }

        /// <summary>
        /// One robust least-squares fit; returns the published lattice as a field. A triple point on a section
        /// <paramref name="isFixed"/> accepts adds no unknowns: its position is taken as already corrected.
        /// </summary>
        public static NeighborResidualField Fit(IReadOnlyList<ProcessTriple> triples, double spatial, double ridge, int pin, JointRegistrationOptions options,
            Action<string> log = null, Func<int, bool> isFixed = null)
        {
            double P = options.PitchNm;
            Dictionary<(int Z, int Gx, int Gy), int> index = [];
            List<(int Z, int Gx, int Gy)> keys = [];
            int R = triples.Count;
            int[] cols = new int[R * 12];
            double[] coef = new double[R * 12];
            double[] bx = new double[R], by = new double[R];

            int Col(int z, int gx, int gy)
            {
                var k = (z, gx, gy);
                if (!index.TryGetValue(k, out int i))
                {
                    i = keys.Count;
                    index[k] = i;
                    keys.Add(k);
                }
                return i;
            }

            void Corners(int row, int slot, int z, Vector2 p, double scale)
            {
                double fx = p.X / P, fy = p.Y / P;
                int gx = (int)Math.Floor(fx), gy = (int)Math.Floor(fy);
                double tx = fx - gx, ty = fy - gy;
                int o = row * 12 + slot * 4;
                cols[o] = Col(z, gx, gy); coef[o] = scale * (1 - tx) * (1 - ty);
                cols[o + 1] = Col(z, gx + 1, gy); coef[o + 1] = scale * tx * (1 - ty);
                cols[o + 2] = Col(z, gx, gy + 1); coef[o + 2] = scale * (1 - tx) * ty;
                cols[o + 3] = Col(z, gx + 1, gy + 1); coef[o + 3] = scale * tx * ty;
            }

            // A fixed point keeps valid column indices (B's) with zero coefficients, so every row still has 12 entries.
            void FixedCorners(int row, int slot)
            {
                int o = row * 12 + slot * 4, b = row * 12 + 4;
                for (int j = 0; j < 4; j++)
                {
                    cols[o + j] = cols[b + j];
                    coef[o + j] = 0;
                }
            }

            for (int r = 0; r < R; r++)
            {
                ProcessTriple t = triples[r];
                Corners(r, 1, t.ZB, t.B, 1.0);
                if (isFixed?.Invoke(t.ZA) == true)
                    FixedCorners(r, 0);
                else
                    Corners(r, 0, t.ZA, t.A, -(1 - t.Alpha));
                if (isFixed?.Invoke(t.ZC) == true)
                    FixedCorners(r, 2);
                else
                    Corners(r, 2, t.ZC, t.C, -t.Alpha);
                Vector2 d = t.SecondDifference;
                bx[r] = -d.X;
                by[r] = -d.Y;
            }

            int N = keys.Count;
            int[] support = new int[N];
            for (int r = 0; r < R; r++)
            {
                HashSet<int> seen = [];
                for (int j = 0; j < 12; j++)
                    if (coef[r * 12 + j] != 0 && seen.Add(cols[r * 12 + j]))
                        support[cols[r * 12 + j]]++;
            }

            List<(int, int)> edges = [];
            for (int i = 0; i < N; i++)
            {
                (int z, int gx, int gy) = keys[i];
                if (index.TryGetValue((z, gx + 1, gy), out int j))
                    edges.Add((i, j));
                if (index.TryGetValue((z, gx, gy + 1), out j))
                    edges.Add((i, j));
            }

            double[] ridgeDiag = new double[N];
            for (int i = 0; i < N; i++)
                ridgeDiag[i] = keys[i].Z == pin ? 1e4 : ridge;

            double[] w = new double[R];
            for (int r = 0; r < R; r++)
                w[r] = 1.0;
            double[] ex = new double[N], ey = new double[N];
            NormalOperator op = new(cols, coef, R, N, edges, spatial, ridgeDiag);
            for (int it = 0; it < Math.Max(1, options.IrlsIterations); it++)
            {
                op.Weights = w;
                int nx = 0, ny = 0;
                System.Threading.Tasks.Parallel.Invoke(
                    () => nx = op.Solve(bx, ex, options.MaxCgIterations, options.CgRelativeTolerance),
                    () => ny = op.Solve(by, ey, options.MaxCgIterations, options.CgRelativeTolerance));
                double[] res = new double[R];
                for (int r = 0; r < R; r++)
                {
                    double ax = 0, ay = 0;
                    for (int j = 0; j < 12; j++)
                    {
                        int c = cols[r * 12 + j];
                        ax += coef[r * 12 + j] * ex[c];
                        ay += coef[r * 12 + j] * ey[c];
                    }
                    res[r] = Math.Sqrt((ax - bx[r]) * (ax - bx[r]) + (ay - by[r]) * (ay - by[r]));
                    w[r] = res[r] <= options.HuberNm ? 1.0 : options.HuberNm / res[r];
                }
                log?.Invoke($"joint registration: spatial {spatial} ridge {ridge} pass {it + 1}: {N} unknowns, {R} triples, CG {nx}/{ny} iterations, " +
                            $"median corrected second difference {Median(res):F1} nm");
            }

            List<(int Z, int Gx, int Gy, Vector2 Offset, int Votes)> nodes = [];
            for (int i = 0; i < N; i++)
            {
                if (support[i] < options.MinSupport)
                    continue;
                nodes.Add((keys[i].Z, keys[i].Gx, keys[i].Gy, new Vector2(ex[i], ey[i]), support[i]));
            }
            return NeighborResidualField.FromLattice(nodes, P, options.KernelRadiusNm);
        }

        /// <summary>
        /// x -> (A' W A + s L + diag(ridge)) x for the 12-entry triple rows A, the lattice graph Laplacian L and the
        /// ridge; solved by Jacobi-preconditioned conjugate gradients.
        /// </summary>
        sealed class NormalOperator(int[] cols, double[] coef, int rows, int n, List<(int A, int B)> edges, double spatial, double[] ridge)
        {
            public double[] Weights { get; set; }

            void Apply(double[] x, double[] y)
            {
                for (int i = 0; i < n; i++)
                    y[i] = ridge[i] * x[i];
                for (int r = 0; r < rows; r++)
                {
                    int o = r * 12;
                    double t = 0;
                    for (int j = 0; j < 12; j++)
                        t += coef[o + j] * x[cols[o + j]];
                    t *= Weights[r];
                    for (int j = 0; j < 12; j++)
                        y[cols[o + j]] += coef[o + j] * t;
                }
                foreach ((int a, int b) in edges)
                {
                    double d = spatial * (x[a] - x[b]);
                    y[a] += d;
                    y[b] -= d;
                }
            }

            double[] Diagonal()
            {
                double[] d = new double[n];
                for (int i = 0; i < n; i++)
                    d[i] = ridge[i];
                for (int r = 0; r < rows; r++)
                {
                    int o = r * 12;
                    for (int j = 0; j < 12; j++)
                        d[cols[o + j]] += Weights[r] * coef[o + j] * coef[o + j];
                }
                foreach ((int a, int b) in edges)
                {
                    d[a] += spatial;
                    d[b] += spatial;
                }
                return d;
            }

            /// <summary>Solves for one component in place, warm-started from <paramref name="x"/>; returns iterations.</summary>
            public int Solve(double[] rhsRows, double[] x, int maxIterations, double tolerance)
            {
                double[] b = new double[n];
                for (int r = 0; r < rows; r++)
                {
                    int o = r * 12;
                    double t = Weights[r] * rhsRows[r];
                    for (int j = 0; j < 12; j++)
                        b[cols[o + j]] += coef[o + j] * t;
                }
                double[] diag = Diagonal();
                double[] ax = new double[n], rr = new double[n], z = new double[n], p = new double[n], ap = new double[n];
                Apply(x, ax);
                double bnorm = 0, rz = 0;
                for (int i = 0; i < n; i++)
                {
                    rr[i] = b[i] - ax[i];
                    z[i] = rr[i] / diag[i];
                    p[i] = z[i];
                    rz += rr[i] * z[i];
                    bnorm += b[i] * b[i];
                }
                bnorm = Math.Sqrt(bnorm);
                if (bnorm == 0)
                    return 0;
                for (int k = 1; k <= maxIterations; k++)
                {
                    Apply(p, ap);
                    double pap = 0;
                    for (int i = 0; i < n; i++)
                        pap += p[i] * ap[i];
                    if (pap <= 0)
                        return k;
                    double alpha = rz / pap;
                    double rnorm = 0, rzNew = 0;
                    for (int i = 0; i < n; i++)
                    {
                        x[i] += alpha * p[i];
                        rr[i] -= alpha * ap[i];
                        rnorm += rr[i] * rr[i];
                    }
                    if (Math.Sqrt(rnorm) <= tolerance * bnorm)
                        return k;
                    for (int i = 0; i < n; i++)
                    {
                        z[i] = rr[i] / diag[i];
                        rzNew += rr[i] * z[i];
                    }
                    double beta = rzNew / rz;
                    rz = rzNew;
                    for (int i = 0; i < n; i++)
                        p[i] = z[i] + beta * p[i];
                }
                return maxIterations;
            }
        }
    }
}
