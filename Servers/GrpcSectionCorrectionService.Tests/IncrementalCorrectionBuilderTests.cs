using AnnotationVizLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// The incremental update must write, for every section it recomputes, exactly what a full rebuild writes, and must
    /// recompute every section a full rebuild would change. Checked by editing synthetic structures and comparing.
    /// </summary>
    [TestClass]
    public class IncrementalCorrectionBuilderTests
    {
        const int MinLocations = 3;
        static readonly DateTime Edited = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        static MorphologyGraph.ResidualWindowOptions Window => MorphologyGraph.ResidualWindowOptions.Default;

        static void AssertSameSections(
            Dictionary<long, SectionVectorField> expected,
            Dictionary<long, SectionVectorField> actual,
            string context)
        {
            CollectionAssert.AreEquivalent(expected.Keys.ToList(), actual.Keys.ToList(), $"{context}: sections with data differ");
            foreach (long z in expected.Keys)
            {
                Dictionary<(int, int), LatticeNode> e = expected[z].Nodes.ToDictionary(n => (n.Gx, n.Gy));
                Dictionary<(int, int), LatticeNode> a = actual[z].Nodes.ToDictionary(n => (n.Gx, n.Gy));
                CollectionAssert.AreEquivalent(e.Keys.ToList(), a.Keys.ToList(), $"{context}: lattice squares differ in section {z}");
                foreach (KeyValuePair<(int, int), LatticeNode> kv in e)
                {
                    LatticeNode other = a[kv.Key];
                    Assert.AreEqual(kv.Value.VoteCount, other.VoteCount, $"{context}: votes at section {z} {kv.Key}");
                    Assert.AreEqual(kv.Value.Dx, other.Dx, 1e-6, $"{context}: dx at section {z} {kv.Key}");
                    Assert.AreEqual(kv.Value.Dy, other.Dy, 1e-6, $"{context}: dy at section {z} {kv.Key}");
                }
            }
        }

        /// <summary>Applies <paramref name="edit"/> to a copy of the rows and checks the incremental result against full builds.</summary>
        static async Task<IncrementalResult> CheckAsync(
            InMemoryMorphologySource before,
            Action<InMemoryMorphologySource, Random, HashSet<long>> edit,
            int seed,
            string context)
        {
            InMemoryMorphologySource after = before.Clone();
            HashSet<long> forcedDirty = [];
            edit(after, new Random(seed), forcedDirty);

            SortedSet<long> dirty = SectionFingerprintDiff.DirtySections(
                await before.LoadFingerprintsAsync(CancellationToken.None),
                await after.LoadFingerprintsAsync(CancellationToken.None));
            dirty.UnionWith(forcedDirty);
            Assert.IsTrue(dirty.Count > 0, $"{context}: the edit must change a fingerprint");

            IncrementalResult result = await IncrementalCorrectionBuilder.BuildAsync(
                after, null, InMemoryMorphologySource.Scale, "None", dirty, MinLocations, Window, int.MaxValue, null, CancellationToken.None);
            Assert.IsNotNull(result);

            Dictionary<long, SectionVectorField> merged = before.FullBuild(MinLocations)
                .Where(kv => !result.Affected.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (KeyValuePair<long, SectionVectorField> kv in result.Sections)
                merged[kv.Key] = kv.Value;

            AssertSameSections(after.FullBuild(MinLocations), merged, context);
            return result;
        }

        static SqlMorphologyLoader.CandidateRow Interior(InMemoryMorphologySource source, Random rng)
        {
            List<SqlMorphologyLoader.CandidateRow> large = [.. source.Rows
                .GroupBy(r => r.ParentID).Where(g => g.Count() > 12).SelectMany(g => g.OrderBy(r => r.Z).Skip(4).SkipLast(4))];
            return large[rng.Next(large.Count)];
        }

        [TestMethod]
        public async Task FullBuild_OfTheSyntheticData_ProducesLatticeNodes()
        {
            Dictionary<long, SectionVectorField> full = InMemoryMorphologySource.Generate(1).FullBuild(MinLocations);
            Assert.IsTrue(full.Count > 20, "the synthetic structures must reach the lattice's minimum votes, or the tests prove nothing");
            Assert.IsTrue(full.Values.All(s => s.NodeCount > 0));
            await Task.CompletedTask;
        }

        [TestMethod]
        public async Task MovedLocation_RecomputesOnlyNearbySections_AndMatchesFullBuild()
        {
            for (int seed = 1; seed <= 12; seed++)
            {
                InMemoryMorphologySource before = InMemoryMorphologySource.Generate(seed);
                IncrementalResult result = await CheckAsync(before, (source, rng, _) =>
                {
                    SqlMorphologyLoader.CandidateRow row = Interior(source, rng);
                    row.X += 150 + rng.NextDouble() * 300;
                    row.Y -= 100 + rng.NextDouble() * 200;
                    row.LastModified = Edited;
                }, seed, $"move seed {seed}");

                int total = before.FullBuild(MinLocations).Count;
                Assert.IsTrue(result.Affected.Count < total, $"seed {seed}: one move affected {result.Affected.Count} of {total} sections");
            }
        }

        [TestMethod]
        public async Task DeletedLocation_WithAndWithoutRelink_MatchesFullBuild()
        {
            foreach (bool relink in new[] { true, false })
            {
                for (int seed = 1; seed <= 12; seed++)
                {
                    await CheckAsync(InMemoryMorphologySource.Generate(seed), (source, rng, _) =>
                    {
                        SqlMorphologyLoader.CandidateRow row = Interior(source, rng);
                        SqlMorphologyLoader.LinkRow before = source.Links.Single(l => l.B == row.ID);
                        SqlMorphologyLoader.LinkRow after = source.Links.Single(l => l.A == row.ID);
                        source.Rows.Remove(row);
                        source.Links.Remove(before);
                        source.Links.Remove(after);
                        if (relink)
                            source.Links.Add(new SqlMorphologyLoader.LinkRow { A = before.A, B = after.B, Created = Edited });
                    }, seed, $"delete relink={relink} seed {seed}");
                }
            }
        }

        [TestMethod]
        public async Task LocationInsertedIntoAGap_MatchesFullBuild()
        {
            int checkedCases = 0;
            for (int seed = 1; seed <= 20 && checkedCases < 8; seed++)
            {
                InMemoryMorphologySource before = InMemoryMorphologySource.Generate(seed);
                if (!HasGap(before))
                    continue;
                checkedCases++;
                await CheckAsync(before, (source, rng, _) =>
                {
                    Dictionary<long, SqlMorphologyLoader.CandidateRow> byId = source.Rows.ToDictionary(r => r.ID);
                    List<SqlMorphologyLoader.LinkRow> gaps = [.. source.Links.Where(l => byId[l.B].Z - byId[l.A].Z > 1)];
                    SqlMorphologyLoader.LinkRow link = gaps[rng.Next(gaps.Count)];
                    SqlMorphologyLoader.CandidateRow a = byId[link.A];
                    SqlMorphologyLoader.CandidateRow b = byId[link.B];
                    long id = 900000 + seed;
                    source.Rows.Add(new SqlMorphologyLoader.CandidateRow
                    {
                        ID = id, ParentID = a.ParentID, Z = a.Z + 1, X = (a.X + b.X) / 2 + 200, Y = (a.Y + b.Y) / 2 - 150,
                        TypeCode = 1, LastModified = Edited
                    });
                    source.Links.Remove(link);
                    source.Links.Add(new SqlMorphologyLoader.LinkRow { A = a.ID, B = id, Created = Edited });
                    source.Links.Add(new SqlMorphologyLoader.LinkRow { A = id, B = b.ID, Created = Edited });
                }, seed, $"insert seed {seed}");
            }

            Assert.IsTrue(checkedCases >= 4, "the synthetic chains must contain gaps");
        }

        static bool HasGap(InMemoryMorphologySource source)
        {
            Dictionary<long, SqlMorphologyLoader.CandidateRow> byId = source.Rows.ToDictionary(r => r.ID);
            return source.Links.Any(l => byId[l.B].Z - byId[l.A].Z > 1);
        }

        [TestMethod]
        public async Task RestampedSection_WhoseStosChanged_MatchesFullBuild()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                await CheckAsync(InMemoryMorphologySource.Generate(seed), (source, rng, forcedDirty) =>
                {
                    long z = Interior(source, rng).Z;
                    foreach (SqlMorphologyLoader.CandidateRow row in source.Rows.Where(r => r.Z == z))
                        row.X += 250;
                    // A new stos moves positions without touching any row, so only the stos fingerprint notices.
                    forcedDirty.Add(z);
                }, seed, $"stos seed {seed}");
            }
        }

        [TestMethod]
        public async Task StructureShrunkBelowTheMinimumSize_MatchesFullBuild()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                await CheckAsync(InMemoryMorphologySource.Generate(seed), (source, rng, _) =>
                {
                    // 3 locations -> 2: the structure drops out of the build, taking its residuals with it.
                    SqlMorphologyLoader.CandidateRow row = source.Rows.Where(r => r.ParentID == 5001).OrderBy(r => r.Z).Last();
                    source.Rows.Remove(row);
                    source.Links.RemoveAll(l => l.A == row.ID || l.B == row.ID);
                }, seed, $"shrink seed {seed}");
            }
        }

        [TestMethod]
        public async Task StructureGrownToTheMinimumSize_MatchesFullBuild()
        {
            for (int seed = 1; seed <= 6; seed++)
            {
                await CheckAsync(InMemoryMorphologySource.Generate(seed), (source, rng, _) =>
                {
                    // 2 locations -> 3: the structure enters the build.
                    SqlMorphologyLoader.CandidateRow last = source.Rows.Where(r => r.ParentID == 5000).OrderBy(r => r.Z).Last();
                    long id = 900500 + seed;
                    source.Rows.Add(new SqlMorphologyLoader.CandidateRow
                    {
                        ID = id, ParentID = 5000, Z = last.Z + 1, X = last.X + 30, Y = last.Y - 20, TypeCode = 1, LastModified = Edited
                    });
                    source.Links.Add(new SqlMorphologyLoader.LinkRow { A = last.ID, B = id, Created = Edited });
                }, seed, $"grow seed {seed}");
            }
        }

        [TestMethod]
        public async Task SeveralEditsAtOnce_MatchFullBuild()
        {
            for (int seed = 1; seed <= 10; seed++)
            {
                await CheckAsync(InMemoryMorphologySource.Generate(seed), (source, rng, _) =>
                {
                    for (int k = 0; k < 4; k++)
                    {
                        SqlMorphologyLoader.CandidateRow row = Interior(source, rng);
                        row.X += (rng.NextDouble() - 0.5) * 800;
                        row.Y += (rng.NextDouble() - 0.5) * 800;
                        row.LastModified = Edited;
                    }
                }, seed, $"several seed {seed}");
            }
        }

        [TestMethod]
        public async Task TooManyAffectedSections_ReturnsNull()
        {
            InMemoryMorphologySource before = InMemoryMorphologySource.Generate(3);
            InMemoryMorphologySource after = before.Clone();
            SqlMorphologyLoader.CandidateRow row = Interior(after, new Random(3));
            row.X += 400;
            row.LastModified = Edited;
            SortedSet<long> dirty = SectionFingerprintDiff.DirtySections(
                await before.LoadFingerprintsAsync(CancellationToken.None),
                await after.LoadFingerprintsAsync(CancellationToken.None));

            IncrementalResult result = await IncrementalCorrectionBuilder.BuildAsync(
                after, null, InMemoryMorphologySource.Scale, "None", dirty, MinLocations, Window, maxAffected: 2, null, CancellationToken.None);

            Assert.IsNull(result);
        }
    }
}
