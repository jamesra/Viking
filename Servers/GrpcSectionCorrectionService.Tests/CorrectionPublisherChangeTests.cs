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
    /// Publishes to a real directory from in-memory rows (identity stos, no previews) to check when the section
    /// fingerprints leave a published set alone and when they rebuild it.
    /// </summary>
    [TestClass]
    public class CorrectionPublisherChangeTests
    {
        const int MinLocations = 3;
        string _root;
        string _dest;

        [TestInitialize]
        public void Setup()
        {
            _root = Path.Combine(Path.GetTempPath(), "correction-change-" + Guid.NewGuid().ToString("N"));
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

        static Task<CorrectionPublishResult> PublishAsync(InMemoryFingerprintSource source, CorrectionPublishOptions options) =>
            CorrectionPublisher.PublishGroupAsync(
                options,
                null,
                InMemoryFingerprintSource.Scale,
                "None",
                source,
                () => Task.FromResult(SqlMorphologyLoader.FromRows(
                    [.. source.Rows.GroupBy(r => r.ParentID).Where(g => g.Count() >= options.MinLocations).SelectMany(g => g)],
                    source.Links, null, InMemoryFingerprintSource.Scale, "None")),
                CancellationToken.None);

        static void MoveInterior(InMemoryFingerprintSource source, DateTime editedAt)
        {
            SqlMorphologyLoader.CandidateRow row = source.Rows
                .GroupBy(r => r.ParentID).Where(g => g.Count() > 20).First()
                .OrderBy(r => r.Z).Skip(20).First();
            row.X += 300;
            row.Y -= 200;
            row.LastModified = editedAt;
        }

        DateTime BuiltUtc() => PublishedCorrectionSet.LoadManifest(_dest).Provenance.BuiltUtc;

        [TestMethod]
        public async Task FirstPublish_RecordsFingerprintsAndTheJointMethod()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsFalse(result.Skipped);
            CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(_dest);
            Assert.AreEqual(CorrectionPublisher.BuilderVersion, manifest.BuilderVersion);
            Assert.AreEqual(MinLocations, manifest.Provenance.MinLocations);
            Assert.AreEqual("joint-second-difference-v1", manifest.Provenance.Method);
            CollectionAssert.AreEquivalent(
                source.Rows.Select(r => r.Z).Distinct().ToList(), manifest.SectionFingerprints.Keys.ToList(),
                "one fingerprint per section with annotations");
            Assert.IsFalse(File.Exists(Path.Combine(_dest, "volume.npz")), "volume.npz is built on request");
        }

        [TestMethod]
        public async Task SecondPublish_WithNothingChanged_TouchesNothing()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            DateTime built = BuiltUtc();

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsTrue(result.Skipped);
            Assert.AreEqual(built, BuiltUtc());
        }

        [TestMethod]
        public async Task EditedSection_RebuildsTheGroup()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            DateTime built = BuiltUtc();

            MoveInterior(source, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsFalse(result.Skipped);
            Assert.IsTrue(BuiltUtc() > built, "clients see a new version token");
            Assert.IsFalse(Directory.Exists(_dest + ".staging") || Directory.Exists(_dest + ".old"));

            // And the next run has nothing to do.
            Assert.IsTrue((await PublishAsync(source, Options())).Skipped);
        }

        [TestMethod]
        public async Task Force_Rebuilds()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());

            CorrectionPublishResult result = await PublishAsync(source, Options(o => o.Force = true));

            Assert.IsFalse(result.Skipped);
        }

        [TestMethod]
        public async Task ManifestFromAnOlderBuilder_IsRebuilt()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(_dest);
            manifest.BuilderVersion = 0;
            manifest.SectionFingerprints = null;
            new PublishedCorrectionSet(manifest, new Dictionary<long, SectionVectorField>()).WriteManifest(_dest);

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsFalse(result.Skipped);
            Assert.AreEqual(CorrectionPublisher.BuilderVersion, PublishedCorrectionSet.LoadManifest(_dest).BuilderVersion);
        }

        [TestMethod]
        public async Task SetFromTheLeaveOneOutField_IsRebuilt()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            CorrectionManifestDto manifest = PublishedCorrectionSet.LoadManifest(_dest);
            manifest.Provenance.Method = null;
            new PublishedCorrectionSet(manifest, new Dictionary<long, SectionVectorField>()).WriteManifest(_dest);

            CorrectionPublishResult result = await PublishAsync(source, Options());

            Assert.IsFalse(result.Skipped);
            Assert.AreEqual("joint-second-difference-v1", PublishedCorrectionSet.LoadManifest(_dest).Provenance.Method);
        }

        [TestMethod]
        public async Task ChangedMinLocations_IsRebuilt()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());

            CorrectionPublishResult result = await PublishAsync(source, Options(o => o.MinLocations = 4));

            Assert.IsFalse(result.Skipped);
        }

        [TestMethod]
        public async Task RecentEdit_WaitsForTheQuietPeriod()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
            await PublishAsync(source, Options());
            DateTime built = BuiltUtc();
            MoveInterior(source, DateTime.UtcNow.AddMinutes(-5));

            CorrectionPublishResult waiting = await PublishAsync(source, Options(o => o.QuietPeriod = TimeSpan.FromMinutes(60)));

            Assert.IsTrue(waiting.Skipped, "an edit five minutes ago is inside a 60 minute quiet period");
            Assert.AreEqual(built, BuiltUtc());

            CorrectionPublishResult later = await PublishAsync(source, Options(o => o.QuietPeriod = TimeSpan.FromMinutes(2)));
            Assert.IsFalse(later.Skipped, "the same edit is old enough for a 2 minute quiet period");
        }

        [TestMethod]
        public async Task Force_IgnoresTheQuietPeriod()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
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
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
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

            Assert.IsFalse(result.Skipped, "a watermark alone could not see a deletion");
        }

        [TestMethod]
        public async Task Lazy_VolumeArchive_IsBuiltOnce_AndFollowsTheNextPublish()
        {
            InMemoryFingerprintSource source = InMemoryFingerprintSource.Generate(1, length: 140);
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
