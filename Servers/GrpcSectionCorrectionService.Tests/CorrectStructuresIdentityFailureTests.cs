using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Viking.GrpcSectionCorrectionService;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Pins the status code <see cref="SectionCorrectionsService.CorrectStructures"/> returns when the Identity
    /// volume listing fails: transport failures map to UNAVAILABLE (like the volume-load failure beside it),
    /// caller cancellation stays an <see cref="OperationCanceledException"/>, other faults are not remapped, and an
    /// unlisted volume is still NOT_FOUND.
    /// No SQL or network is touched; the Identity source fails before any load.
    /// </summary>
    [TestClass]
    public sealed class CorrectStructuresIdentityFailureTests
    {
        const string Volume = "RC1";

        /// <summary>Identity transport failures, including an HTTP timeout, become UNAVAILABLE.</summary>
        [TestMethod]
        [DataRow("http")]
        [DataRow("io")]
        [DataRow("token")]
        [DataRow("timeout")]
        public async Task IdentityTransportFailureIsUnavailable(string kind)
        {
            Exception failure = kind switch
            {
                "http" => new HttpRequestException("connection refused"),
                "io" => new IOException("connection reset"),
                "token" => new InvalidOperationException("Identity returned no access_token."),
                "timeout" => new TaskCanceledException("HttpClient.Timeout elapsed"),
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => CreateService(failure).CorrectStructures(Request(), new CallContext(CancellationToken.None)));

            Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
            StringAssert.Contains(ex.Status.Detail, Volume);
        }

        /// <summary>
        /// The volume list holds only contexts that have an annotation database, once per database. Contexts without
        /// one (images only) and unrelated published folders are not volumes.
        /// </summary>
        [TestMethod]
        public async Task ListVolumesHoldsOnlyContextsWithAnAnnotationDatabase()
        {
            var volumes = new StaticIdentityVolumeSource(
            [
                new IdentityVolumeRow("RC1", "https://x/RC1.vikingxml", "https://ann/RC1", "RC1"),
                new IdentityVolumeRow("RC2", "https://x/RC2.vikingxml", "https://ann/RC2", "RC2"),
                new IdentityVolumeRow("RC2-Pitt", "https://x/RC2p.vikingxml", "https://ann/RC2", "RC2"),
                new IdentityVolumeRow("Zia", "https://x/Zia.vikingxml", null),
                new IdentityVolumeRow("Mosaics", "https://x/Mosaics.vikingxml", "", "  ")
            ]);

            ListVolumesResponse response = await CreateService(volumes)
                .ListVolumes(new ListVolumesRequest(), new CallContext(CancellationToken.None));

            CollectionAssert.AreEqual(new[] { "RC1", "RC2" }, response.Volumes.Select(v => v.VolumeName).ToArray());
        }

        /// <summary>Without Identity settings the list reports what to configure instead of showing every folder.</summary>
        [TestMethod]
        public async Task ListVolumesWithoutIdentityIsUnavailableAndSaysWhatToSet()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => CreateService(new UnconfiguredIdentityVolumeSource())
                    .ListVolumes(new ListVolumesRequest(), new CallContext(CancellationToken.None)));

            Assert.AreEqual(StatusCode.Unavailable, ex.StatusCode);
            StringAssert.Contains(ex.Status.Detail, "IdentityServer__ClientSecret");
        }

        /// <summary>Cancellation of the call itself is not reported as UNAVAILABLE.</summary>
        [TestMethod]
        public async Task CallerCancellationPassesThrough()
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                () => CreateService(new OperationCanceledException(cts.Token))
                    .CorrectStructures(Request(), new CallContext(cts.Token)));
        }

        /// <summary>Faults that are not Identity transport failures keep their original exception.</summary>
        [TestMethod]
        public async Task OtherFaultsAreNotRemapped()
        {
            await Assert.ThrowsExactlyAsync<FormatException>(
                () => CreateService(new FormatException("bad row"))
                    .CorrectStructures(Request(), new CallContext(CancellationToken.None)));
        }

        /// <summary>A reachable Identity that does not list the volume still yields NOT_FOUND, not UNAVAILABLE.</summary>
        [TestMethod]
        public async Task UnlistedVolumeIsNotFound()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => CreateService(new StaticIdentityVolumeSource([new IdentityVolumeRow("Other", "https://other/volume", "")]))
                    .CorrectStructures(Request(), new CallContext(CancellationToken.None)));

            Assert.AreEqual(StatusCode.NotFound, ex.StatusCode);
        }

        static CorrectStructuresRequest Request() => new()
        {
            VolumeName = Volume,
            StosGroup = "Grid",
            StructureIds = { 1L }
        };

        static SectionCorrectionsService CreateService(Exception identityFailure) =>
            CreateService(new FailingIdentitySource(identityFailure));

        static SectionCorrectionsService CreateService(IIdentityVolumeSource volumes) =>
            new(new VolumeCorrectionCatalog(Path.Combine(Path.GetTempPath(), "viking-correct-structures-unused")),
                volumes,
                new AnnotationConnectionResolver(new Dictionary<string, string> { [Volume] = "Server=unused" }, null),
                new ConfigurationBuilder().Build(),
                new RebuildStatusStore(),
                NullLogger<SectionCorrectionsService>.Instance);

        /// <summary>Identity source whose listing always fails with the given exception.</summary>
        sealed class FailingIdentitySource(Exception toThrow) : IIdentityVolumeSource
        {
            public Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken) =>
                Task.FromException<IReadOnlyList<IdentityVolumeRow>>(toThrow);
        }

        /// <summary>Minimal unary call context carrying only a cancellation token.</summary>
        sealed class CallContext(CancellationToken token) : ServerCallContext
        {
            protected override string MethodCore => "AnnotateSectionCorrections/CorrectStructures";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = [];
            protected override CancellationToken CancellationTokenCore => token;
            protected override Metadata ResponseTrailersCore { get; } = [];
            protected override Status StatusCore { get; set; }
            protected override WriteOptions WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => null;

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
