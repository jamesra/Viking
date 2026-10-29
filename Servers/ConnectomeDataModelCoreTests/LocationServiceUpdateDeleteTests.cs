using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.DataModel.Annotation;
using Xunit;
using EfLocation = Viking.DataModel.Annotation.Location;
using EfLocationLink = Viking.DataModel.Annotation.LocationLink;
using EfStructure = Viking.DataModel.Annotation.Structure;
using ProtoLocation = Viking.AnnotationServiceTypes.gRPC.V1.Protos.Location;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the offline behavior of <see cref="LocationService.Update"/> when one batch touches the same row
    /// twice: deleting two linked locations, deleting one ID twice, or creating a location whose Links list a
    /// peer twice each writes one row per key (one DeletedLocationLink, one DeletedLocation, one LocationLink)
    /// and commits, instead of failing the whole batch on an EF identity conflict. Also pins the tombstones a
    /// plain delete writes today: one DeletedLocationLink per attached link with both sections, none for a link
    /// the database already logged. CreateLocation and CreateLocationLink, which share Update's link helper, save
    /// one link per distinct pair and refuse a self-link. No database is contacted: interceptors stand in for
    /// the connection, the transaction, the queries and SaveChanges.
    /// </summary>
    public class LocationServiceUpdateDeleteTests
    {
        /// <summary>Unroutable endpoint; the interceptors suppress every database call, so no connection is opened.</summary>
        private const string UnreachableConnection =
            "Server=127.0.0.1,1;Database=None;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

        [Fact]
        public async Task DeletingTwoLinkedLocationsLogsTheirLinkOnce()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            store.AddLocation(2, z: 11);
            store.Links.Add((1, 2));
            using var db = NewContext(store);
            Track(db, store, 1, 2);

            var response = await NewService(db).Update(Request(Delete(2), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 2L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal(new[] { (1L, 2L, (long?)10, (long?)11) }, store.Saved.LinkTombstones);
            Assert.Equal(new[] { (1L, (long?)10), (2L, (long?)11) }, store.Saved.LocationTombstones);
            Assert.Equal(new[] { (1L, 2L) }, store.Saved.DeletedLinks);
            Assert.Equal(new[] { 1L, 2L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task DeletingOneIdTwiceLogsItOnce()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            store.AddLocation(3, z: 12);
            store.Links.Add((1, 3));
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Delete(1), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 1L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal(new[] { (1L, 3L, (long?)10, (long?)12) }, store.Saved.LinkTombstones);
            Assert.Equal(new[] { (1L, (long?)10) }, store.Saved.LocationTombstones);
            Assert.Equal(new[] { (1L, 3L) }, store.Saved.DeletedLinks);
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreatingWithARepeatedPeerSavesOneLink()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);
            var created = NewLocation();
            created.Links.Add(5);
            created.Links.Add(5);
            var request = new UpdateLocationsRequest();
            request.Locations.Add(new LocationChangeRequest { Create = created });

            var response = await NewService(db).Update(request, new TestCallContext());

            Assert.True(response.Results.Single().Success);
            Assert.Equal(new[] { (5L, FakeDatabase.FirstGeneratedId) }, store.Saved.AddedLinks);
            Assert.True(store.Committed);
        }

        /// <summary>A link deleted, re-created and deleted again keeps the tombstone the database already holds.</summary>
        [Fact]
        public async Task DeletingALinkTheDatabaseAlreadyLoggedAddsNoTombstone()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            store.AddLocation(2, z: 11);
            store.AddLocation(4, z: 9);
            store.Links.Add((1, 2));
            store.Links.Add((1, 4));
            store.LinkTombstones.Add((1, 2));
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Delete(1)), new TestCallContext());

            Assert.True(response.Results.Single().Success);
            Assert.Equal(new[] { (1L, 4L, (long?)10, (long?)9) }, store.Saved.LinkTombstones);
            Assert.Equal(new[] { (1L, 2L), (1L, 4L) }, store.Saved.DeletedLinks);
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
        }

        [Fact]
        public async Task DeletingAnUnknownIdFailsThatRowOnly()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Delete(8), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (false, 0L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal(new[] { (1L, (long?)10) }, store.Saved.LocationTombstones);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreateLocationWithARepeatedPeerSavesOneLink()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);
            var created = NewLocation();
            created.Links.Add(5);
            created.Links.Add(5);

            var response = await NewService(db).CreateLocation(new CreateLocationRequest { Obj = created }, new TestCallContext());

            Assert.Equal(FakeDatabase.FirstGeneratedId, response.Result.Id);
            Assert.Equal(new[] { (5L, FakeDatabase.FirstGeneratedId) }, store.Saved.AddedLinks);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreateLocationLinkSavesTheLinkLowerIdFirst()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);

            await NewService(db).CreateLocationLink(new CreateLocationLinkRequest { SourceId = 9, TargetId = 4 }, new TestCallContext());

            Assert.Equal(new[] { (4L, 9L) }, store.Saved.AddedLinks);
        }

        [Fact]
        public async Task LinkingALocationToItselfIsRefusedWithoutSaving()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);

            var ex = await Assert.ThrowsAsync<RpcException>(() => NewService(db).CreateLocationLink(
                new CreateLocationLinkRequest { SourceId = 4, TargetId = 4 }, new TestCallContext()));

            Assert.Equal((StatusCode.InvalidArgument, "A location cannot link to itself"), (ex.StatusCode, ex.Status.Detail));
            Assert.Same(SavedRows.None, store.Saved);
        }

        private static LocationService NewService(AnnotationContext db) => new(db, NullLogger<LocationService>.Instance);

        private static LocationChangeRequest Delete(long id) => new() { Delete = id };

        private static UpdateLocationsRequest Request(params LocationChangeRequest[] changes)
        {
            var request = new UpdateLocationsRequest();
            request.Locations.AddRange(changes);
            return request;
        }

        internal static ProtoLocation NewLocation() => new()
        {
            ParentId = 3,
            Section = 12,
            MosaicGeometry = TestShapes.Mosaic("POLYGON ((0 0, 10 0, 10 10, 0 10, 0 0))"),
            VolumeGeometry = TestShapes.Volume("POLYGON ((100 100, 110 100, 110 110, 100 110, 100 100))"),
        };

        /// <summary>
        /// Tracks the stored locations as the service's by-ID query would, so the fake only has to answer
        /// that query with the key (EF returns the tracked instance without reading the shape columns).
        /// </summary>
        private static void Track(AnnotationContext db, FakeDatabase store, params long[] ids)
        {
            foreach (var id in ids)
                db.Locations.Attach(new EfLocation { Id = id, Z = store.LocationZ[id], Username = "someone" });
        }

        internal static AnnotationContext NewContext(FakeDatabase store) =>
            new(new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(UnreachableConnection, o => o.UseNetTopologySuite())
                .AddInterceptors(store)
                .Options);

        /// <summary>What the batch's last SaveChanges would write, by entity type and state.</summary>
        internal sealed record SavedRows(
            (long A, long B, long? Az, long? Bz)[] LinkTombstones,
            (long Id, long? Z)[] LocationTombstones,
            (long A, long B)[] DeletedLinks,
            (long A, long B)[] AddedLinks,
            long[] DeletedLocations)
        {
            public static readonly SavedRows None = new(
                Array.Empty<(long, long, long?, long?)>(), Array.Empty<(long, long?)>(),
                Array.Empty<(long, long)>(), Array.Empty<(long, long)>(), Array.Empty<long>());
        }

        /// <summary>
        /// Stands in for the annotation database. Opening the connection is suppressed; commands and the
        /// transaction are fakes, so EF never touches SqlClient. Queries are answered from the stored rows by
        /// table, matching any row whose key is among the command's parameter values. A full Location row is
        /// answered with its key only, which is enough for a location the test tracked. SaveChanges is
        /// suppressed: it gives each added location or structure a generated ID and records what would be
        /// written. Nothing is accepted, so the last save holds every write of the batch.
        /// </summary>
        internal sealed class FakeDatabase : IDbConnectionInterceptor, IDbCommandInterceptor, IDbTransactionInterceptor, ISaveChangesInterceptor
        {
            /// <summary>The ID the first added location or structure receives.</summary>
            public const long FirstGeneratedId = 1000;

            private static readonly Regex SelectedColumn = new(@"\]\.\[(\w+)\]");
            private static readonly Regex FromTable = new(@"FROM \[(\w+)\]");
            private long _nextId = FirstGeneratedId;

            public Dictionary<long, long> LocationZ { get; } = new();
            public List<(long A, long B)> Links { get; } = new();
            public List<(long A, long B)> LinkTombstones { get; } = new();
            public List<long> LocationTombstones { get; } = new();
            public SavedRows Saved { get; private set; } = SavedRows.None;
            public bool Committed { get; private set; }

            public void AddLocation(long id, long z) => LocationZ[id] = z;

            public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) =>
                InterceptionResult.Suppress();

            public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult.Suppress());

            public InterceptionResult<DbCommand> CommandCreating(CommandCorrelatedEventData eventData, InterceptionResult<DbCommand> result) =>
                InterceptionResult<DbCommand>.SuppressWithResult(new FakeCommand());

            public ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult<DbTransaction>.SuppressWithResult(new FakeTransaction(connection, () => Committed = true)));

            public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                var tracker = eventData.Context.ChangeTracker;
                foreach (var id in tracker.Entries().Where(e => e.State == EntityState.Added && e.Entity is EfLocation or EfStructure)
                             .Select(e => e.Property(nameof(EfLocation.Id))).Where(p => p.IsTemporary))
                    id.CurrentValue = _nextId++;

                Saved = new SavedRows(
                    tracker.Entries<DeletedLocationLink>().Where(e => e.State == EntityState.Added)
                        .Select(e => (e.Entity.A, e.Entity.B, e.Entity.Az, e.Entity.Bz)).OrderBy(t => t).ToArray(),
                    tracker.Entries<DeletedLocation>().Where(e => e.State == EntityState.Added)
                        .Select(e => (e.Entity.Id, e.Entity.Z)).OrderBy(t => t).ToArray(),
                    LinksIn(tracker, EntityState.Deleted),
                    LinksIn(tracker, EntityState.Added),
                    tracker.Entries<EfLocation>().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity.Id).OrderBy(id => id).ToArray());
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(tracker.Entries().Count()));
            }

            private static (long, long)[] LinksIn(Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker tracker, EntityState state) =>
                tracker.Entries<EfLocationLink>().Where(e => e.State == state).Select(e => (e.Entity.A, e.Entity.B)).OrderBy(t => t).ToArray();

            public ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Answer(command).CreateDataReader()));

            private DataTable Answer(DbCommand command)
            {
                var sql = command.CommandText;
                var keys = command.Parameters.Cast<DbParameter>().SelectMany(p => KeyValues(p.Value)).ToHashSet();
                var table = FromTable.Match(sql).Groups[1].Value;
                var both = sql.Contains(" AND ", StringComparison.Ordinal);
                bool Match(long a, long b) => both ? keys.Contains(a) && keys.Contains(b) : keys.Contains(a) || keys.Contains(b);

                var rows = table switch
                {
                    "Location" => LocationZ.Where(l => keys.Contains(l.Key))
                        .Select(l => Row(("ID", l.Key), ("Z", l.Value))),
                    "LocationLink" => Links.Where(l => Match(l.A, l.B))
                        .Select(l => Row(("A", l.A), ("B", l.B), ("Created", DateTime.UtcNow), ("Username", "someone"))),
                    "DeletedLocationLinks" => LinkTombstones.Where(l => Match(l.A, l.B))
                        .Select(l => Row(("A", l.A), ("B", l.B), ("AZ", DBNull.Value), ("BZ", DBNull.Value), ("DeletedOn", DateTime.UtcNow))),
                    "DeletedLocations" => LocationTombstones.Where(keys.Contains)
                        .Select(id => Row(("ID", id), ("DeletedOn", DateTime.UtcNow), ("Z", DBNull.Value))),
                    _ => throw new InvalidOperationException($"Unexpected query: {sql}")
                };

                var answer = new DataTable();
                if (sql.Contains("EXISTS", StringComparison.Ordinal))
                {
                    answer.Columns.Add("Value", typeof(bool));
                    answer.Rows.Add(rows.Any());
                    return answer;
                }

                var selectList = sql.Substring(0, sql.IndexOf("FROM", StringComparison.Ordinal));
                var columns = SelectedColumn.Matches(selectList).Select(m => m.Groups[1].Value).Distinct().ToArray();
                if (table == "Location" && columns.Length > 2)
                    columns = new[] { "ID" }; // a full row; EF reads only the key of a tracked location
                foreach (var column in columns)
                    answer.Columns.Add(column, ColumnType(column));
                foreach (var row in rows)
                    answer.Rows.Add(columns.Select(c => row[c]).ToArray());
                return answer;
            }

            private static Dictionary<string, object> Row(params (string Column, object Value)[] values) =>
                values.ToDictionary(v => v.Column, v => v.Value);

            private static Type ColumnType(string column) => column switch
            {
                "ID" or "A" or "B" or "Z" or "AZ" or "BZ" => typeof(long),
                "Created" or "DeletedOn" => typeof(DateTime),
                "Username" => typeof(string),
                _ => throw new InvalidOperationException($"Unexpected column {column}")
            };

            /// <summary>The ID values a parameter carries: one number, or a JSON array of them for a Contains list.</summary>
            private static IEnumerable<long> KeyValues(object value) => value switch
            {
                long l => new[] { l },
                int i => new[] { (long)i },
                string json when json.StartsWith('[') => JsonSerializer.Deserialize<long[]>(json),
                _ => Array.Empty<long>()
            };
        }

        /// <summary>A command EF can configure (parameters are real SqlParameters) but that never runs.</summary>
        private sealed class FakeCommand : DbCommand
        {
            private readonly SqlCommand _inner = new();

            public override string CommandText { get => _inner.CommandText; set => _inner.CommandText = value; }
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection DbConnection { get; set; }
            protected override DbParameterCollection DbParameterCollection => _inner.Parameters;
            protected override DbTransaction DbTransaction { get; set; }

            public override void Cancel() { }
            public override int ExecuteNonQuery() => throw new NotSupportedException();
            public override object ExecuteScalar() => throw new NotSupportedException();
            public override void Prepare() { }
            protected override DbParameter CreateDbParameter() => _inner.CreateParameter();
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        }

        /// <summary>A transaction on the test's connection that reports its commit and does nothing else.</summary>
        private sealed class FakeTransaction : DbTransaction
        {
            private readonly DbConnection _connection;
            private readonly Action _onCommit;

            public FakeTransaction(DbConnection connection, Action onCommit) => (_connection, _onCommit) = (connection, onCommit);

            public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
            protected override DbConnection DbConnection => _connection;
            public override void Commit() => _onCommit();
            public override void Rollback() { }
        }

        /// <summary>Call context carrying an anonymous HTTP context (as ASP.NET Core hosting supplies) and an uncancelled token.</summary>
        internal sealed class TestCallContext : ServerCallContext
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
