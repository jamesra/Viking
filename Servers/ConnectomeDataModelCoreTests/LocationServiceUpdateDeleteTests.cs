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
    /// peer twice each writes one row per key (one DeletedLocationLink, one LocationLink) and commits, instead of
    /// failing the whole batch on an EF identity conflict. Also pins the tombstones a plain delete writes: one
    /// DeletedLocationLink per attached link with both sections, none for a link the database already logged, and
    /// no DeletedLocation, which the Location_delete trigger writes (a second writer fails on PK_DeletedLocations).
    /// An update of a location the batch already deleted fails its own row rather than reporting a lost edit.
    /// CreateLocation and CreateLocationLink, which share Update's link helper, save
    /// one link per distinct pair, refuse a self-link, and refuse a missing peer location or parent structure with
    /// NotFound before saving. No database is contacted: interceptors stand in for
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
            Assert.Empty(store.Saved.LocationTombstones);
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
            Assert.Empty(store.Saved.LocationTombstones);
            Assert.Equal(new[] { (1L, 3L) }, store.Saved.DeletedLinks);
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// An edit of a location the batch already deleted would be lost when the delete is saved, so it fails
        /// its own row instead of reporting success; the delete stands.
        /// </summary>
        [Fact]
        public async Task UpdatingALocationTheBatchDeletedFailsThatRow()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Delete(1), Update(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 1L, false), (false, 0L, false) },
                response.Results.Select(r => (r.Success, r.DeletedId, r.Updated != null)).ToArray());
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task UpdatingThenDeletingALocationReportsBoth()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Update(1), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (true, 0L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal(1L, response.Results[0].Updated.Id);
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task UpdatingAStoredLocationSavesTheEdit()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Update(1), Update(8)), new TestCallContext());

            Assert.Equal(new[] { true, false }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal((1L, 12L), (response.Results[0].Updated.Id, response.Results[0].Updated.Section));
            Assert.Equal(EntityState.Modified, db.Entry(db.Locations.Local.Single(l => l.Id == 1)).State);
            Assert.Empty(store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreatingWithARepeatedPeerSavesOneLink()
        {
            var store = new FakeDatabase();
            store.AddLocation(5, z: 11);
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

        [Fact]
        public async Task CreatingStampsTheCallerAndCreationTime()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);
            var callContext = new TestCallContext();
            var request = new UpdateLocationsRequest();
            request.Locations.Add(new LocationChangeRequest { Create = NewLocation() });
            var before = DateTime.UtcNow;

            await NewService(db).Update(request, callContext);

            var saved = db.Locations.Local.Single();
            Assert.Equal(AnnotationRpc.CallerName(callContext), saved.Username);
            Assert.InRange(saved.Created, before, DateTime.UtcNow);
            Assert.Equal(saved.Created, saved.LastModified);
        }

        [Fact]
        public async Task ARowWithNoActionFailsWithoutStoppingTheBatch()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(new LocationChangeRequest(), Delete(1)), new TestCallContext());

            Assert.Equal(new[] { (false, 0L), (true, 1L) }, response.Results.Select(r => (r.Success, r.DeletedId)).ToArray());
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
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
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// Location_delete logs (ID, Z) in the DELETE statement itself, which no EF lookup can see, so a
        /// DeletedLocation the service queued as well would fail the save on PK_DeletedLocations.
        /// </summary>
        [Fact]
        public async Task DeletingALocationLeavesItsTombstoneToTheTrigger()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Delete(1)), new TestCallContext());

            Assert.Equal((true, 1L), (response.Results.Single().Success, response.Results.Single().DeletedId));
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.Empty(store.Saved.LocationTombstones);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task CreateLocationWithARepeatedPeerSavesOneLink()
        {
            var store = new FakeDatabase();
            store.AddLocation(5, z: 11);
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
            store.AddLocation(4, z: 11);
            store.AddLocation(9, z: 12);
            using var db = NewContext(store);

            await NewService(db).CreateLocationLink(new CreateLocationLinkRequest { SourceId = 9, TargetId = 4 }, new TestCallContext());

            Assert.Equal(new[] { (4L, 9L) }, store.Saved.AddedLinks);
            Assert.Equal(1, store.LocationQueries);
        }

        /// <summary>
        /// A link to a location that does not exist would fail the save on FK_LocationLink_Location as a generic
        /// Unknown error, so it is refused with NotFound naming only the missing ends, and nothing is saved.
        /// </summary>
        [Theory]
        [InlineData(8, 4, "Locations not found: 8")]
        [InlineData(4, 9, "Locations not found: 9")]
        [InlineData(9, 8, "Locations not found: 9, 8")]
        public async Task LinkingToAMissingLocationIsNotFoundWithoutSaving(long source, long target, string detail)
        {
            var store = new FakeDatabase();
            store.AddLocation(4, z: 11);
            using var db = NewContext(store);

            var ex = await Assert.ThrowsAsync<RpcException>(() => NewService(db).CreateLocationLink(
                new CreateLocationLinkRequest { SourceId = source, TargetId = target }, new TestCallContext()));

            Assert.Equal((StatusCode.NotFound, detail), (ex.StatusCode, ex.Status.Detail));
            Assert.Same(SavedRows.None, store.Saved);
            Assert.Equal(1, store.LocationQueries);
        }

        /// <summary>A link that is already stored has both ends by its foreign keys, so it costs no location query.</summary>
        [Fact]
        public async Task LinkingAStoredLinkAgainRunsNoLocationQuery()
        {
            var store = new FakeDatabase();
            store.AddLocation(4, z: 11);
            store.AddLocation(9, z: 12);
            store.Links.Add((4, 9));
            using var db = NewContext(store);

            await NewService(db).CreateLocationLink(new CreateLocationLinkRequest { SourceId = 9, TargetId = 4 }, new TestCallContext());

            Assert.Empty(store.Saved.AddedLinks);
            Assert.Equal(0, store.LocationQueries);
        }

        /// <summary>A location under a structure that does not exist would fail FK_Location_StructureBase1 as Unknown.</summary>
        [Fact]
        public async Task CreateLocationUnderAMissingStructureIsNotFoundWithoutSaving()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);
            var orphan = NewLocation();
            orphan.ParentId = 77;

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                NewService(db).CreateLocation(new CreateLocationRequest { Obj = orphan }, new TestCallContext()));

            Assert.Equal((StatusCode.NotFound, "Structure not found: 77"), (ex.StatusCode, ex.Status.Detail));
            Assert.Same(SavedRows.None, store.Saved);
            Assert.False(store.Committed);
            Assert.Empty(db.Locations.Local);
        }

        /// <summary>
        /// A create whose Links name a location that does not exist would save the location, then fail its links on
        /// FK_LocationLink_Location as Unknown. It is refused with NotFound naming each missing peer once, before the
        /// location is added; peer 0 and stored peers are not reported.
        /// </summary>
        [Fact]
        public async Task CreateLocationLinkedToMissingLocationsIsNotFoundWithoutSaving()
        {
            var store = new FakeDatabase();
            store.AddLocation(5, z: 11);
            using var db = NewContext(store);
            var created = NewLocation();
            created.Links.AddRange(new long[] { 0, 8, 5, 9, 8 });

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                NewService(db).CreateLocation(new CreateLocationRequest { Obj = created }, new TestCallContext()));

            Assert.Equal((StatusCode.NotFound, "Locations not found: 8, 9"), (ex.StatusCode, ex.Status.Detail));
            Assert.Same(SavedRows.None, store.Saved);
            Assert.False(store.Committed);
            Assert.Empty(db.Locations.Local);
            Assert.Equal(1, store.LocationQueries);
        }

        /// <summary>A create that names no peer, or only peer 0, cannot break FK_LocationLink_Location, so it costs no location query.</summary>
        [Fact]
        public async Task CreateLocationWithoutPeersRunsNoLocationQuery()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);
            var created = NewLocation();
            created.Links.Add(0);

            var response = await NewService(db).CreateLocation(new CreateLocationRequest { Obj = created }, new TestCallContext());

            Assert.Equal(FakeDatabase.FirstGeneratedId, response.Result.Id);
            Assert.Empty(store.Saved.AddedLinks);
            Assert.Equal(0, store.LocationQueries);
            Assert.Equal(1, store.StructureQueries);
            Assert.True(store.Committed);
        }

        /// <summary>The peer lookup is chunked so no query carries more than SQL Server's 2,100 parameters.</summary>
        [Fact]
        public async Task CreateLocationLinkedToManyPeersLooksThemUpInChunks()
        {
            var store = new FakeDatabase();
            var peers = Enumerable.Range((int)FakeDatabase.FirstGeneratedId + 1, 2500).Select(i => (long)i).ToArray();
            foreach (var peer in peers)
                store.AddLocation(peer, z: 11);
            using var db = NewContext(store);
            var created = NewLocation();
            created.Links.AddRange(peers);

            await NewService(db).CreateLocation(new CreateLocationRequest { Obj = created }, new TestCallContext());

            Assert.Equal(2, store.LocationQueries);
            Assert.Equal(peers.Length, store.Saved.AddedLinks.Length);
            Assert.True(store.Committed);
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

        /// <summary>
        /// A create or a move under a structure that does not exist would fail the save on FK_Location_StructureBase1
        /// and roll back every row, so only those rows fail.
        /// </summary>
        [Fact]
        public async Task CreatingOrMovingUnderAMissingStructureFailsOnlyThoseRows()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            store.AddLocation(2, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1, 2);
            var moved = db.Locations.Local.Single(l => l.Id == 1);
            var orphan = NewLocation();
            orphan.ParentId = 77;

            var response = await NewService(db).Update(
                Request(new LocationChangeRequest { Create = orphan }, Update(1, parentId: 78), new LocationChangeRequest { Create = NewLocation() }, Update(2)),
                new TestCallContext());

            Assert.Equal(new[] { false, false, true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Null(response.Results[0].Created);
            Assert.Null(response.Results[1].Updated);
            Assert.Equal(3L, response.Results[2].Created.ParentId);
            Assert.Equal(3L, response.Results[3].Updated.ParentId);
            Assert.Equal(new[] { 3L }, db.Locations.Local.Where(l => db.Entry(l).State == EntityState.Added).Select(l => l.ParentId));
            Assert.Equal(EntityState.Detached, db.Entry(moved).State);
            Assert.True(store.Committed);
        }

        /// <summary>An update that keeps its stored parent cannot break FK_Location_StructureBase1, so it costs no structure query.</summary>
        [Fact]
        public async Task UpdatingWithoutMovingRunsNoStructureQuery()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            using var db = NewContext(store);
            Track(db, store, 1);

            var response = await NewService(db).Update(Request(Update(1)), new TestCallContext());

            Assert.True(response.Results.Single().Success);
            Assert.Equal(0, store.StructureQueries);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// A create linked to a location that is not stored, or that the batch deletes, would fail the save on
        /// FK_LocationLink_Location and roll back every row, so only that create fails; the rest of the batch,
        /// including a create linked to a stored location, saves. An update writes no links, so its Links are not checked.
        /// </summary>
        [Fact]
        public async Task CreatingLinkedToAMissingOrDeletedLocationFailsOnlyThatRow()
        {
            var store = new FakeDatabase();
            store.AddLocation(1, z: 10);
            store.AddLocation(5, z: 11);
            using var db = NewContext(store);
            Track(db, store, 1, 5);
            var updateNamingAMissingPeer = Update(5);
            updateNamingAMissingPeer.Update.Links.Add(8);

            var response = await NewService(db).Update(
                Request(Create(0, 5), Create(8), Delete(1), Create(0, 1), updateNamingAMissingPeer),
                new TestCallContext());

            Assert.Equal(new[] { true, false, true, false, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(FakeDatabase.FirstGeneratedId, response.Results[0].Created.Id);
            Assert.Null(response.Results[1].Created);
            Assert.Null(response.Results[3].Created);
            Assert.Equal(new[] { (5L, FakeDatabase.FirstGeneratedId) }, store.Saved.AddedLinks);
            Assert.Equal(new[] { 1L }, store.Saved.DeletedLocations);
            Assert.True(store.Committed);
        }

        /// <summary>Creates that name no peers cannot break FK_LocationLink_Location, so they cost no location query.</summary>
        [Fact]
        public async Task CreatingWithoutLinksRunsNoLocationQuery()
        {
            var store = new FakeDatabase();
            using var db = NewContext(store);

            var response = await NewService(db).Update(Request(Create(), Create(0)), new TestCallContext());

            Assert.Equal(new[] { true, true }, response.Results.Select(r => r.Success).ToArray());
            Assert.Equal(0, store.LocationQueries);
            Assert.True(store.Committed);
        }

        private static LocationService NewService(AnnotationContext db) => new(db, NullLogger<LocationService>.Instance);

        private static LocationChangeRequest Create(params long[] peers)
        {
            var created = NewLocation();
            created.Links.AddRange(peers);
            return new() { Create = created };
        }

        private static LocationChangeRequest Delete(long id) => new() { Delete = id };

        private static LocationChangeRequest Update(long id, long parentId = 3)
        {
            var edited = NewLocation();
            edited.Id = id;
            edited.ParentId = parentId;
            return new() { Update = edited };
        }

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
                db.Locations.Attach(new EfLocation { Id = id, ParentId = 3, Z = store.LocationZ[id], Username = "someone" });
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
        /// written. Nothing is accepted, so the last save holds every write of the batch. An added or modified
        /// location whose ParentID is neither in <see cref="Structures"/> nor a tracked structure fails the save with
        /// <see cref="DbUpdateException"/>, as FK_Location_StructureBase1 does, and so does an added link with an end
        /// that is neither a stored nor a tracked location, or that the batch deletes, as FK_LocationLink_Location does.
        /// An added structure whose ParentID is not in <see cref="Structures"/>, or whose TypeID is not in
        /// <see cref="StructureTypes"/>, fails the save the same way, as FK_Structure_Structure and
        /// FK_StructureBase_StructureType do.
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

            /// <summary>Stored structures: the ParentIDs a location may name.</summary>
            public HashSet<long> Structures { get; } = new() { 3 };

            /// <summary>Stored structure types: the TypeIDs a structure may name.</summary>
            public HashSet<long> StructureTypes { get; } = new() { 1 };

            /// <summary>Number of Structure queries run.</summary>
            public int StructureQueries { get; private set; }

            /// <summary>Number of StructureType queries run.</summary>
            public int StructureTypeQueries { get; private set; }

            /// <summary>Number of Location queries run.</summary>
            public int LocationQueries { get; private set; }

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
                var parents = Structures.Concat(tracker.Entries<EfStructure>().Select(e => e.Entity.Id)).ToHashSet();
                var addedStructures = tracker.Entries<EfStructure>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToList();
                if (addedStructures.Any(s => s.ParentId.HasValue && !Structures.Contains(s.ParentId.Value)))
                    throw new DbUpdateException("The INSERT statement conflicted with the FOREIGN KEY SAME TABLE constraint \"FK_Structure_Structure\".");
                if (addedStructures.Any(s => !StructureTypes.Contains(s.TypeId)))
                    throw new DbUpdateException("The INSERT statement conflicted with the FOREIGN KEY constraint \"FK_StructureBase_StructureType\".");
                if (tracker.Entries<EfLocation>().Any(e => (e.State is EntityState.Added or EntityState.Modified) && !parents.Contains(e.Entity.ParentId)))
                    throw new DbUpdateException("The INSERT or UPDATE statement conflicted with the FOREIGN KEY constraint \"FK_Location_StructureBase1\".");
                var deleted = tracker.Entries<EfLocation>().Where(e => e.State == EntityState.Deleted).Select(e => e.Entity.Id).ToHashSet();
                var locations = LocationZ.Keys.Concat(tracker.Entries<EfLocation>().Select(e => e.Entity.Id)).Where(id => !deleted.Contains(id)).ToHashSet();
                if (tracker.Entries<EfLocationLink>().Any(e => e.State == EntityState.Added && !(locations.Contains(e.Entity.A) && locations.Contains(e.Entity.B))))
                    throw new DbUpdateException("The INSERT statement conflicted with the FOREIGN KEY constraint \"FK_LocationLink_Location\".");

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

                if (table == "Structure")
                    StructureQueries++;
                if (table == "StructureType")
                    StructureTypeQueries++;
                if (table == "Location")
                    LocationQueries++;
                var rows = table switch
                {
                    "Structure" => Structures.Where(keys.Contains).Select(id => Row(("ID", id))),
                    "StructureType" => StructureTypes.Where(keys.Contains).Select(id => Row(("ID", id))),
                    "Location" => LocationZ.Where(l => keys.Contains(l.Key))
                        .Select(l => Row(("ID", l.Key), ("Z", l.Value))),
                    "LocationLink" => Links.Where(l => Match(l.A, l.B))
                        .Select(l => Row(("A", l.A), ("B", l.B), ("Created", DateTime.UtcNow), ("Username", "someone"))),
                    "DeletedLocationLinks" => LinkTombstones.Where(l => Match(l.A, l.B))
                        .Select(l => Row(("A", l.A), ("B", l.B), ("AZ", DBNull.Value), ("BZ", DBNull.Value), ("DeletedOn", DateTime.UtcNow))),
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
        internal sealed class FakeCommand : DbCommand
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
        internal sealed class FakeTransaction : DbTransaction
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
