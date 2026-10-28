using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// Pins the offline behavior of <see cref="StructureService.CreateStructureLink"/> and
    /// <see cref="StructureService.UpdateLinks"/>: a link from a structure to itself is refused with
    /// InvalidArgument before anything is queried or saved; a valid link is saved once, stamped with the
    /// caller's name rather than the payload's; UpdateLinks reads existing links in one query, keeps the
    /// last row of a repeated (SourceId, TargetId) pair, and updates a link that exists instead of inserting it.
    /// No database is contacted: interceptors stand in for the connection, the query and SaveChanges.
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

        [Fact]
        public async Task UpdateLinksRefusesASelfLinkBeforeAnyQueryOrSave()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable();
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            var ex = await Assert.ThrowsAsync<RpcException>(() => service.UpdateLinks(
                Request(Link(1, 2), Link(5, 5), Link(3, 4)), new TestCallContext()));

            Assert.Equal(StatusCode.InvalidArgument, ex.StatusCode);
            Assert.Equal("A structure cannot link to itself", ex.Status.Detail);
            Assert.Equal(0, table.Queries);
            Assert.Equal(0, saves.Count);
        }

        [Fact]
        public async Task UpdateLinksKeepsTheLastRowOfARepeatedPair()
        {
            var saves = new CaptureSaves();
            using var db = NewContext(saves, new FakeLinkTable());
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(
                Request(Link(3, 8, false, "a"), Link(3, 9, false, "c"), Link(3, 8, true, "b")),
                new TestCallContext());

            Assert.Equal(1, saves.Count);
            Assert.Equal(
                new[] { (EntityState.Added, 3L, 8L, true, "b"), (EntityState.Added, 3L, 9L, false, "c") },
                saves.Links.OrderBy(l => l.Target).ToArray());
        }

        [Fact]
        public async Task UpdateLinksLooksUpExistingLinksInOneQuery()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(1, 2), Link(1, 3), Link(4, 5)), new TestCallContext());

            Assert.Equal(1, table.Queries);
        }

        [Fact]
        public async Task UpdateLinksUpdatesAnExistingLinkInPlace()
        {
            var created = new DateTime(2020, 1, 2, 3, 4, 5);
            var saves = new CaptureSaves();
            var table = new FakeLinkTable(new EfStructureLink
            {
                SourceId = 3, TargetId = 8, Bidirectional = false, Tags = "old", Username = "someone", Created = created, LastModified = created
            });
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(3, 8, true, "new")), new TestCallContext());

            Assert.Equal(new[] { (EntityState.Modified, 3L, 8L, true, "new") }, saves.Links);
            Assert.Equal(new[] { "unknown" }, saves.Usernames);
            var saved = db.ChangeTracker.Entries<EfStructureLink>().Single().Entity;
            Assert.Equal(created, saved.Created);
            Assert.True(saved.LastModified > created);
        }

        /// <summary>
        /// The batch query may return a link that shares a source with one requested pair and a target with
        /// another (the fake returns every stored row, as that over-fetch would); such a link is not requested
        /// and must be left alone, while both requested pairs are inserted.
        /// </summary>
        [Fact]
        public async Task UpdateLinksLeavesAnUnrequestedLinkFromTheQueryAlone()
        {
            var saves = new CaptureSaves();
            var table = new FakeLinkTable(new EfStructureLink
            {
                SourceId = 3, TargetId = 9, Bidirectional = false, Tags = "keep", Username = "someone", Created = DateTime.UtcNow, LastModified = DateTime.UtcNow
            });
            using var db = NewContext(saves, table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(Request(Link(3, 8, true, "x"), Link(4, 9, true, "y")), new TestCallContext());

            Assert.Equal(
                new[] { (EntityState.Unchanged, 3L, 9L, false, "keep"), (EntityState.Added, 3L, 8L, true, "x"), (EntityState.Added, 4L, 9L, true, "y") },
                saves.Links.OrderBy(l => l.State).ThenBy(l => l.Source).ThenBy(l => l.Target).ToArray());
        }

        [Fact]
        public async Task UpdateLinksWithNoRowsQueriesNothing()
        {
            var table = new FakeLinkTable();
            using var db = NewContext(new CaptureSaves(), table);
            var service = new StructureService(db, NullLogger<StructureService>.Instance);

            await service.UpdateLinks(new UpdateStructureLinksRequest(), new TestCallContext());

            Assert.Equal(0, table.Queries);
        }

        private static UpdateStructureLinksRequest Request(params ProtoStructureLink[] links)
        {
            var request = new UpdateStructureLinksRequest();
            request.Objs.AddRange(links);
            return request;
        }

        private static ProtoStructureLink Link(long source, long target, bool bidirectional = false, string tags = "") =>
            new ProtoStructureLink { SourceId = source, TargetId = target, Bidirectional = bidirectional, Tags = tags };

        private static AnnotationContext NewContext(params IInterceptor[] interceptors) =>
            new AnnotationContext(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(interceptors)
                .Options);

        /// <summary>
        /// Suppresses the database write, counts SaveChanges calls and records the links being saved: the
        /// usernames of the added and modified ones, and the state and values of every tracked one.
        /// </summary>
        private sealed class CaptureSaves : SaveChangesInterceptor
        {
            public int Count { get; private set; }
            public string[] Usernames { get; private set; } = Array.Empty<string>();
            public (EntityState State, long Source, long Target, bool Bidirectional, string Tags)[] Links { get; private set; } =
                Array.Empty<(EntityState, long, long, bool, string)>();

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Count++;
                var entries = eventData.Context.ChangeTracker.Entries<EfStructureLink>().ToArray();
                Usernames = entries.Where(e => e.State is EntityState.Added or EntityState.Modified).Select(e => e.Entity.Username).ToArray();
                Links = entries.Select(e => (e.State, e.Entity.SourceId, e.Entity.TargetId, e.Entity.Bidirectional, e.Entity.Tags)).ToArray();
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(eventData.Context.ChangeTracker.Entries().Count()));
            }
        }

        /// <summary>
        /// Stands in for the StructureLink table: opening the connection is suppressed, and every query is
        /// answered with all stored links, whatever its WHERE clause says, in the column order its SELECT list
        /// names. Counts the queries.
        /// </summary>
        private sealed class FakeLinkTable : IDbConnectionInterceptor, IDbCommandInterceptor
        {
            private static readonly Regex SelectedColumn = new(@"\]\.\[(\w+)\]");
            private readonly EfStructureLink[] _rows;

            public FakeLinkTable(params EfStructureLink[] rows) => _rows = rows;

            public int Queries { get; private set; }

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Queries++;
                var selectList = command.CommandText.Substring(0, command.CommandText.IndexOf("FROM", StringComparison.Ordinal));
                var columns = SelectedColumn.Matches(selectList).Select(m => m.Groups[1].Value).ToArray();
                var table = new DataTable();
                foreach (var column in columns)
                    table.Columns.Add(column, ColumnType(column));
                foreach (var row in _rows)
                    table.Rows.Add(columns.Select(c => ColumnValue(row, c)).ToArray());
                return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(table.CreateDataReader()));
            }

            private static Type ColumnType(string column) => column switch
            {
                "SourceID" or "TargetID" => typeof(long),
                "Bidirectional" => typeof(bool),
                "Tags" or "Username" => typeof(string),
                "Created" or "LastModified" => typeof(DateTime),
                _ => throw new InvalidOperationException($"Unexpected StructureLink column {column}")
            };

            private static object ColumnValue(EfStructureLink row, string column) => column switch
            {
                "SourceID" => row.SourceId,
                "TargetID" => row.TargetId,
                "Bidirectional" => row.Bidirectional,
                "Tags" => row.Tags,
                "Username" => row.Username,
                "Created" => row.Created,
                "LastModified" => row.LastModified,
                _ => throw new InvalidOperationException($"Unexpected StructureLink column {column}")
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
