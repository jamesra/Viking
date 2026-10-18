using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
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
    /// Pins the successful-save contract shared with the WCF store: once the update RPC returns, every saved object
    /// is back to <see cref="DBACTION.NONE"/>, so it is not sent again and, for a structure, its per-field edit
    /// tracking is forgotten and the next server refresh overwrites the fields that were just saved
    /// (<see cref="StructureRefreshPendingEditTests"/> covers the unsaved case).
    /// Runs without a server; the server client is a fake whose <c>UpdateAsync</c> returns an empty result.
    /// </summary>
    [TestFixture]
    public class StoreSaveSuccessTests
    {
        private StructureStore _structures;
        private ConcurrentDictionary<long, StructureObj> _changed;

        [SetUp]
        public void SetUp()
        {
            var client = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>
                .Create("UpdateAsync", _ => Task.FromResult(new UpdateResults<long, IStructure>()));
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => client);
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<StructureObj, IStructure>>
                .Create("Convert", _ => null);

            _structures = new StructureStore(factory, null, toServer, null, null, null, null);
            _changed = (ConcurrentDictionary<long, StructureObj>)typeof(StructureStore)
                .GetField("ChangedObjects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_structures);
        }

        /// <summary>A structure the store holds, as after a load: DBAction NONE and queued for Save when edited.</summary>
        private StructureObj Loaded(long id)
        {
            var obj = _structures.GetOrAdd(id, key => new StructureObj(key, 1), out bool added);
            obj.DBAction = DBACTION.NONE;
            return obj;
        }

        [Test]
        public async Task SuccessfulSave_ResetsEverySavedObjectToNoneAndEmptiesTheDirtySet()
        {
            var updated = Loaded(5);
            updated.Label = "user label";
            var deleted = Loaded(6);
            deleted.DBAction = DBACTION.DELETE;
            Assert.That(_changed.Values, Is.EquivalentTo(new[] { updated, deleted }), "edits queue for Save");

            bool ok = await _structures.Save(CancellationToken.None);

            Assert.That(ok, Is.True);
            Assert.That(updated.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(deleted.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(_changed, Is.Empty);
        }

        [Test]
        public async Task EditThenSave_Refresh_OverwritesTheSavedField()
        {
            var obj = Loaded(7);
            obj.Label = "user label";
            await _structures.Save(CancellationToken.None);

            await new StructureServerToClientUpdater().Update(obj, new Structure
            {
                Id = 7,
                TypeId = 1,
                Label = "server label",
                Notes = string.Empty,
                Username = string.Empty,
                Created = Timestamp.FromDateTime(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                LastModified = Timestamp.FromDateTime(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)),
            });

            Assert.That(obj.Label, Is.EqualTo("server label"), "the edit was saved; it is no longer pending");
            Assert.That(obj.DBAction, Is.EqualTo(DBACTION.NONE));
        }
    }
}
