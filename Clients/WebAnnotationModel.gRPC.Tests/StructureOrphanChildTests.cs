using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using NUnit.Framework;
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
    /// as the WCF client's tree ends up after its InternalAdd fetched the missing parent.
    /// Runs without a server; the client factory is never used.
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

        [Test]
        public async Task ParentNeverArrives_ChildKeepsParentIdAndStaysUnwired()
        {
            await ServerSends(ServerStructure(ChildId, ParentId));
            await ServerSends(ServerStructure(OtherParentId, null));

            var child = Cached(ChildId);
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(child.Parent, Is.Null);
            Assert.That(Cached(OtherParentId).Children, Is.Empty);
            Assert.That(_structures.RootObjects, Is.EqualTo(new[] { OtherParentId }));
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
