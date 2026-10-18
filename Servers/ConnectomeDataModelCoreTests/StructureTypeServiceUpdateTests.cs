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
using EfStructureType = Viking.DataModel.Annotation.StructureType;
using ProtoStructureType = Viking.AnnotationServiceTypes.gRPC.V1.Protos.StructureType;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins that the batch <see cref="StructureTypeService.Update"/> reports each created row with the
    /// Id the database assigned, as <see cref="StructureTypeService.CreateStructureType"/> and the
    /// structure batch Update do. No database is contacted: an interceptor stands in for SaveChanges
    /// and assigns identity values the way SQL Server would.
    /// </summary>
    public class StructureTypeServiceUpdateTests
    {
        /// <summary>Unroutable endpoint; the interceptor suppresses the save, so no connection is opened.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task BatchCreateRepliesWithDatabaseAssignedIds()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            var options = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(identity)
                .Options;
            using var db = new AnnotationContext(options);
            var service = new StructureTypeService(db, NullLogger<StructureTypeService>.Instance);

            var request = new UpdateStructureTypesRequest
            {
                Objs =
                {
                    new StructureTypeChangeRequest { Create = new ProtoStructureType { Name = "A", Code = "A" } },
                    new StructureTypeChangeRequest { Create = new ProtoStructureType { Name = "B", Code = "B" } },
                }
            };

            var response = await service.Update(request, new TestCallContext());

            Assert.Equal(1, identity.SaveCount);
            Assert.All(response.Results, r => Assert.True(r.Success));
            Assert.Equal(new long[] { 1000, 1001 }, response.Results.Select(r => r.Created.Id).ToArray());
            Assert.Equal(new[] { "A", "B" }, response.Results.Select(r => r.Created.Name).ToArray());
        }

        /// <summary>
        /// Suppresses the database write and gives each added structure type the next identity value,
        /// leaving the tracked entity as it would be after a real insert.
        /// </summary>
        private sealed class IdentityOnSave(long firstId) : SaveChangesInterceptor
        {
            private long _next = firstId;

            /// <summary>Number of SaveChanges calls seen; the batch must save once.</summary>
            public int SaveCount { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                SaveCount++;
                var added = eventData.Context.ChangeTracker.Entries<EfStructureType>()
                    .Where(e => e.State == EntityState.Added).ToList();
                foreach (var entry in added)
                {
                    var id = entry.Property(e => e.Id);
                    id.CurrentValue = _next++;
                    id.IsTemporary = false;
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(added.Count));
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
