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
        public static List<ProcessTriple> CollectTriples(IEnumerable<MorphologyGraph> cells, int maxSectionGap = 2)
        {
            List<ProcessTriple> triples = [];
            foreach (MorphologyGraph cell in cells)
            {
                if (cell is null || cell.StructureID == 0)
                    continue;
                foreach (ulong[] process in cell.Processes())
                {
                    if (process.Length < 3)
                        continue;
                    MorphologyNode[] nodes = [.. process.Select(id => cell.Nodes[id])];
                    for (int i = 1; i + 1 < nodes.Length; i++)
                    {
                        MorphologyNode a = nodes[i - 1], b = nodes[i], c = nodes[i + 1];
                        int za = (int)Math.Round(a.UnscaledZ), zb = (int)Math.Round(b.UnscaledZ), zc = (int)Math.Round(c.UnscaledZ);
                        if (zb - za <= 0 || zb - za > maxSectionGap || zc - zb <= 0 || zc - zb > maxSectionGap)
                            continue;
                        double alpha = (b.Z - a.Z) / (c.Z - a.Z);
                        triples.Add(new ProcessTriple(cell.StructureID, za, a.Center.XY(), zb, b.Center.XY(), zc, c.Center.XY(), alpha));
                    }
                }
            }
            return triples;
        }

        /// <summary>True for structures held out of the ridge-selection fit.</summary>
        public static bool IsHeldOut(ulong structureId, int percent) => (structureId * 2654435761UL) % 100UL < (ulong)percent;

        public static JointRegistrationResult Solve(IReadOnlyList<MorphologyGraph> cells, JointRegistrationOptions options = null, Action<string> log = null)
            => Solve(CollectTriples(cells, (options ?? new JointRegistrationOptions()).MaxSectionGap), options, log);

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

        /// <summary>One robust least-squares fit; returns the published lattice as a field.</summary>
        public static NeighborResidualField Fit(IReadOnlyList<ProcessTriple> triples, double spatial, double ridge, int pin, JointRegistrationOptions options, Action<string> log = null)
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

            for (int r = 0; r < R; r++)
            {
                ProcessTriple t = triples[r];
                Corners(r, 0, t.ZA, t.A, -(1 - t.Alpha));
                Corners(r, 1, t.ZB, t.B, 1.0);
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
