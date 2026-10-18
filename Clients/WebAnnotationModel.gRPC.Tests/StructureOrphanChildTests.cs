using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins how the gRPC structure store wires a child that arrives in a server batch before its parent
    /// (for example a region query returns a synapse before its cell is loaded). When the parent arrives in a
    /// later batch, the store must attach the waiting child to <see cref="AnnotationModelObjBaseWithParent{KEY,SERVER_INTERFACE,THISTYPE}.Parent"/>
    /// and to the parent's <see cref="AnnotationModelObjBaseWithParent{KEY,SERVER_INTERFACE,THISTYPE}.Children"/>,
    /// as the WCF client's tree ends up after its InternalAdd fetched the missing parent. A parent in no batch is
    /// fetched by ID after the notify step, like that InternalAdd.
    /// Runs without a server: the notify-step tests never touch the client factory, and the fetch tests use a fake
    /// by-ID client.
    /// </summary>
    [TestFixture]
    public class StructureOrphanChildTests
    {
        private const long TypeId = 1;
        private const long ParentId = 10;
        private const long OtherParentId = 11;
        private const long ChildId = 30;
        private const long SecondChildId = 31;

        private StructureStore _structures;
        private StoreServerQueryResultsHandler<long, StructureObj, IStructure> _handler;

        [SetUp]
        public void SetUp()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no server in this test"));
            _structures = new StructureStore(factory, null, null, null, null, null, null);
            _handler = new StoreServerQueryResultsHandler<long, StructureObj, IStructure>(
                _structures, new StructureServerToClientConverter(), new StructureServerToClientUpdater());
        }

        /// <summary>Runs a server batch through the production handler, converter and updater, then the store's notify step.</summary>
        private async Task ServerSends(long[] deletedIds, params IStructure[] batch)
        {
            var inventory = await _handler.ProcessServerObjects(batch, deletedIds);
            // CallOnCollectionChanged is internal; ProcessorForServerAnnotationSets calls it after each batch.
            MethodInfo notify = typeof(StructureStore).GetMethod("CallOnCollectionChanged",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ChangeInventory<StructureObj>) }, null);
            await (Task)notify.Invoke(_structures, new object[] { inventory });
        }

        private Task ServerSends(params IStructure[] batch) => ServerSends(Array.Empty<long>(), batch);

        private static Structure ServerStructure(long id, long? parentId, string notes = "notes")
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

        /// <summary>
        /// Number of waiting-list entries (one per parent ID plus one per child) the store still holds. Read by
        /// reflection so the tests can show adopted or removed children are not kept alive by the private waiting list.
        /// </summary>
        private int WaitingChildCount()
        {
            FieldInfo field = typeof(StoreBaseWithKeyAndParent<long, StructureObj, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>)
                .GetField("_childrenAwaitingParent", BindingFlags.Instance | BindingFlags.NonPublic);
            var waiting = (System.Collections.Generic.Dictionary<long, System.Collections.Generic.List<StructureObj>>)field.GetValue(_structures);
            return waiting.Count + waiting.Values.Sum(list => list.Count);
        }

        [Test]
        public async Task ChildBatchBeforeParentBatch_ChildAdoptedWhenParentArrives()
        {
            await ServerSends(ServerStructure(ChildId, ParentId));
            Assert.That(Cached(ChildId).Parent, Is.Null, "parent not loaded yet");

            await ServerSends(ServerStructure(ParentId, null));

            var parent = Cached(ParentId);
            var child = Cached(ChildId);
            Assert.That(child.Parent, Is.SameAs(parent));
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(parent.Children, Is.EqualTo(new[] { child }));
            Assert.That(_structures.RootObjects, Is.EqualTo(new[] { ParentId }));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.NONE), "adoption mirrors the server; nothing to save");
            Assert.That(WaitingChildCount(), Is.Zero);
        }

        [Test]
        public async Task TwoChildrenAndRepeatedChildBatch_EachAdoptedOnce()
        {
            await ServerSends(ServerStructure(ChildId, ParentId), ServerStructure(SecondChildId, ParentId));
            await ServerSends(ServerStructure(ChildId, ParentId, notes: "changed"));

            await ServerSends(ServerStructure(ParentId, null));

            var parent = Cached(ParentId);
            Assert.That(parent.Children.Select(c => c.ID), Is.EquivalentTo(new[] { ChildId, SecondChildId }));
            Assert.That(Cached(ChildId).Parent, Is.SameAs(parent));
            Assert.That(Cached(SecondChildId).Parent, Is.SameAs(parent));
        }

        /// <summary>
        /// The notify step alone never calls the server: a child whose parent is in no batch waits, unwired, until
        /// a caller runs the fetch (see the FetchesMissing tests below).
        /// </summary>
        [Test]
        public async Task ParentInNoBatch_NotifyStepAloneLeavesChildWaiting()
        {
            await ServerSends(ServerStructure(ChildId, ParentId));
            await ServerSends(ServerStructure(OtherParentId, null));

            var child = Cached(ChildId);
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(child.Parent, Is.Null);
            Assert.That(Cached(OtherParentId).Children, Is.Empty);
            Assert.That(_structures.RootObjects, Is.EqualTo(new[] { OtherParentId }));
        }

        /// <summary>
        /// A region or section set holding a child whose parent (and grandparent) are in no batch: the processor
        /// fetches the parent by ID after the notify step, the store then fetches the grandparent, and both
        /// waiting children are adopted, as the WCF InternalAdd left the tree.
        /// </summary>
        [Test]
        public async Task AnnotationSetChildWithUncachedAncestors_ProcessorFetchesThemAndWiresTree()
        {
            const long grandparentId = 5;
            var server = new FakeStructureServer(ServerStructure(ParentId, grandparentId), ServerStructure(grandparentId, null));
            var structures = server.CreateStore();
            var processor = CreateProcessor(structures);

            var set = new AnnotationSet();
            set.Structures.Add(ServerStructure(ChildId, ParentId));
            set.Structures.Add(ServerStructure(OtherParentId, null));
            await processor.ProcessServerResult(DateTime.UtcNow, set);

            Assert.That(structures.TryGetObjectByID(ChildId, out StructureObj child), Is.True);
            Assert.That(structures.TryGetObjectByID(ParentId, out StructureObj parent), Is.True);
            Assert.That(structures.TryGetObjectByID(grandparentId, out StructureObj grandparent), Is.True);
            Assert.That(child.Parent, Is.SameAs(parent));
            Assert.That(parent.Parent, Is.SameAs(grandparent));
            Assert.That(parent.Children, Is.EqualTo(new[] { child }));
            Assert.That(grandparent.Children, Is.EqualTo(new[] { parent }));
            Assert.That(structures.RootObjects, Is.EquivalentTo(new[] { OtherParentId, grandparentId }));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.NONE), "adoption mirrors the server; nothing to save");
            Assert.That(server.Requests, Is.EqualTo(new[] { new[] { ParentId }, new[] { grandparentId } }),
                "one by-ID request per generation, each ID asked for once");
        }

        /// <summary>
        /// A by-ID fetch (the path LocationStore uses for a location's parent) also fetches the returned
        /// structure's uncached parent, so a synapse fetched for its location is wired under its cell.
        /// </summary>
        [Test]
        public async Task GetObjectsByIDs_FetchesMissingParentOfReturnedStructure()
        {
            var server = new FakeStructureServer(ServerStructure(ChildId, ParentId), ServerStructure(ParentId, null));
            var structures = server.CreateStore();

            var result = await structures.GetObjectsByIDs(new[] { ChildId });

            Assert.That(result.Found.Select(s => s.ID), Is.EqualTo(new[] { ChildId }));
            Assert.That(structures.TryGetObjectByID(ParentId, out StructureObj parent), Is.True);
            Assert.That(result.Found[0].Parent, Is.SameAs(parent));
            Assert.That(structures.RootObjects, Is.EqualTo(new[] { ParentId }));
        }

        /// <summary>
        /// GetChildStructures returns children whose parent is not cached (the caller asked by ID only): the
        /// store fetches the parent after the notify step, wires the children under it, and still loads the links
        /// embedded on the children.
        /// </summary>
        [Test]
        public async Task GetChildStructures_ParentNotCached_FetchedAndChildrenWired()
        {
            var server = new FakeStructureServer(ServerStructure(ParentId, null));
            var link = new StructureLink { SourceId = ChildId, TargetId = SecondChildId, Username = "user" };
            var repository = StructureStoreRegionQueryTests.FakeInterface<IStructureRepository>.Create(
                nameof(IStructureRepository.GetChildStructures),
                args =>
                {
                    var linked = ServerStructure(ChildId, (long)args[0]);
                    linked.Links.Add(link);
                    return Task.FromResult(new IStructure[] { linked, ServerStructure(SecondChildId, (long)args[0]) });
                });
            var structures = server.CreateStore(StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IStructureRepository>>
                .Create("GetOrCreate", _ => repository));

            var children = await structures.GetChildStructures(ParentId);

            Assert.That(structures.TryGetObjectByID(ParentId, out StructureObj parent), Is.True);
            Assert.That(parent.Children, Is.EquivalentTo(children));
            Assert.That(children.Select(c => c.Parent), Is.All.SameAs(parent));
            Assert.That(server.Requests, Is.EqualTo(new[] { new[] { ParentId } }));
            Assert.That(server.Links.TryGetObjectByID(new StructureLinkKey(ChildId, SecondChildId, false), out _), Is.True, "embedded link loaded");
        }

        /// <summary>
        /// A batch that re-parents a cached structure under an uncached parent reports it in UpdatedObjects; the
        /// fetch covers those as well as added objects.
        /// </summary>
        [Test]
        public async Task FetchMissingParents_UpdatedObjectWithUncachedParent_ParentFetched()
        {
            var server = new FakeStructureServer(ServerStructure(ParentId, null));
            var structures = server.CreateStore();
            var moved = new StructureObj(ChildId, TypeId) { ParentID = ParentId };
            var inventory = new ChangeInventory<StructureObj>();
            inventory.UpdatedObjects.Add(moved);

            await structures.FetchMissingParentsAsync(inventory);

            Assert.That(server.Requests, Is.EqualTo(new[] { new[] { ParentId } }));
            Assert.That(structures.TryGetObjectByID(ParentId, out _), Is.True);
        }

        /// <summary>
        /// Parent missing on the server (deleted meanwhile): asked for once, the child keeps its ParentID and
        /// stays unwired, and the fetch ends instead of asking again.
        /// </summary>
        [Test]
        public async Task ParentNotOnServer_RequestedOnceAndChildStaysUnwired()
        {
            var server = new FakeStructureServer();
            var structures = server.CreateStore();
            var processor = CreateProcessor(structures);

            var set = new AnnotationSet();
            set.Structures.Add(ServerStructure(ChildId, ParentId));
            await processor.ProcessServerResult(DateTime.UtcNow, set);

            Assert.That(structures.TryGetObjectByID(ChildId, out StructureObj child), Is.True);
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(child.Parent, Is.Null);
            Assert.That(server.Requests, Is.EqualTo(new[] { new[] { ParentId } }));
        }

        /// <summary>
        /// Bad server data where the fetched parent names the child's own subtree as its parent (A→B→A). The
        /// fetch must end once every ID was asked for, without asking again.
        /// </summary>
        [Test]
        public async Task FetchedParentsFormCycle_EachRequestedOnce()
        {
            const long grandparentId = 5;
            var server = new FakeStructureServer(ServerStructure(ParentId, grandparentId), ServerStructure(grandparentId, ParentId));
            var structures = server.CreateStore();

            await structures.GetObjectsByIDs(new[] { ParentId });

            Assert.That(server.Requests, Is.EqualTo(new[] { new[] { ParentId }, new[] { grandparentId } }));
            Assert.That(structures.TryGetObjectByID(grandparentId, out StructureObj grandparent), Is.True);
            Assert.That(structures.TryGetObjectByID(ParentId, out StructureObj parent), Is.True);
            Assert.That(parent.Parent, Is.SameAs(grandparent));
        }

        /// <summary>Processor wired to <paramref name="structures"/> and empty location and link stores; no server.</summary>
        private static ProcessorForServerAnnotationSets CreateProcessor(StructureStore structures)
        {
            var locationLinks = new LocationLinkStore(null, null, null, null);
            var locations = new LocationStore(null, null, null, new LocationServerToClientConverter(), structures, locationLinks);
            return new ProcessorForServerAnnotationSets(locations, structures, locationLinks, new StructureLinkStore(null, null, null),
                new LocationServerToClientConverter(), new LocationServerToClientUpdater(),
                new StructureServerToClientConverter(), new StructureServerToClientUpdater(),
                new LocationLinkServerToClientConverter(), new StructureLinkServerToClientConverter());
        }

        /// <summary>
        /// Structure client whose by-ID GetAsync returns the structures it was built with and records each
        /// request. GetOrCreate fails after a few calls so a fetch that never ends fails the test instead of hanging.
        /// </summary>
        private sealed class FakeStructureServer
        {
            private const int MaxClientRequests = 10;
            private readonly IStructure[] _structures;
            private int _clientRequests;

            public FakeStructureServer(params IStructure[] structures) => _structures = structures;

            /// <summary>Link store the created structure store hydrates from embedded links.</summary>
            public StructureLinkStore Links { get; } = new StructureLinkStore(null, null, new StructureLinkServerToClientConverter());

            /// <summary>IDs of each by-ID request, in order.</summary>
            public System.Collections.Generic.List<long[]> Requests { get; } = new System.Collections.Generic.List<long[]>();

            public StructureStore CreateStore(IServerAnnotationsClientFactory<IStructureRepository> repositoryFactory = null)
            {
                var client = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>
                    .Create("GetAsync", args =>
                    {
                        long[] ids = ((System.Collections.Generic.IEnumerable<long>)args[0]).ToArray();
                        Requests.Add(ids);
                        return Task.FromResult<System.Collections.Generic.IList<IStructure>>(_structures.Where(s => ids.Contains(s.ID)).ToList());
                    });
                var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                    .Create("GetOrCreate", _ => ++_clientRequests <= MaxClientRequests
                        ? client
                        : throw new InvalidOperationException("parent fetch did not end"));
                return new StructureStore(factory, repositoryFactory, null, new StructureServerToClientConverter(), null, null, Links);
            }
        }

        [Test]
        public async Task ChildDeletedBeforeParentArrives_NotAdopted()
        {
            await ServerSends(ServerStructure(ChildId, ParentId));
            await ServerSends(new[] { ChildId });
            Assert.That(WaitingChildCount(), Is.Zero, "a removed child is not kept alive by the waiting list");

            await ServerSends(ServerStructure(ParentId, null));

            Assert.That(Cached(ParentId).Children, Is.Empty);
            Assert.That(_structures.TryGetObjectByID(ChildId, out _), Is.False);
        }

        [Test]
        public async Task ChildMovedToCachedParentBeforeOldParentArrives_StaysWithNewParent()
        {
            await ServerSends(ServerStructure(OtherParentId, null));
            await ServerSends(ServerStructure(ChildId, ParentId));
            await ServerSends(ServerStructure(ChildId, OtherParentId));

            await ServerSends(ServerStructure(ParentId, null));

            var child = Cached(ChildId);
            Assert.That(child.Parent, Is.SameAs(Cached(OtherParentId)));
            Assert.That(Cached(ParentId).Children, Is.Empty);
            Assert.That(Cached(OtherParentId).Children, Is.EqualTo(new[] { child }));
        }

        [Test]
        public async Task ChildMovedToAnotherMissingParentBeforeOldParentArrives_NotAdoptedByOldParent()
        {
            const long missingParentId = 12;
            await ServerSends(ServerStructure(ChildId, ParentId));
            await ServerSends(ServerStructure(ChildId, missingParentId));

            await ServerSends(ServerStructure(ParentId, null));

            var child = Cached(ChildId);
            Assert.That(child.Parent, Is.Null);
            Assert.That(child.ParentID, Is.EqualTo(missingParentId), "not rewritten to the old parent's ID");
            Assert.That(Cached(ParentId).Children, Is.Empty);

            await ServerSends(ServerStructure(missingParentId, null));
            Assert.That(child.Parent, Is.SameAs(Cached(missingParentId)));
        }

        [Test]
        public async Task ChildWaitsForParentAsWellAsParentWaitsForGrandparent_BothAdopted()
        {
            const long grandparentId = 5;
            await ServerSends(ServerStructure(ChildId, ParentId));
            await ServerSends(ServerStructure(ParentId, grandparentId));
            await ServerSends(ServerStructure(grandparentId, null));

            var grandparent = Cached(grandparentId);
            var parent = Cached(ParentId);
            Assert.That(parent.Parent, Is.SameAs(grandparent));
            Assert.That(grandparent.Children, Is.EqualTo(new[] { parent }));
            Assert.That(parent.Children, Is.EqualTo(new[] { Cached(ChildId) }));
            Assert.That(_structures.RootObjects, Is.EqualTo(new[] { grandparentId }));
        }

        /// <summary>
        /// Bad server data where A's parent is B and B's parent is A, split across batches. Wiring must finish
        /// without looping or throwing; the resulting two-node cycle is the same tree a single batch already produces.
        /// </summary>
        [Test]
        public void ParentCycleAcrossBatches_CompletesWithoutLooping()
        {
            Assert.That(async () =>
            {
                await ServerSends(ServerStructure(ChildId, ParentId));
                await ServerSends(ServerStructure(ParentId, ChildId));
            }, Throws.Nothing);

            var a = Cached(ChildId);
            var b = Cached(ParentId);
            Assert.That(a.Parent, Is.SameAs(b));
            Assert.That(b.Parent, Is.SameAs(a));
            Assert.That(a.Children, Is.EqualTo(new[] { b }));
            Assert.That(b.Children, Is.EqualTo(new[] { a }));
        }
    }
}
