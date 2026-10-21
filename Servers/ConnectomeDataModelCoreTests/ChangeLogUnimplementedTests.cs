using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins that the two change-log RPCs answer Unimplemented, naming the RPC, rather than an empty
    /// list that clients would read as "no changes". No database is contacted.
    /// </summary>
    public class ChangeLogUnimplementedTests
    {
        /// <summary>Unroutable endpoint; the change-log RPCs never touch the context.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task StructureChangeLogIsUnimplementedAndNamesTheRpc()
        {
            using var db = NewContext();
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                service.GetStructureChangeLog(new GetStructureChangeLogRequest(), new NoOpCallContext()));

            Assert.Equal(StatusCode.Unimplemented, ex.StatusCode);
            Assert.Contains(nameof(StructureService.GetStructureChangeLog), ex.Status.Detail);
        }

        [Fact]
        public async Task LocationChangeLogIsUnimplementedAndNamesTheRpc()
        {
            using var db = NewContext();
            var service = new LocationService(db, NullLogger<LocationService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                service.GetLocationChangeLog(new GetLocationChangeLogRequest(), new NoOpCallContext()));

            Assert.Equal(StatusCode.Unimplemented, ex.StatusCode);
            Assert.Contains(nameof(LocationService.GetLocationChangeLog), ex.Status.Detail);
        }

        private static AnnotationContext NewContext() =>
            new AnnotationContext(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .Options);

        /// <summary>Call context for RPCs that never read it.</summary>
        private sealed class NoOpCallContext : ServerCallContext
        {
            protected override string MethodCore => "test";
            protected override string HostCore => "localhost";
            protected override string PeerCore => "127.0.0.1";
            protected override DateTime DeadlineCore => DateTime.MaxValue;
            protected override Metadata RequestHeadersCore { get; } = new();
            protected override CancellationToken CancellationTokenCore => CancellationToken.None;
            protected override Metadata ResponseTrailersCore { get; } = new();
            protected override Status StatusCore { get; set; }
            protected override WriteOptions WriteOptionsCore { get; set; }
            protected override AuthContext AuthContextCore => null;

            protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions options) =>
                throw new NotSupportedException();

            protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
        }
    }
}
