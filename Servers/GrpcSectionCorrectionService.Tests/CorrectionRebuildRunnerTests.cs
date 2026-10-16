using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrection;
using Path = System.IO.Path;

namespace GrpcSectionCorrectionService.Tests
{
    [TestClass]
    public class CorrectionRebuildRunnerTests
    {
        [TestMethod]
        public async Task RunAsync_SkipsUnmappedAndMissingEndpoint_PublishesMappedVolume()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var volumes = new StaticIdentityVolumeSource(
                [
                    new IdentityVolumeRow("NoXml", "", null, "NoXml"),
                    new IdentityVolumeRow("NoSql", "http://example/volume.vikingxml", null, "NoSql"),
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann", "RC1")
                ]);
                var connections = new AnnotationConnectionResolver(
                    new Dictionary<string, string> { ["RC1"] = "Server=local;Database=RC1" },
                    template: null);
                var publisher = new RecordingPublisher();
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["Corrections:RootDirectory"] = root,
                        ["Rebuild:Force"] = "false"
                    })
                    .Build();

                var runner = new CorrectionRebuildRunner(
                    volumes,
                    connections,
                    publisher,
                    catalog,
                    status,
                    config,
                    NullLogger<CorrectionRebuildRunner>.Instance);

                await runner.RunAsync(CancellationToken.None);

                Assert.AreEqual(1, publisher.Calls.Count);
                Assert.AreEqual("Server=local;Database=RC1", publisher.Calls[0].Connection);
                Assert.AreEqual("http://example/RC1.vikingxml", publisher.Calls[0].VolumeUrl);
                StringAssert.Contains(publisher.Calls[0].Output, VolumeCorrectionCatalog.SanitizeVolumeName("RC1"));
                Assert.IsFalse(status.Snapshot().InProgress);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RunAsync_ContextsSharingDatabaseAndStosGroup_BuildOnce()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-share-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var volumes = new StaticIdentityVolumeSource(
                [
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann", "RC1db", "grid"),
                    new IdentityVolumeRow("RC1-Internal", "http://example/RC1i.vikingxml", "http://ann", "rc1DB", "GRID"),
                    new IdentityVolumeRow("RC1-Other", "http://example/RC1o.vikingxml", "http://ann", "RC1db", "other"),
                    new IdentityVolumeRow("RC2", "http://example/RC2.vikingxml", "http://ann2", "RC2db", "grid")
                ]);
                var connections = new AnnotationConnectionResolver(
                    new Dictionary<string, string>
                    {
                        ["RC1db"] = "Server=local;Database=RC1",
                        ["RC2db"] = "Server=local;Database=RC2"
                    },
                    template: null);
                var publisher = new RecordingPublisher();
                using VolumeCorrectionCatalog catalog = new(root);
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["Corrections:RootDirectory"] = root })
                    .Build();

                var runner = new CorrectionRebuildRunner(
                    volumes, connections, publisher, catalog, new RebuildStatusStore(), config,
                    NullLogger<CorrectionRebuildRunner>.Instance);

                await runner.RunAsync(CancellationToken.None);

                // (RC1db, grid) is shared by two contexts and built once; (RC1db, other) and (RC2db, grid) are distinct.
                Assert.AreEqual(3, publisher.Calls.Count);
                CollectionAssert.AreEquivalent(
                    new[] { "RC1db|grid", "RC1db|other", "RC2db|grid" },
                    publisher.Calls.ConvertAll(c => Path.GetFileName(c.Output) + "|" + c.StosGroups));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RunAsync_NoSqlMapping_FallsBackToODataWhenIdentityGaveAnEndpoint()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-odata-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var volumes = new StaticIdentityVolumeSource(
                [
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann", "RC1db", "grid", "https://ann/RC1/OData/"),
                    new IdentityVolumeRow("RC2", "http://example/RC2.vikingxml", "http://ann2", "RC2db", "grid")
                ]);
                var publisher = new RecordingPublisher();
                using VolumeCorrectionCatalog catalog = new(root);
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["Corrections:RootDirectory"] = root })
                    .Build();
                var runner = new CorrectionRebuildRunner(
                    volumes, new AnnotationConnectionResolver(null, template: null), publisher, catalog, new RebuildStatusStore(), config,
                    NullLogger<CorrectionRebuildRunner>.Instance);

                await runner.RunAsync(CancellationToken.None);

                Assert.AreEqual(0, publisher.Calls.Count);
                Assert.AreEqual(1, publisher.ODataCalls.Count, "RC2 has neither SQL nor OData and is skipped");
                Assert.AreEqual(new Uri("https://ann/RC1/OData/"), publisher.ODataCalls[0].OData);
                Assert.AreEqual("RC1db", Path.GetFileName(publisher.ODataCalls[0].Output));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RunAsync_ContinuesAfterOneVolumePublishFailure()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-fail-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var volumes = new StaticIdentityVolumeSource(
                [
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", null, "RC1"),
                    new IdentityVolumeRow("RC2", "http://example/RC2.vikingxml", null, "RC2")
                ]);
                var connections = new AnnotationConnectionResolver(
                    new Dictionary<string, string>
                    {
                        ["RC1"] = "Server=local;Database=RC1",
                        ["RC2"] = "Server=local;Database=RC2"
                    },
                    template: null);
                var publisher = new RecordingPublisher { ThrowOnVolumeUrl = "http://example/RC1.vikingxml" };
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        ["Corrections:RootDirectory"] = root
                    })
                    .Build();

                var runner = new CorrectionRebuildRunner(
                    volumes,
                    connections,
                    publisher,
                    catalog,
                    status,
                    config,
                    NullLogger<CorrectionRebuildRunner>.Instance);

                await runner.RunAsync(CancellationToken.None);

                Assert.AreEqual(2, publisher.Calls.Count);
                StringAssert.Contains(status.Snapshot().LastError, "RC1");
                Assert.IsFalse(status.Snapshot().InProgress);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>Identity source that fails or cancels instead of listing, for runner error-path tests.</summary>
        sealed class FailingVolumeSource(Exception toThrow) : IIdentityVolumeSource
        {
            public Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromException<IReadOnlyList<IdentityVolumeRow>>(toThrow);
        }

        static CorrectionRebuildRunner MakeRunner(IIdentityVolumeSource volumes, RebuildStatusStore status, VolumeCorrectionCatalog catalog) =>
            new(volumes, new AnnotationConnectionResolver(null, template: null), new RecordingPublisher(), catalog, status,
                new ConfigurationBuilder().Build(), NullLogger<CorrectionRebuildRunner>.Instance);

        [TestMethod]
        public async Task RunAsync_IdentityListFails_RecordsStartAndErrorAndRethrows()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-ident-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                var runner = MakeRunner(new FailingVolumeSource(new InvalidOperationException("identity down")), status, catalog);

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => runner.RunAsync(CancellationToken.None));

                var snapshot = status.Snapshot();
                Assert.IsNotNull(snapshot.LastStartedUtc);
                Assert.IsNotNull(snapshot.LastCompletedUtc);
                Assert.IsFalse(snapshot.InProgress);
                StringAssert.Contains(snapshot.LastError, "identity down");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RunAsync_Cancelled_RecordsNonEmptyErrorAndRethrows()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-cancel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                var runner = MakeRunner(new FailingVolumeSource(new OperationCanceledException()), status, catalog);

                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => runner.RunAsync(CancellationToken.None));

                var snapshot = status.Snapshot();
                Assert.IsFalse(snapshot.InProgress);
                StringAssert.Contains(snapshot.LastError, "cancelled");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RunAsync_VolumeFilter_BuildsOnlyThatVolumeAndCountsProgress()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-filter-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var volumes = new StaticIdentityVolumeSource(
                [
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann", "RC1db", "grid"),
                    new IdentityVolumeRow("RC2", "http://example/RC2.vikingxml", "http://ann2", "RC2db", "grid")
                ]);
                var connections = new AnnotationConnectionResolver(
                    new Dictionary<string, string> { ["RC1db"] = "Server=a", ["RC2db"] = "Server=b" }, template: null);
                var publisher = new RecordingPublisher();
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["Corrections:RootDirectory"] = root })
                    .Build();
                var runner = new CorrectionRebuildRunner(
                    volumes, connections, publisher, catalog, status, config, NullLogger<CorrectionRebuildRunner>.Instance);

                await runner.RunAsync("rc2DB", force: false, CancellationToken.None);

                Assert.AreEqual(1, publisher.Calls.Count);
                Assert.AreEqual("Server=b", publisher.Calls[0].Connection);
                var snapshot = status.Snapshot();
                Assert.AreEqual(1, snapshot.GroupsTotal);
                Assert.AreEqual(1, snapshot.GroupsDone);
                Assert.AreEqual("", snapshot.LastError);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task RunAsync_UnknownVolume_RecordsErrorAndBuildsNothing()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-unknown-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var publisher = new RecordingPublisher();
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                var volumes = new StaticIdentityVolumeSource([new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann", "RC1")]);
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["Corrections:RootDirectory"] = root })
                    .Build();
                var runner = new CorrectionRebuildRunner(
                    volumes, new AnnotationConnectionResolver(null, template: null), publisher, catalog, status, config,
                    NullLogger<CorrectionRebuildRunner>.Instance);

                await runner.RunAsync("Nope", force: false, CancellationToken.None);

                Assert.AreEqual(0, publisher.Calls.Count);
                StringAssert.Contains(status.Snapshot().LastError, "Nope");
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public async Task TryStartInBackground_WhileRunning_ReturnsFalseThenSucceedsAfterFinish()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-bg-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var publisher = new BlockingPublisher(release.Task);
                using VolumeCorrectionCatalog catalog = new(root);
                var status = new RebuildStatusStore();
                var volumes = new StaticIdentityVolumeSource([new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann", "RC1")]);
                IConfiguration config = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string> { ["Corrections:RootDirectory"] = root })
                    .Build();
                var connections = new AnnotationConnectionResolver(
                    new Dictionary<string, string> { ["RC1"] = "Server=a" }, template: null);
                var runner = new CorrectionRebuildRunner(
                    volumes, connections, publisher, catalog, status, config, NullLogger<CorrectionRebuildRunner>.Instance);

                Assert.IsTrue(runner.TryStartInBackground("RC1", false, CancellationToken.None));
                await publisher.Entered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.IsTrue(status.Snapshot().InProgress);
                Assert.IsFalse(runner.TryStartInBackground("RC1", false, CancellationToken.None));

                release.SetResult();
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                while (status.Snapshot().InProgress && DateTime.UtcNow < deadline)
                    await Task.Delay(20);
                Assert.IsFalse(status.Snapshot().InProgress);
                Assert.IsTrue(runner.TryStartInBackground("RC1", false, CancellationToken.None));
                await Task.Delay(100);
            }
            finally
            {
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        [TestMethod]
        public void CorrectionMenu_RenderBar_ScalesAndHandlesUnknownTotal()
        {
            Assert.AreEqual("[" + new string('.', 30) + "]", CorrectionMenu.RenderBar(0, 0));
            Assert.AreEqual("[" + new string('#', 15) + new string('-', 15) + "]", CorrectionMenu.RenderBar(1, 2));
            Assert.AreEqual("[" + new string('#', 30) + "]", CorrectionMenu.RenderBar(5, 2));
            Assert.AreEqual("3d 4h ago", CorrectionMenu.FormatAge(TimeSpan.FromHours(76)));
        }

        sealed class BlockingPublisher(Task release) : IVolumeCorrectionPublisher
        {
            readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Entered => _entered.Task;

            public async Task<int> PublishVolumeAsync(
                string annotationConnection, string volumeUrl, Viking.SectionCorrectionBuilder.CorrectionPublishOptions options,
                CancellationToken cancellationToken)
            {
                _entered.TrySetResult();
                await release;
                return 0;
            }

            public Task<int> PublishVolumeFromODataAsync(
                Uri odataEndpoint, string volumeUrl, Viking.SectionCorrectionBuilder.CorrectionPublishOptions options,
                CancellationToken cancellationToken) => Task.FromResult(0);
        }
    }
}
