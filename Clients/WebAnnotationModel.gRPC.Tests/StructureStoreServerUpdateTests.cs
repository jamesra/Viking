using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins that a structure the store already caches takes the server's newer values when it comes back on the
    /// store's own query paths (here GetChildStructures), as the WCF client's InternalUpdate did. The store must
    /// hand the updater it is constructed with to its server-results handler; without it every cached structure
    /// is reported Unchanged and server edits to label, notes, or ParentID are dropped. Applied server values
    /// must not queue an unedited structure for Save; a pending local edit stays queued.
    /// Runs without a server: the structure repository is a fake and no parent is fetched by ID.
    /// </summary>
    /// <remarks>
    /// Rebinds the process-wide <see cref="Store"/> to an empty structure-type cache, because the converter looks
    /// up each structure's type there once the store is initialized (another fixture may have bound it).
    /// </remarks>
    [TestFixture]
    public class StructureStoreServerUpdateTests
    {
        private const long TypeId = 1;
        private const long ParentId = 10;
        private const long ChildId = 30;

        private IStructure[] _serverReply;
        private StructureStore _structures;

        [SetUp]
        public async Task SetUp()
        {
            var types = new StructureTypeStore(null, null, null, null);
            await Store.InitializeAsync(StructureStoreRegionQueryTests.FakeInterface<IAnnotationStores>
                .Create(nameof(IAnnotationStores.StructureTypes), _ => types));

            var repository = StructureStoreRegionQueryTests.FakeInterface<IStructureRepository>.Create(
                nameof(IStructureRepository.GetChildStructures), _ => Task.FromResult(_serverReply));
            var repositoryFactory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IStructureRepository>>
                .Create("GetOrCreate", _ => repository);
            var clientFactory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no by-ID fetch expected in this test"));
            var links = new StructureLinkStore(null, null, new StructureLinkServerToClientConverter());
            _structures = new StructureStore(clientFactory, repositoryFactory, null, new StructureServerToClientConverter(),
                new StructureServerToClientUpdater(), null, links);
        }

        private Task ServerReturns(params IStructure[] structures)
        {
            _serverReply = structures;
            return _structures.GetChildStructures(ParentId);
        }

        private static Structure ServerStructure(long id, long? parentId, string notes)
        {
            var proto = new Structure
            {
                Id = id,
                TypeId = TypeId,
                Label = "label",
                Notes = notes,
                Username = "user",
                Created = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            };
            if (parentId.HasValue)
                proto.ParentId = parentId.Value;
            return proto;
        }

        private StructureObj Cached(long id)
        {
            Assert.That(_structures.TryGetObjectByID(id, out StructureObj obj), Is.True, $"structure {id} cached");
            return obj;
        }

        [Test]
        public async Task CachedStructure_NewerNotesFromServer_AppliedToCachedInstance()
        {
            await ServerReturns(ServerStructure(ChildId, null, "old"));
            var cached = Cached(ChildId);

            await ServerReturns(ServerStructure(ChildId, null, "new"));

            Assert.That(Cached(ChildId), Is.SameAs(cached));
            Assert.That(cached.Notes, Is.EqualTo("new"));
            Assert.That(cached.DBAction, Is.EqualTo(DBACTION.NONE), "a server value must not be sent back on Save");
        }

        [Test]
        public async Task CachedStructureWithPendingEdit_ServerUpdate_StaysQueuedForSave()
        {
            await ServerReturns(ServerStructure(ChildId, null, "old"));
            var cached = Cached(ChildId);
            cached.SubmitOnNextUpdate();

            await ServerReturns(ServerStructure(ChildId, null, "new"));

            Assert.That(cached.Notes, Is.EqualTo("new"));
            Assert.That(cached.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public async Task CachedRoot_ServerMovesItUnderCachedParent_Rewired()
        {
            await ServerReturns(ServerStructure(ParentId, null, "notes"), ServerStructure(ChildId, null, "notes"));
            Assert.That(_structures.RootObjects, Does.Contain(ChildId));

            await ServerReturns(ServerStructure(ChildId, ParentId, "notes"));

            var parent = Cached(ParentId);
            var child = Cached(ChildId);
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(child.Parent, Is.SameAs(parent));
            Assert.That(parent.Children, Does.Contain(child));
            Assert.That(_structures.RootObjects, Does.Not.Contain(ChildId));
        }
    }
}
