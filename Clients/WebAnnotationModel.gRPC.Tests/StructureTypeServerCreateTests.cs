using System;
using System.Threading;
using System.Threading.Tasks;
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
    /// Pins that a structure type converted from the server mirrors the server: the
    /// <see cref="StructureTypeServerToClientConverter"/> copies the parent id and leaves DBAction
    /// <see cref="DBACTION.NONE"/>, so <c>StructureTypeStore.Create</c> (server echo, then <c>Add</c>) does not queue
    /// the new type for a spurious update on the next Save. Same contract as <see cref="StructureServerCreateParentTests"/>
    /// for structures. Runs without a server; the client factory throws if Save reaches it.
    /// </summary>
    [TestFixture]
    public class StructureTypeServerCreateTests
    {
        private const long ParentId = 10;
        private const long ChildId = 30;

        private static StructureType ServerType(long id, long? parentId)
        {
            var proto = new StructureType
            {
                Id = id,
                Name = "name",
                Notes = "notes",
                Code = "C",
                Color = 0x00FF00FF,
            };
            if (parentId.HasValue)
                proto.ParentId = parentId.Value;
            return proto;
        }

        [Test]
        public void ConvertProto_CopiesParentIdAndStaysNone()
        {
            var obj = new StructureTypeServerToClientConverter().Convert(ServerType(ChildId, ParentId));

            Assert.That(obj.ParentID, Is.EqualTo(ParentId));
            Assert.That(obj.Name, Is.EqualTo("name"));
            Assert.That(obj.Notes, Is.EqualTo("notes"));
            Assert.That(obj.Code, Is.EqualTo("C"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public void ConvertProto_WithoutParent_IsRootAndStaysNone()
        {
            var obj = new StructureTypeServerToClientConverter().Convert(ServerType(ChildId, null));

            Assert.That(obj.ParentID, Is.Null);
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        [Test]
        public void ConvertInterface_CopiesParentIdAndStaysNone()
        {
            IStructureType src = new PlainStructureType { ID = ChildId, ParentID = ParentId, Name = "name", Code = "C" };

            var obj = new StructureTypeServerToClientConverter().Convert(src);

            Assert.That(obj.ParentID, Is.EqualTo(ParentId));
            Assert.That(obj.Name, Is.EqualTo("name"));
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }

        /// <summary>
        /// The Create path: the server's echo goes through the converter and <c>Add</c>. A converted type that is not
        /// NONE would be queued, and the next Save would call the client factory (which throws here).
        /// </summary>
        [Test]
        public async Task ServerEchoAddedToStore_IsNotQueuedForSave()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructureType, IStructureType, IStructureType>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("Save reached the server: a server-created type was queued"));
            var store = new StructureTypeStore(factory, null, null, new StructureTypeServerToClientConverter());

            var created = await store.Add(new StructureTypeServerToClientConverter().Convert(ServerType(ChildId, null)));

            Assert.That(created.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(await store.Save(CancellationToken.None), Is.True);
        }

        /// <summary>A non-proto <see cref="IStructureType"/>, so the converter takes its interface path.</summary>
        private sealed class PlainStructureType : IStructureType
        {
            public long ID { get; set; }
            public long? ParentID { get; set; }
            public string Name { get; set; }
            public string Code { get; set; }
            public string Notes { get; set; }
            public string Attributes { get; set; }
            public bool Abstract { get; set; }
            public uint Color { get; set; }
            public int AllowedShapes { get; set; }
            public IPermittedStructureLink[] PermittedLinks { get; set; } = Array.Empty<IPermittedStructureLink>();
            public bool Equals(IStructureType other) => other != null && other.ID == ID;
        }
    }
}
