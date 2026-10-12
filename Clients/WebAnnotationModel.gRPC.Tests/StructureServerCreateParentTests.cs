using System;
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
    /// Pins how a structure first seen in a server batch gets its parent: the gRPC
    /// <see cref="StructureServerToClientConverter"/> must copy the server's parent id so the store wires the new
    /// object under its parent instead of into
    /// <see cref="StoreBaseWithKeyAndParent{KEY,OBJECT,SERVER_OBJECT,CREATION_DATA_PARAM,CREATION_RESULT}.RootObjects"/>
    /// (the WCF client keeps the server's ParentID on first load). The converted object must also stay
    /// <see cref="DBACTION.NONE"/>, because it mirrors the server and must not be sent back on the next Save.
    /// Runs without a server; the client factory is never used.
    /// </summary>
    [TestFixture]
    public class StructureServerCreateParentTests
    {
        private const long TypeId = 1;
        private const long ParentId = 10;
        private const long ChildId = 30;

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
        private async Task ServerSends(params IStructure[] batch)
        {
            var inventory = await _handler.ProcessServerObjects(batch, Array.Empty<long>());
            // CallOnCollectionChanged is internal; ProcessorForServerAnnotationSets calls it after each batch.
            MethodInfo notify = typeof(StructureStore).GetMethod("CallOnCollectionChanged",
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(ChangeInventory<StructureObj>) }, null);
            await (Task)notify.Invoke(_structures, new object[] { inventory });
        }

        private static Structure ServerStructure(long id, long? parentId)
        {
            var proto = new Structure
            {
                Id = id,
                TypeId = TypeId,
                Label = "label",
                Notes = "notes",
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
        public async Task NewChildInLaterBatch_WiredUnderCachedParent()
        {
            await ServerSends(ServerStructure(ParentId, null));
            await ServerSends(ServerStructure(ChildId, ParentId));

            var parent = Cached(ParentId);
            var child = Cached(ChildId);
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(child.Parent, Is.SameAs(parent));
            Assert.That(parent.Children, Does.Contain(child));
            Assert.That(_structures.RootObjects, Does.Not.Contain(ChildId));
            Assert.That(_structures.RootObjects, Does.Contain(ParentId));
        }

        [Test]
        public async Task ParentAndChildInSameBatch_ChildWiredUnderParent()
        {
            await ServerSends(ServerStructure(ParentId, null), ServerStructure(ChildId, ParentId));

            var child = Cached(ChildId);
            Assert.That(child.ParentID, Is.EqualTo(ParentId));
            Assert.That(child.Parent, Is.SameAs(Cached(ParentId)));
            Assert.That(_structures.RootObjects, Does.Not.Contain(ChildId));
        }

        [Test]
        public async Task NewStructureWithoutParent_IsRoot()
        {
            await ServerSends(ServerStructure(ChildId, null));

            var obj = Cached(ChildId);
            Assert.That(obj.ParentID, Is.Null);
            Assert.That(obj.Parent, Is.Null);
            Assert.That(_structures.RootObjects, Does.Contain(ChildId));
        }

        [Test]
        public async Task ServerCreatedStructures_StayUnchangedForSave()
        {
            await ServerSends(ServerStructure(ParentId, null), ServerStructure(ChildId, ParentId));

            Assert.That(Cached(ParentId).DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(Cached(ChildId).DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public void ConvertProto_CopiesParentIdAndStaysNone()
        {
            var obj = new StructureServerToClientConverter().Convert(ServerStructure(ChildId, ParentId));

            Assert.That(obj.ParentID, Is.EqualTo(ParentId));
            Assert.That(obj.Label, Is.EqualTo("label"));
            Assert.That(obj.Notes, Is.EqualTo("notes"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public void ConvertInterface_CopiesParentIdAndStaysNone()
        {
            IStructure src = new PlainStructure { ID = ChildId, TypeID = TypeId, ParentID = ParentId, Label = "label" };

            var obj = new StructureServerToClientConverter().Convert(src);

            Assert.That(obj.ParentID, Is.EqualTo(ParentId));
            Assert.That(obj.Label, Is.EqualTo("label"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public void ConvertInterface_WithoutParent_LeavesParentIdNull()
        {
            IStructure src = new PlainStructure { ID = ChildId, TypeID = TypeId, ParentID = null, Label = "label" };

            var obj = new StructureServerToClientConverter().Convert(src);

            Assert.That(obj.ParentID, Is.Null);
        }

        /// <summary>A non-proto <see cref="IStructure"/>, so the converter takes its interface path.</summary>
        private sealed class PlainStructure : IStructure
        {
            public long ID { get; set; }
            public long? ParentID { get; set; }
            public long TypeID { get; set; }
            public string Label { get; set; }
            public string Attributes { get; set; }
            public string Notes { get; set; }
            public double Confidence { get; set; }
            public string Username => null;
            public long[] ChildIDs => Array.Empty<long>();
            public bool Verified => false;
            public IStructureLink[] Links => Array.Empty<IStructureLink>();
            public bool Equals(IStructure other) => other != null && other.ID == ID;
        }
    }
}
