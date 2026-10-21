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
    /// Pins the offline behavior of <see cref="StructureTypeService.CreateStructureType"/> (one save,
    /// caller's name rather than the payload's, database-assigned Id echoed, failures mapped to Unknown)
    /// and of a batch <see cref="StructureTypeService.Update"/> row with no action set. No database is
    /// contacted: an interceptor stands in for SaveChanges.
    /// </summary>
    public class StructureTypeServiceCreateTests
    {
        /// <summary>Unroutable endpoint; the interceptor suppresses the save, so no connection is opened.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task CreateSavesOnceStampsCallerAndEchoesTheAssignedId()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves);
            var service = new StructureTypeService(db, NullLogger<StructureTypeService>.Instance);

            var response = await service.CreateStructureType(
                new CreateStructureTypeRequest { Obj = new ProtoStructureType { Name = "Synapse", Code = "S", Username = "spoofed" } },
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { "unknown" }, saves.Usernames);
            Assert.Equal(777L, response.Result.Id);
            Assert.Equal(("Synapse", "S", "unknown"), (response.Result.Name, response.Result.Code, response.Result.Username));
        }

        [Fact]
        public async Task CreateFailureIsReportedAsUnknownWithTheRpcName()
        {
            using var db = NewContext(new ThrowOnSave());
            var service = new StructureTypeService(db, NullLogger<StructureTypeService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.CreateStructureType(
                new CreateStructureTypeRequest { Obj = new ProtoStructureType { Name = "A", Code = "A" } },
                new TestCallContext()));

            Assert.Equal(StatusCode.Unknown, ex.StatusCode);
            Assert.Equal(nameof(StructureTypeService.CreateStructureType), ex.Status.Detail);
        }

        [Fact]
        public async Task BatchRowWithoutAnActionFailsAndTheBatchStillSavesOnce()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves);
            var service = new StructureTypeService(db, NullLogger<StructureTypeService>.Instance);

            var response = await service.Update(
                new UpdateStructureTypesRequest { Objs = { new StructureTypeChangeRequest(), new StructureTypeChangeRequest() } },
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { false, false }, response.Results.Select(r => r.Success).ToArray());
        }

        private static AnnotationContext NewContext(SaveChangesInterceptor interceptor) =>
            new AnnotationContext(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptor)
                .Options);

        /// <summary>Suppresses the database write, counts SaveChanges calls, records the usernames being saved and assigns Id 777 to added types.</summary>
        private sealed class CaptureSaves : SaveChangesInterceptor
        {
            public int Count { get; private set; }
            public string[] Usernames { get; private set; } = Array.Empty<string>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
                var types = eventData.Context.ChangeTracker.Entries<EfStructureType>().ToList();
                Usernames = types.Select(e => e.Entity.Username).ToArray();
                foreach (var entry in types)
                {
                    var id = entry.Property(e => e.Id);
                    id.CurrentValue = 777L;
                    id.IsTemporary = false;
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(types.Count));
            }
        }

        /// <summary>Fails the save the way a database outage would.</summary>
        private sealed class ThrowOnSave : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("database unavailable");
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
