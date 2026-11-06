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
using EfLink = Viking.DataModel.Annotation.PermittedStructureLink;
using ProtoLink = Viking.AnnotationServiceTypes.gRPC.V1.Protos.PermittedStructureLink;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the offline paths of <see cref="PermittedStructureLinksService"/>: a batch saves once, replies
    /// per change in request order, and echoes the composite key. Each change sees the changes before it
    /// in the same batch: a repeated insert, an insert of a stored link or naming a missing type, and an
    /// update or delete of a link the batch deleted fail only their own row, while an insert followed by an update or delete of the
    /// same key applies to the queued row. No database is contacted: interceptors stand in for the
    /// connection, the key lookups and SaveChanges. A single create naming a missing type is refused with NotFound.
    /// </summary>
    public class PermittedStructureLinksServiceUpdateTests
    {
        /// <summary>Unroutable endpoint; the interceptors suppress the connection, queries and save.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task BatchInsertAndNoneReplyInOrderWithOneSave()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());

            var response = await Update(db,
                Change(DBAction.Insert, 1, 2, true),
                new PermittedStructureLinkChange { Action = DBAction.None },
                Change(DBAction.Insert, 3, 4));

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { DBAction.Insert, DBAction.None, DBAction.Insert }, response.Changes.Select(c => c.Action).ToArray());
            Assert.All(response.Changes, c => Assert.True(c.Sucess));
            Assert.Equal((1L, 2L, true), Key(response.Changes[0].Result));
            Assert.Equal((3L, 4L, false), Key(response.Changes[2].Result));
        }

        [Fact]
        public async Task ValidBatchAppliesEveryActionToItsOwnKey()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable(Stored(1, 2, false), Stored(3, 4, true)));

            var response = await Update(db,
                Change(DBAction.Insert, 5, 6, true),
                Change(DBAction.Update, 1, 2, true),
                Change(DBAction.Delete, 3, 4, true),
                new PermittedStructureLinkChange { Action = DBAction.None });

            Assert.Equal(1, saves.Count);
            Assert.All(response.Changes, c => Assert.True(c.Sucess));
            Assert.Equal((5L, 6L, true), Key(response.Changes[0].Result));
            Assert.Equal((1L, 2L, true), Key(response.Changes[1].Result));
            Assert.Equal((3L, 4L, true), Key(response.Changes[2].Result));
            Assert.Equal(new[] { (EntityState.Deleted, 3L, 4L, true), (EntityState.Modified, 1L, 2L, true), (EntityState.Added, 5L, 6L, true) },
                saves.Links.OrderBy(l => l.State).ToArray());
        }

        [Fact]
        public async Task RepeatedInsertQueuesOneRowAndFailsTheRepeat()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());

            var response = await Update(db, Change(DBAction.Insert, 1, 2, true), Change(DBAction.Insert, 1, 2, false));

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { true, false }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Equal((1L, 2L, true), Key(response.Changes[0].Result));
            Assert.Null(response.Changes[1].Result);
            Assert.Equal(new[] { (EntityState.Added, 1L, 2L, true) }, saves.Links);
        }

        [Fact]
        public async Task InsertOfAStoredLinkFailsThatRowOnly()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable(Stored(1, 2, false)));

            var response = await Update(db, Change(DBAction.Insert, 1, 2, true), Change(DBAction.Insert, 3, 4));

            Assert.Equal(new[] { false, true }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Equal(new[] { (EntityState.Unchanged, 1L, 2L, false), (EntityState.Added, 3L, 4L, false) },
                saves.Links.OrderBy(l => l.State).ToArray());
        }

        [Fact]
        public async Task InsertThenUpdateChangesTheQueuedRow()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());

            var response = await Update(db, Change(DBAction.Insert, 1, 2, false), Change(DBAction.Update, 1, 2, true));

            Assert.Equal(new[] { true, true }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Equal((1L, 2L, true), Key(response.Changes[1].Result));
            Assert.Equal(new[] { (EntityState.Added, 1L, 2L, true) }, saves.Links);
        }

        [Fact]
        public async Task InsertThenDeleteQueuesNothing()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());

            var response = await Update(db, Change(DBAction.Insert, 1, 2, true), Change(DBAction.Delete, 1, 2, true));

            Assert.Equal(new[] { true, true }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Equal((1L, 2L, true), Key(response.Changes[1].Result));
            Assert.Empty(saves.Links);
        }

        [Fact]
        public async Task DeleteThenUpdateKeepsTheDeleteAndFailsTheUpdate()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable(Stored(1, 2, false)));

            var response = await Update(db, Change(DBAction.Delete, 1, 2), Change(DBAction.Update, 1, 2, true));

            Assert.Equal(new[] { true, false }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Null(response.Changes[1].Result);
            Assert.Equal(EntityState.Deleted, Assert.Single(saves.Links).State);
        }

        [Fact]
        public async Task DeleteThenInsertReplacesTheStoredRow()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable(Stored(1, 2, false)));

            var response = await Update(db, Change(DBAction.Delete, 1, 2), Change(DBAction.Insert, 1, 2, true));

            Assert.Equal(new[] { true, true }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Equal((1L, 2L, true), Key(response.Changes[1].Result));
        }

        [Fact]
        public async Task SelfLinkIsAnOrdinaryKey()
        {
            // The schema has no self-link check: a type may permit links to its own type (cell to cell).
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());

            var response = await Update(db, Change(DBAction.Insert, 5, 5, true));

            Assert.True(Assert.Single(response.Changes).Sucess);
            Assert.Equal(new[] { (EntityState.Added, 5L, 5L, true) }, saves.Links);
        }

        [Fact]
        public async Task InsertNamingAMissingTypeFailsThatRowOnly()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable(Stored(1, 2, false)));

            var response = await Update(db,
                Change(DBAction.Insert, 3, 4),
                Change(DBAction.Insert, 1, 99),
                Change(DBAction.Insert, 99, 2),
                Change(DBAction.Update, 1, 2, true),
                Change(DBAction.Insert, 4, 5));

            Assert.Equal(1, saves.Count);
            Assert.Equal(new[] { true, false, false, true, true }, response.Changes.Select(c => c.Sucess).ToArray());
            Assert.Null(response.Changes[1].Result);
            Assert.Null(response.Changes[2].Result);
            Assert.Equal(new[] { (EntityState.Modified, 1L, 2L, true), (EntityState.Added, 3L, 4L, false), (EntityState.Added, 4L, 5L, false) },
                saves.Links.OrderBy(l => l.State).ThenBy(l => l.Source).ToArray());
        }

        [Fact]
        public async Task InsertsLookUpTheirTypesInOneQuery()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);

            await Update(db, Change(DBAction.Insert, 1, 2), Change(DBAction.Insert, 2, 3), Change(DBAction.Insert, 3, 1));

            Assert.Equal(1, table.TypeQueries);
        }

        [Fact]
        public async Task BatchWithoutInsertsRunsNoTypeQuery()
        {
            var table = new FakeLinkTable(Stored(1, 2, false), Stored(3, 4, false));
            using var db = NewContext(new CaptureSaves(), table);

            var response = await Update(db, Change(DBAction.Update, 1, 2, true), Change(DBAction.Delete, 3, 4));

            Assert.All(response.Changes, c => Assert.True(c.Sucess));
            Assert.Equal(0, table.TypeQueries);
        }

        [Fact]
        public async Task EmptyBatchRepliesEmptyAndSavesOnce()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable();
            using var db = NewContext(saves, table);

            var response = await Update(db);

            Assert.Empty(response.Changes);
            Assert.Equal(1, saves.Count);
            Assert.Equal(0, table.Queries);
        }

        [Fact]
        public async Task CreateRepliesWithTheStoredLink()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable();
            using var db = NewContext(saves, table);

            var response = await Create(db, 7, 9, true);

            Assert.Equal(1, saves.Count);
            Assert.Equal((7L, 9L, true), Key(response.Result));
            Assert.Equal(new[] { (EntityState.Added, 7L, 9L, true) }, saves.Links);
            Assert.Equal(1, table.TypeQueries);
        }

        [Theory]
        [InlineData(99, 2, "99")]
        [InlineData(1, 98, "98")]
        [InlineData(99, 98, "99, 98")]
        [InlineData(99, 99, "99")]
        public async Task CreateNamingAMissingTypeIsNotFoundAndSavesNothing(long source, long target, string missing)
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable();
            using var db = NewContext(saves, table);

            var e = await Assert.ThrowsAsync<RpcException>(() => Create(db, source, target));

            Assert.Equal(StatusCode.NotFound, e.StatusCode);
            Assert.Equal($"Structure types not found: {missing}", e.Status.Detail);
            Assert.Equal(0, saves.Count);
            Assert.Equal(1, table.TypeQueries);
        }

        private static Task<CreatePermittedStructureLinkResponse> Create(AnnotationContext db, long source, long target, bool bidirectional = false) =>
            new PermittedStructureLinksService(db, NullLogger<PermittedStructureLinksService>.Instance).CreatePermittedStructureLink(
                new CreatePermittedStructureLinkRequest { NewObj = new ProtoLink { SourceTypeId = source, TargetTypeId = target, Bidirectional = bidirectional } },
                new TestCallContext());

        private static Task<UpdatePermittedStructureLinksResponse> Update(AnnotationContext db, params PermittedStructureLinkChange[] changes)
        {
            var request = new UpdatePermittedStructureLinksRequest();
            request.Changes.AddRange(changes);
            return new PermittedStructureLinksService(db, NullLogger<PermittedStructureLinksService>.Instance)
                .UpdatePermittedStructureLinks(request, new TestCallContext());
        }

        private static PermittedStructureLinkChange Change(DBAction action, long source, long target, bool bidirectional = false) =>
            new() { Action = action, Result = new ProtoLink { SourceTypeId = source, TargetTypeId = target, Bidirectional = bidirectional } };

        private static EfLink Stored(long source, long target, bool bidirectional) =>
            new() { SourceTypeId = source, TargetTypeId = target, Bidirectional = bidirectional };

        private static (long Source, long Target, bool Bidirectional) Key(ProtoLink link) =>
            (link.SourceTypeId, link.TargetTypeId, link.Bidirectional);

        private static AnnotationContext NewContext(params IInterceptor[] interceptors) =>
            new(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptors)
                .Options);

        /// <summary>Suppresses the database write, counts SaveChanges calls and records the state and values of every tracked link.</summary>
        private sealed class CaptureSaves : SaveChangesInterceptor
        {
            public int Count { get; private set; }
            public (EntityState State, long Source, long Target, bool Bidirectional)[] Links { get; private set; } =
                Array.Empty<(EntityState, long, long, bool)>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
                Links = eventData.Context.ChangeTracker.Entries<EfLink>()
                    .Select(e => (e.State, e.Entity.SourceTypeId, e.Entity.TargetTypeId, e.Entity.Bidirectional)).ToArray();
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(eventData.Context.ChangeTracker.Entries().Count()));
            }
        }

        /// <summary>
        /// Stands in for the PermittedStructureLink and StructureType tables: opening the connection is
        /// suppressed, a StructureType query is answered with the <see cref="Types"/> among its parameters,
        /// and each link key lookup (its two parameters are the source and target type, in that order) with
        /// the stored row that has that key, in the column order its SELECT list names. Saving an added link
        /// whose source or target is not in <see cref="Types"/> throws, as the foreign keys do.
        /// </summary>
        private sealed class FakeLinkTable : IDbConnectionInterceptor, IDbCommandInterceptor, ISaveChangesInterceptor
        {
            private static readonly Regex SelectedColumn = new(@"\]\.\[(\w+)\]");
            private readonly EfLink[] _rows;

            public FakeLinkTable(params EfLink[] rows) => _rows = rows;

            /// <summary>Stored structure type Ids: 1 to 9, so a test names a missing type with 99.</summary>
            public HashSet<long> Types { get; } = new() { 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            public int Queries { get; private set; }

            public int TypeQueries { get; private set; }

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (eventData.Context.ChangeTracker.Entries<EfLink>().Any(e => e.State == EntityState.Added &&
                        !(Types.Contains(e.Entity.SourceTypeId) && Types.Contains(e.Entity.TargetTypeId))))
                    throw new DbUpdateException("The INSERT statement conflicted with the FOREIGN KEY constraint \"FK_PermittedStructureLink_SourceType\".");
                return ValueTask.FromResult(result);
            }

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Queries++;
                var key = command.Parameters.Cast<DbParameter>().Select(p => Convert.ToInt64(p.Value)).ToArray();
                var selectList = command.CommandText.Substring(0, command.CommandText.IndexOf("FROM", StringComparison.Ordinal));
                var columns = SelectedColumn.Matches(selectList).Select(m => m.Groups[1].Value).ToArray();
                var table = new DataTable();
                if (command.CommandText.Contains("FROM [StructureType]", StringComparison.Ordinal))
                {
                    TypeQueries++;
                    table.Columns.Add(Assert.Single(columns), typeof(long));
                    foreach (var id in key.Distinct().Where(Types.Contains))
                        table.Rows.Add(id);
                    return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
                }

                foreach (var column in columns)
                    table.Columns.Add(column, column == "Bidirectional" ? typeof(bool) : typeof(long));
                foreach (var row in _rows.Where(r => r.SourceTypeId == key[0] && r.TargetTypeId == key[1]))
                    table.Rows.Add(columns.Select(c => ColumnValue(row, c)).ToArray());
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }

            private static object ColumnValue(EfLink row, string column) => column switch
            {
                "SourceTypeID" => row.SourceTypeId,
                "TargetTypeID" => row.TargetTypeId,
                "Bidirectional" => row.Bidirectional,
                _ => throw new InvalidOperationException($"Unexpected PermittedStructureLink column {column}")
            };
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
