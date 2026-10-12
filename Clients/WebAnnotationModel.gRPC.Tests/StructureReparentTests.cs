using System;
using System.Collections.Generic;
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
    /// Pins how a structure's parent changes reach the gRPC client's tree: a server batch that moves a structure to
    /// another parent (the <see cref="StructureServerToClientUpdater"/> path used by section annotation loads) and a
    /// UI reparent through <see cref="AnnotationModelObjBaseWithParent{KEY,SERVER_INTERFACE,THISTYPE}.Parent"/>.
    /// The child must keep the new ParentID, leave the old parent's Children, join the new parent's Children,
    /// and move in or out of <see cref="StoreBaseWithKeyAndParent{KEY,OBJECT,SERVER_OBJECT,CREATION_DATA_PARAM,CREATION_RESULT}.RootObjects"/>.
    /// Runs without a server; the client factory is never used.
    /// </summary>
    [TestFixture]
    public class StructureReparentTests
    {
        private const long TypeId = 1;
        private const long OldParentId = 10;
        private const long NewParentId = 20;
        private const long ChildId = 30;

        private StructureStore _structures;
        private StoreServerQueryResultsHandler<long, StructureObj, IStructure> _handler;
        private StructureObj _oldParent;
        private StructureObj _newParent;

        [SetUp]
        public async Task SetUp()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no server in this test"));
            _structures = new StructureStore(factory, null, null, null, null, null, null);
            _handler = new StoreServerQueryResultsHandler<long, StructureObj, IStructure>(
                _structures, null, new StructureServerToClientUpdater());

            _oldParent = new StructureObj(OldParentId, TypeId) { DBAction = DBACTION.NONE };
            _newParent = new StructureObj(NewParentId, TypeId) { DBAction = DBACTION.NONE };
            await Load(_oldParent, _newParent);
        }

        /// <summary>Adds objects the way server ingest does (cache, then the store's batch notify that wires the tree).</summary>
        private async Task Load(params StructureObj[] objs)
        {
            var inventory = new ChangeInventory<StructureObj>();
            foreach (var obj in objs)
            {
                _structures.GetOrAdd(obj.ID, _ => obj, out bool added);
                Assert.That(added, Is.True);
                inventory.AddedObjects.Add(obj);
            }

            await Notify(inventory);
        }

        /// <summary>Runs a server batch through the production handler and updater, then the store's notify step.</summary>
        private async Task ServerSends(Structure update)
        {
            var inventory = await _handler.ProcessServerObjects(new IStructure[] { update }, Array.Empty<long>());
            await Notify(inventory);
        }

        private Task Notify(ChangeInventory<StructureObj> inventory)
        {
            // CallOnCollectionChanged is internal; ProcessorForServerAnnotationSets calls it after each batch.
            MethodInfo notify = typeof(StructureStore).GetMethod("CallOnCollectionChanged",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ChangeInventory<StructureObj>) }, null);
            return (Task)notify.Invoke(_structures, new object[] { inventory });
        }

        private static Structure ServerChild(long? parentId)
        {
            var proto = new Structure
            {
                Id = ChildId,
                TypeId = TypeId,
                Created = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            };
            if (parentId.HasValue)
                proto.ParentId = parentId.Value;
            return proto;
        }

        private async Task<StructureObj> LoadChildUnder(long? parentId)
        {
            var child = new StructureObj(ChildId, TypeId) { ParentID = parentId };
            child.DBAction = DBACTION.NONE;
            await Load(child);
            return child;
        }

        [Test]
        public async Task ServerMovesWiredChildToAnotherParent_TreeFollows()
        {
            var child = await LoadChildUnder(OldParentId);
            Assert.That(child.Parent, Is.SameAs(_oldParent), "precondition: the batch wired the cached parent");

            await ServerSends(ServerChild(NewParentId));

            Assert.That(child.ParentID, Is.EqualTo(NewParentId));
            Assert.That(child.Parent, Is.SameAs(_newParent));
            Assert.That(_oldParent.Children, Does.Not.Contain(child));
            Assert.That(_newParent.Children, Does.Contain(child));
            Assert.That(_structures.RootObjects, Does.Not.Contain(ChildId));
        }

        [Test]
        public async Task ServerClearsWiredChildParent_ChildBecomesRoot()
        {
            var child = await LoadChildUnder(OldParentId);

            await ServerSends(ServerChild(null));

            Assert.That(child.ParentID, Is.Null);
            Assert.That(child.Parent, Is.Null);
            Assert.That(_oldParent.Children, Does.Not.Contain(child));
            Assert.That(_structures.RootObjects, Does.Contain(ChildId));
        }

        [Test]
        public async Task ServerGivesRootAParent_LeavesRootObjects()
        {
            var child = await LoadChildUnder(null);
            Assert.That(_structures.RootObjects, Does.Contain(ChildId), "precondition");

            await ServerSends(ServerChild(OldParentId));

            Assert.That(child.ParentID, Is.EqualTo(OldParentId));
            Assert.That(child.Parent, Is.SameAs(_oldParent));
            Assert.That(_oldParent.Children, Does.Contain(child));
            Assert.That(_structures.RootObjects, Does.Not.Contain(ChildId));
        }

        [Test]
        public async Task SettingParentIdOnWiredChild_KeepsValueAndLeavesOldParent()
        {
            var child = await LoadChildUnder(OldParentId);
            var changing = new List<string>();
            var changed = new List<string>();
            child.PropertyChanging += (_, e) => changing.Add(e.PropertyName);
            child.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            child.ParentID = NewParentId;

            Assert.That(child.ParentID, Is.EqualTo(NewParentId));
            Assert.That(child.Parent, Is.Null, "the cached parent no longer matches ParentID");
            Assert.That(_oldParent.Children, Does.Not.Contain(child));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(changing, Does.Contain(nameof(StructureObj.ParentID)).And.Contain(nameof(StructureObj.Parent)));
            Assert.That(changed, Does.Contain(nameof(StructureObj.ParentID)).And.Contain(nameof(StructureObj.Parent)));
        }

        [Test]
        public async Task SettingParentIdToSameValue_KeepsCachedParent()
        {
            var child = await LoadChildUnder(OldParentId);

            child.ParentID = OldParentId;

            Assert.That(child.Parent, Is.SameAs(_oldParent));
            Assert.That(_oldParent.Children, Does.Contain(child));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public async Task AssigningNewParentObject_MovesChildAndMarksUpdate()
        {
            var child = await LoadChildUnder(OldParentId);

            child.Parent = _newParent;

            Assert.That(child.ParentID, Is.EqualTo(NewParentId));
            Assert.That(child.Parent, Is.SameAs(_newParent));
            Assert.That(_oldParent.Children, Does.Not.Contain(child));
            Assert.That(_newParent.Children, Does.Contain(child));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.UPDATE));
        }

        [Test]
        public void StructureTypeParentId_OnWiredChild_KeepsValueAndLeavesOldParent()
        {
            var oldParent = new StructureTypeObj(OldParentId) { DBAction = DBACTION.NONE };
            var child = new StructureTypeObj(ChildId) { ParentID = OldParentId };
            child.Parent = oldParent;
            child.DBAction = DBACTION.NONE;
            var changing = new List<string>();
            var changed = new List<string>();
            child.PropertyChanging += (_, e) => changing.Add(e.PropertyName);
            child.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            child.ParentID = NewParentId;

            Assert.That(child.ParentID, Is.EqualTo(NewParentId));
            Assert.That(child.Parent, Is.Null);
            Assert.That(oldParent.Children, Does.Not.Contain(child));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(changing, Does.Contain(nameof(StructureTypeObj.ParentID)));
            Assert.That(changed, Does.Contain(nameof(StructureTypeObj.ParentID)));
        }

        /// <summary>
        /// UI unlink (<c>Parent = null</c>) makes the structure a root: ParentID must be null, never 0, because the
        /// Structure table's ParentID is a foreign key and 0 matches no row. The save request must omit ParentId.
        /// </summary>
        [Test]
        public async Task DetachingParentObject_LeavesNullParentIdAndSendsNoParent()
        {
            var child = await LoadChildUnder(OldParentId);

            child.Parent = null;

            Assert.That(child.ParentID, Is.Null);
            Assert.That(child.Parent, Is.Null);
            Assert.That(_oldParent.Children, Does.Not.Contain(child));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(new StructureClientToServerConverter().Convert(child).HasParentId, Is.False);
        }

        /// <summary>Same contract for structure types, which share the base Parent setter and a nullable ParentID.</summary>
        [Test]
        public void DetachingStructureTypeParentObject_LeavesNullParentIdAndSendsNoParent()
        {
            var oldParent = new StructureTypeObj(OldParentId) { DBAction = DBACTION.NONE };
            var child = new StructureTypeObj(ChildId) { ParentID = OldParentId };
            child.Parent = oldParent;
            child.DBAction = DBACTION.NONE;

            child.Parent = null;

            Assert.That(child.ParentID, Is.Null);
            Assert.That(oldParent.Children, Does.Not.Contain(child));
            Assert.That(child.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(new StructureTypeClientToServerConverter().Convert(child).HasParentId, Is.False);
        }
    }
}
