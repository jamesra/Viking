using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;
using EfStructureLink = Viking.DataModel.Annotation.StructureLink;
using ProtoStructureLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureLink;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the offline behavior of <see cref="StructureService.CreateStructureLink"/>: a link from a
    /// structure to itself is refused with InvalidArgument before anything is saved, and a valid link is
    /// saved once, stamped with the caller's name rather than the payload's, and echoed back. No database
    /// is contacted: an interceptor stands in for SaveChanges.
    /// </summary>
    public class StructureServiceLinkTests
    {
        /// <summary>Unroutable endpoint; the interceptor suppresses the save, so no connection is opened.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task SelfLinkIsRefusedWithoutSaving()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.CreateStructureLink(
                new CreateStructureLinkRequest { NewLink = new ProtoStructureLink { SourceId = 5, TargetId = 5 } },
                new TestCallContext()));

            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal(0, saves.Count);
        }

        [Fact]
        public async Task CreateSavesOnceStampsCallerAndEchoesTheLink()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var response = await service.CreateStructureLink(
                new CreateStructureLinkRequest
                {
                    NewLink = new ProtoStructureLink { SourceId = 3, TargetId = 8, Bidirectional = true, Tags = "t", Username = "spoofed" }
                },
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { "unknown" }, saves.Usernames);
            Assert.Equal((3L, 8L, true, "t"), (response.Result.SourceId, response.Result.TargetId, response.Result.Bidirectional, response.Result.Tags));
            Assert.Equal("unknown", response.Result.Username);
        }

        private static AnnotationContext NewContext(SaveChangesInterceptor interceptor) =>
            new AnnotationContext(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptor)
                .Options);

        /// <summary>Suppresses the database write, counts SaveChanges calls and records the usernames of the links being saved.</summary>
        private sealed class CaptureSaves : SaveChangesInterceptor
        {
            public int Count { get; private set; }
            public string[] Usernames { get; private set; } = Array.Empty<string>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
                Usernames = eventData.Context.ChangeTracker.Entries<EfStructureLink>().Select(e => e.Entity.Username).ToArray();
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(eventData.Context.ChangeTracker.Entries().Count()));
            }
        }

        /// <summary>Call context carrying an anonymous HTTP context (as ASP.NET Core hosting supplies) and an uncancelled token.</summary>
        private sealed class TestCallContext : ServerCallContext
        {
            /// <summary>Key Grpc.AspNetCore's GetHttpContext reads from <see cref="ServerCallContext.UserState"/>.</summary>
            private const string HttpContextKey = "__HttpContext";

            public TestCallContext() => UserState[HttpContextKey] = new DefaultHttpContext();

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
