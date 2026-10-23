using Grpc.Core;
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
using Viking.SectionCorrectionServiceTypes.gRPC.V1.Protos;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Argument handling of <see cref="SectionCorrectionsService.GetStructureFingerprint"/>.  The fingerprint itself is
    /// pinned in SectionCorrection.Tests (StructureFingerprintTests); no SQL is touched here.
    /// </summary>
    [TestClass]
    public sealed class GetStructureFingerprintTests
    {
        [TestMethod]
        public async Task EmptyStructureIds_IsInvalidArgument()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => CreateService().GetStructureFingerprint(
                    new GetStructureFingerprintRequest { VolumeName = "RC1" }, new CallContext()));
            Assert.AreEqual(StatusCode.InvalidArgument, ex.StatusCode);
        }

        [TestMethod]
        public async Task UnmappedVolume_IsFailedPrecondition()
        {
            RpcException ex = await Assert.ThrowsExactlyAsync<RpcException>(
                () => CreateService().GetStructureFingerprint(
                    new GetStructureFingerprintRequest { VolumeName = "Unmapped", StructureIds = { 1L } }, new CallContext()));
            Assert.AreEqual(StatusCode.FailedPrecondition, ex.StatusCode);
            StringAssert.Contains(ex.Status.Detail, "Unmapped");
        }

        static SectionCorrectionsService CreateService() =>
            new(new VolumeCorrectionCatalog(Path.Combine(Path.GetTempPath(), "viking-structure-fingerprint-unused")),
                new StaticIdentityVolumeSource([]),
                new AnnotationConnectionResolver(new Dictionary<string, string> { ["RC1"] = "Server=unused" }, null),
                new ConfigurationBuilder().Build(),
                new RebuildStatusStore(),
                NullLogger<SectionCorrectionsService>.Instance);

        sealed class CallContext : ServerCallContext
        {
            protected override string MethodCore => "AnnotateSectionCorrections/GetStructureFingerprint";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = [];
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
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
