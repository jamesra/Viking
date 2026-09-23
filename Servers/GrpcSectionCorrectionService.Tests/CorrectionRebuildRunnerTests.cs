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
                    new IdentityVolumeRow("NoXml", "", null),
                    new IdentityVolumeRow("NoSql", "http://example/volume.vikingxml", null),
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", "http://ann")
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
        public async Task RunAsync_ContinuesAfterOneVolumePublishFailure()
        {
            string root = Path.Combine(Path.GetTempPath(), "rebuild-fail-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var volumes = new StaticIdentityVolumeSource(
                [
                    new IdentityVolumeRow("RC1", "http://example/RC1.vikingxml", null),
                    new IdentityVolumeRow("RC2", "http://example/RC2.vikingxml", null)
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
    }
}
