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
using ProtoLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLink;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the offline paths of <see cref="PermittedStructureLinksService"/>: a batch saves once, replies
    /// per change in request order, and echoes the composite key. No database is contacted: an interceptor
    /// stands in for SaveChanges. Update and Delete read the table first, so they need a live database
    /// and are not covered here.
    /// </summary>
    public class PermittedStructureLinksServiceUpdateTests
    {
        /// <summary>Unroutable endpoint; the interceptor suppresses the save, so no connection is opened.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task BatchInsertAndNoneReplyInOrderWithOneSave()
        {
            var saves = new CountSaves();
            var options = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(saves)
                .Options;
            using var db = new AnnotationContext(options);
            var service = new PermittedStructureLinksService(db, NullLogger<PermittedStructureLinksService>.Instance);

            var request = new UpdatePermittedStructureLinksRequest
            {
                Changes =
                {
                    new PermittedStructureLinkChange { Action = DBAction.Insert, Result = new ProtoLink { SourceTypeId = 1, TargetTypeId = 2, Bidirectional = true } },
                    new PermittedStructureLinkChange { Action = DBAction.None },
                    new PermittedStructureLinkChange { Action = DBAction.Insert, Result = new ProtoLink { SourceTypeId = 3, TargetTypeId = 4 } },
                }
            };

            var response = await service.UpdatePermittedStructureLinks(request, new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { DBAction.Insert, DBAction.None, DBAction.Insert }, response.Changes.Select(c => c.Action).ToArray());
            Assert.All(response.Changes, c => Assert.True(c.Sucess));
            Assert.Equal((1L, 2L, true), Key(response.Changes[0].Result));
            Assert.Equal((3L, 4L, false), Key(response.Changes[2].Result));
        }

        [Fact]
        public async Task CreateRepliesWithTheStoredLink()
        {
            var saves = new CountSaves();
            var options = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(saves)
                .Options;
            using var db = new AnnotationContext(options);
            var service = new PermittedStructureLinksService(db, NullLogger<PermittedStructureLinksService>.Instance);

            var response = await service.CreatePermittedStructureLink(
                new CreatePermittedStructureLinkRequest { NewObj = new ProtoLink { SourceTypeId = 7, TargetTypeId = 9, Bidirectional = true } },
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal((7L, 9L, true), Key(response.Result));
        }

        private static (long Source, long Target, bool Bidirectional) Key(ProtoLink link) =>
            (link.SourceTypeId, link.TargetTypeId, link.Bidirectional);

        /// <summary>Suppresses the database write and counts SaveChanges calls.</summary>
        private sealed class CountSaves : SaveChangesInterceptor
        {
            public int Count { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
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
