using Grpc.Core;
using gRPCAnnotationService;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Xunit;
using static ConnectomeDataModelCoreTests.LocationServiceUpdateDeleteTests;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins the links <see cref="StructureService.CreateStructure"/> saves for its first location's Links: one
    /// LocationLink per distinct peer, none for peer 0 or a link the database already holds, and a peer listed twice
    /// saves one link and commits instead of failing on an EF identity conflict. A peer that is not a stored location
    /// is refused with NotFound before anything is saved, and so is a ParentId naming no structure or a TypeId naming no
    /// structure type.
    /// Offline, on the database fake of <see cref="LocationServiceUpdateDeleteTests"/>.
    /// </summary>
    public class StructureServiceCreateTests
    {
        /// <summary>The structure is saved first and takes the first generated ID; its location takes the next.</summary>
        private const long NewLocationId = FakeDatabase.FirstGeneratedId + 1;

        [Theory]
        [InlineData(new long[0], new long[0])]
        [InlineData(new long[] { 5 }, new long[] { 5 })]
        [InlineData(new long[] { 5, 5 }, new long[] { 5 })]
        [InlineData(new long[] { 7, 5, 7 }, new long[] { 5, 7 })]
        [InlineData(new long[] { 0, 5 }, new long[] { 5 })]
        [InlineData(new long[] { 2000, 5 }, new long[] { 5, 2000 })]
        public async Task SavesOneLinkPerDistinctPeer(long[] peers, long[] linked)
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);

            var response = await NewService(db).CreateStructure(Request(peers), new TestCallContext());

            Assert.Equal(NewLocationId, response.NewAnnotation.Id);
            Assert.Equal(linked.Select(peer => Ordered(peer, NewLocationId)).ToArray(), store.Saved.AddedLinks);
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task APeerTheDatabaseAlreadyLinksGetsNoSecondLink()
        {
            var store = StoreWithPeers();
            store.Links.Add((5, NewLocationId));
            using var db = NewContext(store);

            await NewService(db).CreateStructure(Request(5, 7, 5), new TestCallContext());

            Assert.Equal(new[] { (7L, NewLocationId) }, store.Saved.AddedLinks);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// A first location whose Links name a location that does not exist would save the structure and the location,
        /// then fail its links on FK_LocationLink_Location as Unknown. It is refused with NotFound naming each missing
        /// peer once, in one query, before the structure is added; peer 0 and stored peers are not reported. The new
        /// location's ID is not assigned when the check runs, so a peer naming it is missing too.
        /// </summary>
        [Theory]
        [InlineData(new long[] { 0, 8, 5, 9, 8 }, "Locations not found: 8, 9")]
        [InlineData(new long[] { 0, NewLocationId, 5 }, "Locations not found: 1001")]
        [InlineData(new long[] { -4, 5 }, "Locations not found: -4")]
        public async Task LinkingToMissingLocationsIsNotFoundWithoutSaving(long[] peers, string detail)
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);

            var ex = await Assert.ThrowsAsync<RpcException>(() => NewService(db).CreateStructure(Request(peers), new TestCallContext()));

            Assert.Equal((StatusCode.NotFound, detail), (ex.StatusCode, ex.Status.Detail));
            Assert.Same(SavedRows.None, store.Saved);
            Assert.False(store.Committed);
            Assert.Empty(db.Structures.Local);
            Assert.Equal(1, store.LocationQueries);
        }

        /// <summary>No peer, only peer 0, or no first location at all cannot break FK_LocationLink_Location, so it costs no location query.</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreatingWithoutPeersRunsNoLocationQuery(bool withLocation)
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);
            var request = Request(0);
            if (!withLocation)
                request.NewAnnotation = null;

            var response = await NewService(db).CreateStructure(request, new TestCallContext());

            Assert.Equal(FakeDatabase.FirstGeneratedId, response.NewStructure.Id);
            Assert.Equal(withLocation, response.NewAnnotation != null);
            Assert.Empty(store.Saved.AddedLinks);
            Assert.Equal(0, store.LocationQueries);
            Assert.True(store.Committed);
        }

        /// <summary>
        /// A ParentId naming no structure would fail the structure's save on FK_Structure_Structure as Unknown. It is
        /// refused with NotFound naming it, in one structure query, before the type, the peers or anything else is
        /// looked at or saved. An explicit ParentId of 0 is a parent like any other.
        /// </summary>
        [Theory]
        [InlineData(99L, true)]
        [InlineData(0L, true)]
        [InlineData(99L, false)]
        public async Task MissingParentIsNotFoundWithoutSaving(long parentId, bool withLocation)
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);
            var request = Request(5);
            request.NewStructure.ParentId = parentId;
            if (!withLocation)
                request.NewAnnotation = null;

            var ex = await Assert.ThrowsAsync<RpcException>(() => NewService(db).CreateStructure(request, new TestCallContext()));

            Assert.Equal((StatusCode.NotFound, $"Structures not found: {parentId}"), (ex.StatusCode, ex.Status.Detail));
            AssertNothingSaved(store, db);
            Assert.Equal((1, 0, 0), (store.StructureQueries, store.StructureTypeQueries, store.LocationQueries));
        }

        /// <summary>
        /// A TypeId naming no structure type, including the proto default 0, would fail the structure's save on
        /// FK_StructureBase_StructureType as Unknown. It is refused with NotFound naming it, in one type query, before
        /// the peers or anything else is looked at or saved.
        /// </summary>
        [Theory]
        [InlineData(42L, true)]
        [InlineData(0L, true)]
        [InlineData(42L, false)]
        public async Task MissingTypeIsNotFoundWithoutSaving(long typeId, bool withLocation)
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);
            var request = Request(5);
            request.NewStructure.TypeId = typeId;
            request.NewStructure.ParentId = 3;
            if (!withLocation)
                request.NewAnnotation = null;

            var ex = await Assert.ThrowsAsync<RpcException>(() => NewService(db).CreateStructure(request, new TestCallContext()));

            Assert.Equal((StatusCode.NotFound, $"Structure type {typeId} not found"), (ex.StatusCode, ex.Status.Detail));
            AssertNothingSaved(store, db);
            Assert.Equal((1, 1, 0), (store.StructureQueries, store.StructureTypeQueries, store.LocationQueries));
        }

        /// <summary>A stored parent and type save the structure under that parent, each looked up once.</summary>
        [Fact]
        public async Task StoredParentAndTypeSaveTheStructureUnderTheParent()
        {
            var store = StoreWithPeers();
            store.StructureTypes.Add(7);
            using var db = NewContext(store);
            var request = Request(5);
            request.NewStructure.ParentId = 3;
            request.NewStructure.TypeId = 7;

            var response = await NewService(db).CreateStructure(request, new TestCallContext());

            Assert.Equal((FakeDatabase.FirstGeneratedId, true, 3L, 7L),
                (response.NewStructure.Id, response.NewStructure.HasParentId, response.NewStructure.ParentId, response.NewStructure.TypeId));
            Assert.Equal((1, 1), (store.StructureQueries, store.StructureTypeQueries));
            Assert.True(store.Committed);
        }

        /// <summary>A root structure cannot break FK_Structure_Structure, so it costs no structure query.</summary>
        [Fact]
        public async Task RootStructureRunsNoParentQuery()
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);

            var response = await NewService(db).CreateStructure(Request(5), new TestCallContext());

            Assert.False(response.NewStructure.HasParentId);
            Assert.Equal((0, 1), (store.StructureQueries, store.StructureTypeQueries));
            Assert.True(store.Committed);
        }

        [Fact]
        public async Task StoredPeersAreLookedUpInOneQuery()
        {
            var store = StoreWithPeers();
            using var db = NewContext(store);

            await NewService(db).CreateStructure(Request(7, 0, 5, 2000), new TestCallContext());

            Assert.Equal(1, store.LocationQueries);
            Assert.Equal(3, store.Saved.AddedLinks.Length);
        }

        private static void AssertNothingSaved(FakeDatabase store, Viking.DataModel.Annotation.AnnotationContext db)
        {
            Assert.Same(SavedRows.None, store.Saved);
            Assert.False(store.Committed);
            Assert.Empty(db.Structures.Local);
        }

        private static StructureService NewService(Viking.DataModel.Annotation.AnnotationContext db) =>
            new(db, NullLogger<StructureService>.Instance);

        /// <summary>The database fake holding the locations the tests link to, which FK_LocationLink_Location requires.</summary>
        private static FakeDatabase StoreWithPeers()
        {
            var store = new FakeDatabase();
            foreach (var peer in new long[] { 5, 7, 2000 })
                store.AddLocation(peer, z: 11);
            return store;
        }

        private static (long A, long B) Ordered(long x, long y) => x < y ? (x, y) : (y, x);

        private static CreateStructureRequest Request(params long[] peers)
        {
            var location = NewLocation();
            location.Links.AddRange(peers);
            return new CreateStructureRequest { NewStructure = new Structure { TypeId = 1 }, NewAnnotation = location };
        }
    }
}
