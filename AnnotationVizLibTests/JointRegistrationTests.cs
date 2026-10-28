using AnnotationVizLib;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;

namespace AnnotationVizLibTests
{
    /// <summary>
    /// <see cref="JointRegistrationSolver"/> on synthetic volumes where EVERY section carries a registration error
    /// (the case the leave-one-out residual field got wrong), plus an opt-in check on real exported positions.
    /// Scores are always taken on structures held out of the fit.
    /// </summary>
    [TestClass]
    public class JointRegistrationTests
    {
        const double SectionZ = 90.0;
        static IScale TestScale => new Scale(new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(SectionZ, "nm"));

        /// <summary>Synthetic volume: straight oblique processes with wiggle and placement noise, plus a smooth registration shift per section.</summary>
        sealed class Synthetic
        {
            public List<MorphologyGraph> Cells = [];
            public Dictionary<int, (double A, double Bx, double By, double Ca, double Cbx, double Cby)> Shift = [];
            public const double Extent = 60000;

            public Vector2 TrueShift(int z, Vector2 p)
            {
                var s = Shift[z];
                return new Vector2(s.A + s.Bx * p.X / Extent + s.By * p.Y / Extent, s.Ca + s.Cbx * p.X / Extent + s.Cby * p.Y / Extent);
            }

            /// <param name="damage">Extra displacement of an annotation at (section, true position), e.g. a tear on one section.</param>
            public static Synthetic Build(int seed, double registrationSdNm, int sections = 40, int processes = 700,
                Func<int, Vector2, Vector2> damage = null)
            {
                Random rng = new(seed);
                double Gauss() => Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())) * Math.Cos(2 * Math.PI * rng.NextDouble());
                Synthetic s = new();
                for (int z = 1; z <= sections; z++)
                    s.Shift[z] = (registrationSdNm * Gauss(), 0.25 * registrationSdNm * Gauss(), 0.25 * registrationSdNm * Gauss(),
                                  registrationSdNm * Gauss(), 0.25 * registrationSdNm * Gauss(), 0.25 * registrationSdNm * Gauss());
                ulong id = 1;
                for (int k = 0; k < processes; k++)
                {
                    ulong sid = 1000 + (ulong)k;
                    MorphologyGraph g = new(sid, TestScale);
                    double x0 = rng.NextDouble() * Extent, y0 = rng.NextDouble() * Extent;
                    double vx = (rng.NextDouble() - 0.5) * 120, vy = (rng.NextDouble() - 0.5) * 120;
                    double amp = 30 * rng.NextDouble(), phase = 2 * Math.PI * rng.NextDouble();
                    int start = 1 + rng.Next(sections / 4), end = sections - rng.Next(sections / 4);
                    ulong first = id;
                    for (int z = start; z <= end; z++)
                    {
                        Vector2 p = new(x0 + vx * z + amp * Math.Sin(phase + z * 0.6) + 15 * Gauss(), y0 + vy * z + amp * Math.Cos(phase + z * 0.6) + 15 * Gauss());
                        p += s.TrueShift(z, p) + (damage?.Invoke(z, p) ?? Vector2.Zero);
                        g.AddNode(new MorphologyNode(id, Point(id, sid, p, z), g));
                        id++;
                    }
                    for (ulong i = first; i + 1 < id; i++)
                        g.AddEdge(new MorphologyEdge(g, i, i + 1));
                    s.Cells.Add(g);
                }
                return s;
            }
        }

        static List<ProcessTriple> HeldOutTriples(IEnumerable<MorphologyGraph> cells) =>
            JointRegistrationSolver.CollectTriples(cells.Where(c => JointRegistrationSolver.IsHeldOut(c.StructureID, 15)));

        static double Median(IEnumerable<double> v)
        {
            double[] a = [.. v.OrderBy(x => x)];
            return a.Length == 0 ? double.NaN : a[a.Length / 2];
        }

        static NeighborResidualField LegacyFieldFromTraining(IEnumerable<MorphologyGraph> cells) =>
            NeighborResidualField.Build(NeighborResidualField.CollectResiduals(cells.Where(c => !JointRegistrationSolver.IsHeldOut(c.StructureID, 15))));

        [TestMethod]
        public void EverySectionJittered_HeldOutStructuresBecomeSmooth()
        {
            Synthetic s = Synthetic.Build(seed: 7, registrationSdNm: 150);
            JointRegistrationResult result = JointRegistrationSolver.Solve(s.Cells, null, msg => Console.WriteLine(msg));
            Assert.IsFalse(result.Identity, "A volume with 150 nm registration error per section must get a correction");
            Assert.IsNotNull(result.SelectionField);

            List<ProcessTriple> held = HeldOutTriples(s.Cells);
            double raw = Median(held.Select(t => t.SecondDifference.Magnitude));
            double corrected = JointRegistrationSolver.HeldOutMedian(result.SelectionField, held);
            // Floor: the same triples with the true registration shift removed (wiggle + placement noise only).
            double floor = Median(held.Select(t =>
            {
                Vector2 a = t.A - s.TrueShift(t.ZA, t.A), b = t.B - s.TrueShift(t.ZB, t.B), c = t.C - s.TrueShift(t.ZC, t.C);
                return (b - a * (1 - t.Alpha) - c * t.Alpha).Magnitude;
            }));
            double legacy = JointRegistrationSolver.HeldOutMedian(LegacyFieldFromTraining(s.Cells), held);
            Console.WriteLine($"held-out second difference median: raw {raw:F1}, joint {corrected:F1}, legacy {legacy:F1}, floor {floor:F1} nm");

            Assert.IsTrue(corrected < 0.5 * raw, $"joint {corrected:F1} should at least halve raw {raw:F1}");
            Assert.IsTrue(corrected < 1.6 * floor, $"joint {corrected:F1} should approach the noise floor {floor:F1}");
            Assert.IsTrue(corrected < 0.8 * legacy, $"joint {corrected:F1} should beat the leave-one-out residual field {legacy:F1}");
        }

        [TestMethod]
        public void EverySectionJittered_SectionToSectionStepsMatchTruth()
        {
            // Carrying an annotation to the next section needs e(z+1) - e(z), not e(z) itself.
            Synthetic s = Synthetic.Build(seed: 11, registrationSdNm: 150);
            JointRegistrationResult result = JointRegistrationSolver.Solve(s.Cells);
            Random rng = new(3);
            List<double> est = [], truth = [];
            for (int i = 0; i < 400; i++)
            {
                int z = 12 + rng.Next(16);
                Vector2 p = new(10000 + rng.NextDouble() * 40000, 10000 + rng.NextDouble() * 40000);
                Vector2? f0 = result.Field.Sample(p, z), f1 = result.Field.Sample(p, z + 1);
                if (f0 is null || f1 is null)
                    continue;
                est.Add(f1.Value.X - f0.Value.X);
                truth.Add(-(s.TrueShift(z + 1, p).X - s.TrueShift(z, p).X));
            }
            Assert.IsTrue(est.Count > 300, $"field should cover the interior ({est.Count} samples)");
            double r = Correlation(est, truth);
            Console.WriteLine($"correlation of estimated and true section-to-section steps: {r:F3}");
            Assert.IsTrue(r > 0.9, $"steps correlation {r:F3}");
        }

        [TestMethod]
        public void NoRegistrationError_DoesNoHarm()
        {
            Synthetic s = Synthetic.Build(seed: 5, registrationSdNm: 0);
            JointRegistrationResult result = JointRegistrationSolver.Solve(s.Cells, null, msg => Console.WriteLine(msg));
            List<ProcessTriple> held = HeldOutTriples(s.Cells);
            double raw = Median(held.Select(t => t.SecondDifference.Magnitude));
            double corrected = result.SelectionField is null ? raw : JointRegistrationSolver.HeldOutMedian(result.SelectionField, held);
            Console.WriteLine($"identity {result.Identity}, held-out raw {raw:F1}, corrected {corrected:F1}");
            Assert.IsTrue(result.Identity || corrected <= 1.02 * raw, $"no registration error: corrected {corrected:F1} vs raw {raw:F1}");
        }

        /// <summary>One straight process, one annotation per section from <paramref name="firstSection"/> to <paramref name="lastSection"/>.</summary>
        static MorphologyGraph Straight(ulong sid, int firstSection, int lastSection)
        {
            MorphologyGraph g = new(sid, TestScale);
            ulong id = sid * 1000;
            for (int z = firstSection; z <= lastSection; z++)
                g.AddNode(new MorphologyNode(id + (ulong)z, Point(id + (ulong)z, sid, new Vector2(1000 + 10 * z, 2000), z), g));
            for (int z = firstSection; z < lastSection; z++)
                g.AddEdge(new MorphologyEdge(g, id + (ulong)z, id + (ulong)z + 1));
            return g;
        }

        [TestMethod]
        public void DamagedSection_IsSkippedByTheMainTriples_AndCentresItsOwn()
        {
            List<MorphologyGraph> cells = [Straight(7, 1, 6)];

            List<ProcessTriple> main = JointRegistrationSolver.CollectTriples(cells, 2, [3]);
            CollectionAssert.AreEqual(new[] { (1, 2, 4), (2, 4, 5), (4, 5, 6) }, main.Select(t => (t.ZA, t.ZB, t.ZC)).ToArray(),
                "no main triple has a point on the damaged section; the sections either side are linked directly");

            List<ProcessTriple> own = JointRegistrationSolver.CollectDamagedTriples(cells, [3]);
            CollectionAssert.AreEqual(new[] { (2, 3, 4) }, own.Select(t => (t.ZA, t.ZB, t.ZC)).ToArray());
            Assert.AreEqual(0.5, own[0].Alpha, 1e-12);
        }

        [TestMethod]
        public void DamagedRun_IsBridged_AndSkippedSectionsDoNotCountTowardTheGap()
        {
            List<MorphologyGraph> cells = [Straight(8, 1, 7)];

            List<ProcessTriple> main = JointRegistrationSolver.CollectTriples(cells, 2, [3, 4]);
            CollectionAssert.AreEqual(new[] { (1, 2, 5), (2, 5, 6), (5, 6, 7) }, main.Select(t => (t.ZA, t.ZB, t.ZC)).ToArray());

            List<ProcessTriple> own = JointRegistrationSolver.CollectDamagedTriples(cells, [3, 4]);
            CollectionAssert.AreEqual(new[] { (2, 3, 5), (2, 4, 5) }, own.Select(t => (t.ZA, t.ZB, t.ZC)).ToArray(),
                "each damaged section is fitted against the nearest undamaged sections, never against another damaged one");
        }

        [TestMethod]
        public void NoDamagedList_TriplesAreUnchanged()
        {
            List<MorphologyGraph> cells = [Straight(9, 1, 6)];
            CollectionAssert.AreEqual(
                new[] { (1, 2, 3), (2, 3, 4), (3, 4, 5), (4, 5, 6) },
                JointRegistrationSolver.CollectTriples(cells).Select(t => (t.ZA, t.ZB, t.ZC)).ToArray());
            Assert.AreEqual(0, JointRegistrationSolver.CollectDamagedTriples(cells, []).Count);
        }

        /// <summary>
        /// Section 20 is torn: on its right half every annotation is displaced 1.5 µm on top of the registration error.
        /// Told it is damaged, the solver must leave its neighbours as smooth as an undamaged volume would, and must still
        /// shift section 20 toward its neighbours.
        /// </summary>
        [TestMethod]
        public void TornSection_DoesNotPullItsNeighbours_AndGetsItsOwnShift()
        {
            const int Torn = 20;
            static Vector2 Tear(int z, Vector2 p) => z == Torn && p.X > Synthetic.Extent / 2 ? new Vector2(1500, -900) : Vector2.Zero;
            Synthetic s = Synthetic.Build(seed: 13, registrationSdNm: 150, damage: Tear);

            JointRegistrationResult told = JointRegistrationSolver.Solve(s.Cells,
                new JointRegistrationOptions { DamagedSections = [Torn] }, msg => Console.WriteLine($"told: {msg}"));
            JointRegistrationResult blind = JointRegistrationSolver.Solve(s.Cells, null, msg => Console.WriteLine($"blind: {msg}"));

            // Smoothness of the torn section's neighbours: triples centred on 18, 19, 21 and 22 that bridge the tear.
            List<ProcessTriple> neighbours = [.. JointRegistrationSolver.CollectTriples(s.Cells, 2, [Torn])
                .Where(t => t.ZB is Torn - 2 or Torn - 1 or Torn + 1 or Torn + 2)];
            double raw = Median(neighbours.Select(t => t.SecondDifference.Magnitude));
            double toldNeighbours = JointRegistrationSolver.HeldOutMedian(told.Field, neighbours);
            double blindNeighbours = JointRegistrationSolver.HeldOutMedian(blind.Field, neighbours);

            // The torn section against its neighbours.
            List<ProcessTriple> centred = JointRegistrationSolver.CollectDamagedTriples(s.Cells, [Torn]);
            double tornRaw = Median(centred.Select(t => t.SecondDifference.Magnitude));
            double toldTorn = JointRegistrationSolver.HeldOutMedian(told.Field, centred);
            double blindTorn = JointRegistrationSolver.HeldOutMedian(blind.Field, centred);
            Console.WriteLine($"neighbours: raw {raw:F1}, told {toldNeighbours:F1}, blind {blindNeighbours:F1} nm; " +
                              $"torn section: raw {tornRaw:F1}, told {toldTorn:F1}, blind {blindTorn:F1} nm");

            CollectionAssert.AreEqual(new[] { Torn }, told.DamagedSections.ToArray());
            Assert.IsTrue(told.DamagedTriples > 100, $"{told.DamagedTriples} triples centred on the torn section");
            Assert.IsTrue(toldNeighbours <= blindNeighbours, $"told {toldNeighbours:F1} must not leave the neighbours rougher than blind {blindNeighbours:F1}");
            Assert.IsTrue(toldNeighbours < 0.6 * raw, $"told {toldNeighbours:F1} should still correct the neighbours (raw {raw:F1})");
            Assert.IsTrue(toldTorn < 0.5 * tornRaw, $"the torn section's own shift {toldTorn:F1} should at least halve its misfit {tornRaw:F1}");
        }

        static double Correlation(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            double ma = a.Average(), mb = b.Average();
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < a.Count; i++)
            {
                sab += (a[i] - ma) * (b[i] - mb);
                saa += (a[i] - ma) * (a[i] - ma);
                sbb += (b[i] - mb) * (b[i] - mb);
            }
            return sab / Math.Sqrt(saa * sbb);
        }

        static TestPoint Point(ulong id, ulong parentId, Vector2 p, int section) => new()
        {
            ID = id,
            ParentID = parentId,
            UnscaledZ = section,
            Z = section * SectionZ,
            TypeCode = LocationType.CIRCLE,
            VolumeGeometryWKT = string.Format(CultureInfo.InvariantCulture, "POINT({0} {1})", p.X, p.Y)
        };

        sealed class TestPoint : ILocationReadOnly
        {
            public ulong ID { get; init; }
            public ulong ParentID { get; init; }
            public bool Terminal { get; init; }
            public bool OffEdge { get; init; }
            public bool IsVericosityCap { get; init; }
            public bool IsUntraceable { get; init; }
            public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>();
            public long UnscaledZ { get; init; }
            public LocationType TypeCode { get; init; }
            public double Z { get; init; }
            public double? Width { get; init; }
            public string MosaicGeometryWKT { get; init; }
            public string VolumeGeometryWKT { get; init; }

            public bool Equals(ILocationReadOnly other) => other != null && ID == other.ID;
        }

        /// <summary>
        /// Opt-in check on real volumes. Set JOINT_REG_SVCPOS to one or more ';'-separated JSON files of service volume
        /// positions ({"pos": {"locationId": [structureId, z, xNm, yNm]}, "links": [[a, b], ...]}, as written by
        /// CorrectStructures(NONE)). Prints held-out carry and smoothness for uncorrected, the leave-one-out residual
        /// field, and the joint fit; asserts the joint fit is no worse than uncorrected on held-out smoothness.
        /// </summary>
        [TestMethod]
        public void RealVolumes_HeldOutScores()
        {
            string files = Environment.GetEnvironmentVariable("JOINT_REG_SVCPOS");
            if (string.IsNullOrWhiteSpace(files))
                Assert.Inconclusive("Set JOINT_REG_SVCPOS to run against exported volume positions.");
            foreach (string file in files.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                List<MorphologyGraph> cells = LoadPositions(file, out List<(ulong A, ulong B)> links, out Dictionary<ulong, (ulong Sid, int Z, Vector2 P)> pos);
                string name = System.IO.Path.GetFileNameWithoutExtension(file);
                DateTime t0 = DateTime.UtcNow;
                // Optional fixed candidates (comma lists) to skip the full grid, e.g. JOINT_REG_SPATIAL=30 JOINT_REG_RIDGE=0.01
                JointRegistrationOptions opts = new();
                static double[] Parse(string s) => [.. s.Split(',').Select(v => double.Parse(v, CultureInfo.InvariantCulture))];
                string sp = Environment.GetEnvironmentVariable("JOINT_REG_SPATIAL"), rg = Environment.GetEnvironmentVariable("JOINT_REG_RIDGE");
                if (!string.IsNullOrWhiteSpace(sp))
                    opts.SpatialCandidates = Parse(sp);
                if (!string.IsNullOrWhiteSpace(rg))
                    opts.RidgeCandidates = Parse(rg);
                string pt = Environment.GetEnvironmentVariable("JOINT_REG_PITCH");
                if (!string.IsNullOrWhiteSpace(pt))
                    opts.PitchNm = double.Parse(pt, CultureInfo.InvariantCulture);
                JointRegistrationResult result = JointRegistrationSolver.Solve(cells, opts, msg => Console.WriteLine($"{name}: {msg}"));
                double seconds = (DateTime.UtcNow - t0).TotalSeconds;
                NeighborResidualField legacy = LegacyFieldFromTraining(cells);
                NeighborResidualField joint = result.SelectionField ?? result.Field;
                List<ProcessTriple> held = HeldOutTriples(cells);
                List<(Vector2 S, int Zs, Vector2 T, int Zt)> pairs = [.. links
                    .Where(l => pos[l.A].Sid == pos[l.B].Sid && JointRegistrationSolver.IsHeldOut(pos[l.A].Sid, 15) && Math.Abs(pos[l.A].Z - pos[l.B].Z) == 1)
                    .Select(l => (pos[l.A].P, pos[l.A].Z, pos[l.B].P, pos[l.B].Z))];
                double Carry(NeighborResidualField f) => Median(pairs.Select(p =>
                {
                    Vector2 fs = f?.Sample(p.S, p.Zs) ?? Vector2.Zero, ft = f?.Sample(p.S, p.Zt) ?? Vector2.Zero;
                    return (p.S + fs - ft - p.T).Magnitude;
                }));
                double Smooth(NeighborResidualField f) => f is null ? Median(held.Select(t => t.SecondDifference.Magnitude)) : JointRegistrationSolver.HeldOutMedian(f, held);
                string line = $"{name}: {seconds:F0}s pitch {opts.PitchNm} spatial {result.ChosenSpatial} ridge {result.ChosenRidge} identity {result.Identity} | held-out carry median nm: raw {Carry(null):F1} legacy {Carry(legacy):F1} joint {Carry(joint):F1}" +
                              $" | held-out second difference median nm: raw {Smooth(null):F1} legacy {Smooth(legacy):F1} joint {Smooth(joint):F1}";
                line += $" | shared (fixable) part after correction, <3 um: raw {Shared(held, null)} legacy {Shared(held, legacy)} joint {Shared(held, joint)}";
                Console.WriteLine(line);
                string report = Environment.GetEnvironmentVariable("JOINT_REG_REPORT");
                if (!string.IsNullOrWhiteSpace(report))
                    File.AppendAllText(report, line + Environment.NewLine);
                // Optional: per-link carry errors of the held-out structures (source, target, raw and joint error vectors, nm).
                string carryDir = Environment.GetEnvironmentVariable("JOINT_REG_CARRY_DIR");
                if (!string.IsNullOrWhiteSpace(carryDir))
                {
                    using StreamWriter w = new(System.IO.Path.Combine(carryDir, name.Replace("_svcpos", "") + "_joint_carry.csv"));
                    w.WriteLine("source,target,zs,zt,ex_raw,ey_raw,ex_joint,ey_joint");
                    foreach ((ulong a, ulong b) in links)
                    {
                        var pa = pos[a];
                        var pb = pos[b];
                        if (pa.Sid != pb.Sid || !JointRegistrationSolver.IsHeldOut(pa.Sid, 15) || Math.Abs(pa.Z - pb.Z) > 2)
                            continue;
                        for (int dir = 0; dir < 2; dir++)
                        {
                            ulong sId = dir == 0 ? a : b, tId = dir == 0 ? b : a;
                            var s = dir == 0 ? pa : pb;
                            var t = dir == 0 ? pb : pa;
                            Vector2 raw = s.P - t.P;
                            Vector2 fs = joint.Sample(s.P, s.Z) ?? Vector2.Zero, ft = joint.Sample(s.P, t.Z) ?? Vector2.Zero;
                            Vector2 e = s.P + fs - ft - t.P;
                            w.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4:F1},{5:F1},{6:F1},{7:F1}",
                                sId, tId, s.Z, t.Z, raw.X, raw.Y, e.X, e.Y));
                        }
                    }
                }
                Assert.IsTrue(Smooth(joint) <= Smooth(null) * 1.01, line);
            }
        }

        /// <summary>
        /// How much of the corrected second difference is still shared by different structures within 3 um on the
        /// same section (registration the field missed) versus private to each process (wiggle and placement noise,
        /// which no field can remove). Components clipped at 500 nm. Returns "sharedSd nm (fraction of variance)".
        /// </summary>
        static string Shared(IReadOnlyList<ProcessTriple> held, NeighborResidualField f) =>
            $"{SharedBin(held, f, 0, 3000)}, 3-10 um {SharedBin(held, f, 3000, 10000)}, 10-30 um {SharedBin(held, f, 10000, 30000)}";

        static string SharedBin(IReadOnlyList<ProcessTriple> held, NeighborResidualField f, double minDist, double maxDist)
        {
            List<(ulong Sid, int Z, Vector2 P, Vector2 D)> v = [];
            foreach (ProcessTriple t in held)
            {
                Vector2 a = t.A + (f?.Sample(t.A, t.ZA) ?? Vector2.Zero);
                Vector2 b = t.B + (f?.Sample(t.B, t.ZB) ?? Vector2.Zero);
                Vector2 c = t.C + (f?.Sample(t.C, t.ZC) ?? Vector2.Zero);
                Vector2 d = b - a * (1 - t.Alpha) - c * t.Alpha;
                v.Add((t.StructureID, t.ZB, t.B, new Vector2(Math.Max(-500, Math.Min(500, d.X)), Math.Max(-500, Math.Min(500, d.Y)))));
            }
            double mx = v.Average(q => q.D.X), my = v.Average(q => q.D.Y);
            double var = v.Average(q => ((q.D.X - mx) * (q.D.X - mx) + (q.D.Y - my) * (q.D.Y - my)) / 2);
            double cov = 0;
            long n = 0;
            foreach (var section in v.GroupBy(q => q.Z))
            {
                var items = section.ToArray();
                Dictionary<(int, int), List<int>> grid = [];
                for (int i = 0; i < items.Length; i++)
                {
                    var key = ((int)Math.Floor(items[i].P.X / maxDist), (int)Math.Floor(items[i].P.Y / maxDist));
                    if (!grid.TryGetValue(key, out var list))
                        grid[key] = list = [];
                    list.Add(i);
                }
                for (int i = 0; i < items.Length; i++)
                {
                    int gx = (int)Math.Floor(items[i].P.X / maxDist), gy = (int)Math.Floor(items[i].P.Y / maxDist);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            if (grid.TryGetValue((gx + dx, gy + dy), out var list))
                                foreach (int j in list)
                                {
                                    if (j <= i || items[j].Sid == items[i].Sid || (items[j].P - items[i].P).Magnitude > maxDist || (items[j].P - items[i].P).Magnitude < minDist)
                                        continue;
                                    cov += ((items[i].D.X - mx) * (items[j].D.X - mx) + (items[i].D.Y - my) * (items[j].D.Y - my)) / 2;
                                    n++;
                                }
                }
            }
            if (n == 0)
                return "n/a";
            cov /= n;
            return string.Format(CultureInfo.InvariantCulture, "{0:F1} nm ({1:F2})", Math.Sqrt(Math.Max(0, cov)), cov / var);
        }

        static List<MorphologyGraph> LoadPositions(string file, out List<(ulong A, ulong B)> links, out Dictionary<ulong, (ulong Sid, int Z, Vector2 P)> pos)
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(file));
            pos = [];
            Dictionary<ulong, MorphologyGraph> cells = [];
            foreach (JsonProperty p in doc.RootElement.GetProperty("pos").EnumerateObject())
            {
                ulong id = ulong.Parse(p.Name, CultureInfo.InvariantCulture);
                JsonElement v = p.Value;
                ulong sid = v[0].GetUInt64();
                int z = (int)v[1].GetInt64();
                Vector2 xy = new(v[2].GetDouble(), v[3].GetDouble());
                pos[id] = (sid, z, xy);
                if (!cells.TryGetValue(sid, out MorphologyGraph g))
                    cells[sid] = g = new MorphologyGraph(sid, TestScale);
                g.AddNode(new MorphologyNode(id, Point(id, sid, xy, z), g));
            }
            links = [];
            HashSet<(ulong, ulong)> seen = [];
            foreach (JsonElement l in doc.RootElement.GetProperty("links").EnumerateArray())
            {
                ulong a = l[0].GetUInt64(), b = l[1].GetUInt64();
                if (a == b || !pos.TryGetValue(a, out var pa) || !pos.TryGetValue(b, out var pb) || pa.Sid != pb.Sid)
                    continue;
                if (!seen.Add((Math.Min(a, b), Math.Max(a, b))))
                    continue;
                cells[pa.Sid].AddEdge(new MorphologyEdge(cells[pa.Sid], a, b));
                links.Add((a, b));
            }
            return [.. cells.Values];
        }
    }
}
