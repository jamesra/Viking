using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// structure batch Update do, and that each change sees the changes before it in the same batch:
    /// an update or delete of a type the batch deleted fails its own row and the delete stands.
    /// No database is contacted: interceptors stand in for the connection, the Id lookups and
    /// SaveChanges, which assigns identity values the way SQL Server would.
    /// </summary>
    public class StructureTypeServiceUpdateTests
    {
        /// <summary>Unroutable endpoint; the interceptors suppress the connection, queries and save.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task BatchCreateRepliesWithDatabaseAssignedIds()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity);

            var response = await Update(db,
                new StructureTypeChangeRequest { Create = new ProtoStructureType { Name = "A", Code = "A" } },
                new StructureTypeChangeRequest { Create = new ProtoStructureType { Name = "B", Code = "B" } });

            Assert.Equal(1, identity.SaveCount);
            Assert.All(response.Results, r => Assert.True(r.Success));
            Assert.Equal(new long[] { 1000, 1001 }, response.Results.Select(r => r.Created.Id).ToArray());
            Assert.Equal(new[] { "A", "B" }, response.Results.Select(r => r.Created.Name).ToArray());
        }

        [Fact]
        public async Task ValidBatchAppliesEachChangeToItsOwnId()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old"), Stored(2, "Gone")));

            var response = await Update(db, UpdateOf(1, "New"), DeleteOf(2), UpdateOf(3, "Missing"));

            Assert.Equal(1, identity.SaveCount);
            Assert.Equal(new[] { true, true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal("New", response.Results[0].Updated.Name);
            Assert.Equal(2, response.Results[1].DeletedId);
            Assert.Equal(new[] { (EntityState.Deleted, 2L, "Gone"), (EntityState.Modified, 1L, "New") },
                identity.Types.OrderBy(t => t.State).ToArray());
        }

        [Fact]
        public async Task UpdateThenDeleteOfOneIdDeletesIt()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old")));

            var response = await Update(db, UpdateOf(1, "New"), DeleteOf(1));

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        [Fact]
        public async Task DeleteThenUpdateKeepsTheDeleteAndFailsTheUpdate()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old")));

            var response = await Update(db, DeleteOf(1), UpdateOf(1, "New"));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[1].Updated);
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        [Fact]
        public async Task RepeatedDeleteFailsTheRepeat()
        {
            var identity = new IdentityOnSave(firstId: 1000);
            using var db = NewContext(identity, new FakeStructureTypeTable(Stored(1, "Old")));

            var response = await Update(db, DeleteOf(1), DeleteOf(1));

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(EntityState.Deleted, Assert.Single(identity.Types).State);
        }

        private static Task<UpdateStructureTypesResponse> Update(AnnotationContext db, params StructureTypeChangeRequest[] changes)
        {
            var request = new UpdateStructureTypesRequest();
            request.Objs.AddRange(changes);
            return new StructureTypeService(db, NullLogger<StructureTypeService>.Instance).Update(request, new TestCallContext());
        }

        private static StructureTypeChangeRequest UpdateOf(long id, string name) =>
            new() { Update = new ProtoStructureType { Id = id, Name = name, Code = "C" } };

        private static StructureTypeChangeRequest DeleteOf(long id) => new() { Delete = id };

        private static EfStructureType Stored(long id, string name) => new()
        {
            Id = id, Name = name, Code = "C", MarkupType = "Point", HotKey = "\0", Username = "stored", Version = new byte[8],
            Created = new DateTime(2020, 1, 1), LastModified = new DateTime(2020, 1, 1),
        };

        private static AnnotationContext NewContext(params IInterceptor[] interceptors) =>
            new(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptors)
                .Options);

        /// <summary>
        /// Suppresses the database write, gives each added structure type the next identity value, leaving
        /// the tracked entity as it would be after a real insert, and records the state of every tracked type.
        /// </summary>
        private sealed class IdentityOnSave(long firstId) : SaveChangesInterceptor
        {
            private long _next = firstId;

            /// <summary>Number of SaveChanges calls seen; the batch must save once.</summary>
            public int SaveCount { get; private set; }

            /// <summary>State, Id and Name of every tracked structure type at the last save.</summary>
            public (EntityState State, long Id, string Name)[] Types { get; private set; } = Array.Empty<(EntityState, long, string)>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                SaveCount++;
                var entries = eventData.Context.ChangeTracker.Entries<EfStructureType>().ToList();
                Types = entries.Select(e => (e.State, e.Entity.Id, e.Entity.Name)).ToArray();
                var added = entries.Where(e => e.State == EntityState.Added).ToList();
                foreach (var entry in added)
                {
                    var id = entry.Property(e => e.Id);
                    id.CurrentValue = _next++;
                    id.IsTemporary = false;
                }

                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(added.Count));
            }
        }

        /// <summary>
        /// Stands in for the StructureType table: opening the connection is suppressed, and each Id lookup
        /// (its one parameter is the Id) is answered with the stored row that has that Id, in the column
        /// order its SELECT list names.
        /// </summary>
        private sealed class FakeStructureTypeTable(params EfStructureType[] rows) : IDbConnectionInterceptor, IDbCommandInterceptor
        {
            private static readonly Regex SelectedColumn = new(@"\]\.\[(\w+)\]");

            private static readonly Dictionary<string, (Type Type, Func<EfStructureType, object> Value)> Columns = new()
            {
                ["ID"] = (typeof(long), t => t.Id),
                ["ParentID"] = (typeof(long), t => t.ParentId),
                ["Abstract"] = (typeof(bool), t => t.Abstract),
                ["Code"] = (typeof(string), t => t.Code),
                ["Color"] = (typeof(int), t => t.Color),
                ["Created"] = (typeof(DateTime), t => t.Created),
                ["HotKey"] = (typeof(string), t => t.HotKey),
                ["LastModified"] = (typeof(DateTime), t => t.LastModified),
                ["MarkupType"] = (typeof(string), t => t.MarkupType),
                ["Name"] = (typeof(string), t => t.Name),
                ["Notes"] = (typeof(string), t => t.Notes),
                ["StructureTags"] = (typeof(string), t => t.StructureTags),
                ["Tags"] = (typeof(string), t => t.Tags),
                ["Username"] = (typeof(string), t => t.Username),
                ["Version"] = (typeof(byte[]), t => t.Version),
            };

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                var id = Convert.ToInt64(command.Parameters.Cast<DbParameter>().Single().Value);
                var selectList = command.CommandText.Substring(0, command.CommandText.IndexOf("FROM", StringComparison.Ordinal));
                var columns = SelectedColumn.Matches(selectList).Select(m => m.Groups[1].Value).ToArray();
                var table = new DataTable();
                foreach (var column in columns)
                    table.Columns.Add(column, Columns[column].Type);
                foreach (var row in rows.Where(r => r.Id == id))
                    table.Rows.Add(columns.Select(c => Columns[c].Value(row) ?? DBNull.Value).ToArray());
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
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
