using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Publishes to a real directory from in-memory rows (identity stos, no previews) to check what an incremental update
    /// leaves alone, when it falls back to a full rebuild, and that readers never see a half-updated set.
    /// </summary>
    [TestClass]
    public class CorrectionPublisherIncrementalTests
    {
        const int MinLocations = 3;
        string _root;
        string _dest;

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "correction-incremental-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            _dest = Path.Combine(_root, "None");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        CorrectionPublishOptions Options(Action<CorrectionPublishOptions> configure = null)
        {
            CorrectionPublishOptions options = new()
            {
                OutputDirectory = _root,
                SkipQuiver = true,
                MinLocations = MinLocations,
                Log = _ => { }
            };
            configure?.Invoke(options);
            return options;
        }

        static Task<CorrectionPublishResult> PublishAsync(InMemoryMorphologySource source, CorrectionPublishOptions options) =>
            CorrectionPublisher.PublishGroupAsync(
                options,
                null,
                InMemoryMorphologySource.Scale,
                "None",
                source,
                () => Task.FromResult(SqlMorphologyLoader.FromRows(
                    [.. source.Rows.GroupBy(r => r.ParentID).Where(g => g.Count() >= MinLocations).SelectMany(g => g)],
                    source.Links, null, InMemoryMorphologySource.Scale, "None")),
                CancellationToken.None);

        static void MoveInterior(InMemoryMorphologySource source, DateTime editedAt)
        {
            SqlMorphologyLoader.CandidateRow row = source.Rows
                .GroupBy(r => r.ParentID).Where(g => g.Count() > 20).First()
                .OrderBy(r => r.Z).Skip(20).First();
            row.X += 300;
            row.Y -= 200;
            row.LastModified = editedAt;
        }

        Dictionary<string, byte[]> SectionFiles() => Directory.GetFiles(_dest, "*.vfield.npz")
            .ToDictionary(Path.GetFileName, File.ReadAllBytes);

        [TestMethod]
        public async Task FirstPublish_IsFull_AndRecordsFingerprints()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsTrue(result.Full);
            Assert.IsFalse(result.Skipped);
            CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(_dest);
            Assert.AreEqual(CorrectionPublisher.BuilderVersion, manifest.BuilderVersion);
            Assert.AreEqual(MinLocations, manifest.Provenance.MinLocations);
            CollectionAssert.AreEquivalent(
                source.FullBuild(MinLocations).Keys.ToList(), manifest.Z, "published sections are the full build's sections");
            Assert.IsTrue(manifest.SectionFingerprints.Count >= manifest.Z.Count);
            Assert.IsFalse(File.Exists(Path.Combine(_dest, "volume.npz")), "volume.npz is built on request");
        }

        [TestMethod]
        public async Task SecondPublish_WithNothingChanged_TouchesNothing()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            DateTime built = PublishedCorrectionSet.LoadManifest(_dest).Provenance.BuiltUtc;
            Dictionary<string, byte[]> before = SectionFiles();

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsTrue(result.Skipped);
            Assert.AreEqual(built, PublishedCorrectionSet.LoadManifest(_dest).Provenance.BuiltUtc);
            CollectionAssert.AreEquivalent(before.Keys.ToList(), SectionFiles().Keys.ToList());
        }

        [TestMethod]
        public async Task EditedSection_IsUpdatedIncrementally_AndOtherSectionFilesAreUntouched()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            Dictionary<string, byte[]> before = SectionFiles();
            DateTime built = PublishedCorrectionSet.LoadManifest(_dest).Provenance.BuiltUtc;

            MoveInterior(source, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsFalse(result.Full);
            Assert.IsFalse(result.Skipped);
            Assert.IsTrue(result.SectionsRecomputed > 0 && result.SectionsRecomputed < before.Count);

            Dictionary<string, byte[]> after = SectionFiles();
            int unchanged = before.Count(kv => after.TryGetValue(kv.Key, out byte[] bytes) && bytes.SequenceEqual(kv.Value));
            Assert.IsTrue(unchanged >= before.Count - result.SectionsRecomputed,
                $"{unchanged} of {before.Count} section files unchanged, {result.SectionsRecomputed} recomputed");

            // The updated directory is what a full rebuild of the edited rows publishes.
            PublishedCorrectionSet published = PublishedCorrectionSet.LoadDirectory(_dest);
            Dictionary<long, SectionVectorField> expected = source.FullBuild(MinLocations);
            CollectionAssert.AreEquivalent(expected.Keys.ToList(), published.Sections.Keys.ToList());
            foreach (KeyValuePair<long, SectionVectorField> kv in expected)
            {
                Assert.AreEqual(kv.Value.NodeCount, published.Sections[kv.Key].NodeCount, $"nodes in section {kv.Key}");
                foreach (LatticeNode node in kv.Value.Nodes)
                {
                    LatticeNode other = published.Sections[kv.Key].Nodes.Single(n => n.Gx == node.Gx && n.Gy == node.Gy);
                    Assert.AreEqual(node.Dx, other.Dx, 1e-3, $"dx in section {kv.Key}");
                    Assert.AreEqual(node.Dy, other.Dy, 1e-3, $"dy in section {kv.Key}");
                }
            }

            CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(_dest);
            Assert.IsTrue(manifest.Provenance.BuiltUtc > built, "clients see a new version token");
            Assert.IsFalse(Directory.Exists(_dest + ".staging") || Directory.Exists(_dest + ".old"));

            // And the next run has nothing to do.
            Assert.IsTrue((await PublishAsync(source, Options())).Skipped);
        }

        [TestMethod]
        public async Task Force_RebuildsEverything()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());

            CorrectionPublishResult result = await PublishAsync(source, Options(o => o.Force = true));

            Assert.IsTrue(result.Full);
            Assert.IsFalse(result.Skipped);
        }

        [TestMethod]
        public async Task ManifestFromAnOlderBuilder_ForcesAFullRebuild()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(_dest);
            manifest.BuilderVersion = 0;
            manifest.SectionFingerprints = null;
            new PublishedCorrectionSet(manifest, new Dictionary<long, SectionVectorField>()).WriteManifest(_dest);

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsTrue(result.Full);
            Assert.AreEqual(CorrectionPublisher.BuilderVersion, PublishedCorrectionSet.LoadManifest(_dest).BuilderVersion);
        }

        [TestMethod]
        public async Task ChangedMinLocations_ForcesAFullRebuild()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());

            CorrectionPublishResult result = await PublishAsync(source, Options(o => o.MinLocations = 4));

            Assert.IsTrue(result.Full);
        }

        [TestMethod]
        public async Task TooManyChangedSections_FallBackToAFullRebuild()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            MoveInterior(source, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

            CorrectionPublishResult result = await PublishAsync(source, Options(o => o.MaxIncrementalFraction = 0.001));

            Assert.IsTrue(result.Full);
        }

        [TestMethod]
        public async Task RecentEdit_WaitsForTheQuietPeriod()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            Dictionary<string, byte[]> before = SectionFiles();
            MoveInterior(source, DateTime.UtcNow.AddMinutes(-5));

            CorrectionPublishResult waiting = await PublishAsync(source, Options(o => o.QuietPeriod = TimeSpan.FromMinutes(60)));

            Assert.IsTrue(waiting.Skipped, "an edit five minutes ago is inside a 60 minute quiet period");
            Assert.IsTrue(before.All(kv => SectionFiles()[kv.Key].SequenceEqual(kv.Value)));

            CorrectionPublishResult later = await PublishAsync(source, Options(o => o.QuietPeriod = TimeSpan.FromMinutes(2)));
            Assert.IsFalse(later.Skipped, "the same edit is old enough for a 2 minute quiet period");
            Assert.IsFalse(later.Full);
        }

        [TestMethod]
        public async Task Force_IgnoresTheQuietPeriod()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            MoveInterior(source, DateTime.UtcNow);

            CorrectionPublishResult result = await PublishAsync(source, Options(o =>
            {
                o.Force = true;
                o.QuietPeriod = TimeSpan.FromHours(1);
            }));

            Assert.IsFalse(result.Skipped);
        }

        [TestMethod]
        public async Task DeletedLocation_IsPickedUp()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());

            SqlMorphologyLoader.CandidateRow row = source.Rows
                .GroupBy(r => r.ParentID).Where(g => g.Count() > 20).First().OrderBy(r => r.Z).Skip(20).First();
            SqlMorphologyLoader.LinkRow into = source.Links.Single(l => l.B == row.ID);
            SqlMorphologyLoader.LinkRow outOf = source.Links.Single(l => l.A == row.ID);
            source.Rows.Remove(row);
            source.Links.Remove(into);
            source.Links.Remove(outOf);
            source.Links.Add(new SqlMorphologyLoader.LinkRow { A = into.A, B = outOf.B, Created = into.Created });

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsFalse(result.Skipped, "the old watermark could not see a deletion");
            Assert.IsFalse(result.Full);
        }

        [TestMethod]
        public async Task Lazy_VolumeArchive_IsBuiltOnce_AndFollowsTheNextPublish()
        {
            InMemoryMorphologySource source = InMemoryMorphologySource.Generate(1, length: 140);
            await PublishAsync(source, Options());

            string first = VolumeArchiveCache.EnsureArchive(_dest);
            Assert.IsTrue(File.Exists(first));
            DateTime written = File.GetLastWriteTimeUtc(first);
            Assert.AreEqual(first, VolumeArchiveCache.EnsureArchive(_dest), "a second request reuses the cached archive");
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(first));
            Assert.AreEqual(0, Directory.GetFiles(_dest, "*.tmp").Length);

            MoveInterior(source, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            await PublishAsync(source, Options());

            Assert.IsFalse(File.Exists(first), "the publish replaced the directory, so the old archive is gone");
            string second = VolumeArchiveCache.EnsureArchive(_dest);
            Assert.AreNotEqual(first, second);
            Assert.IsTrue(File.Exists(second));
            Assert.AreEqual(1, Directory.GetFiles(_dest, "volume*.npz").Length);
        }
    }
}
