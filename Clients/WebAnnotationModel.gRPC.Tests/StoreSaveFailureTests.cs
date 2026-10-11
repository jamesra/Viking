using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins the failed-save contract shared with the WCF store: <c>Save</c> drains the dirty set before calling
    /// the server, so a failed update must put the objects back or the user's edits are silently lost.
    /// Runs without a server; the server client is a fake whose <c>UpdateAsync</c> always throws.
    /// </summary>
    [TestFixture]
    public class StoreSaveFailureTests
    {
        private StructureStore _structures;
        private ConcurrentDictionary<long, StructureObj> _changed;

        [SetUp]
        public void SetUp()
        {
            var client = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>
                .Create("UpdateAsync", _ => throw new InvalidOperationException("server unavailable"));
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => client);
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<StructureObj, IStructure>>
                .Create("Convert", _ => null);

            _structures = new StructureStore(factory, null, toServer, null, null, null, null);
            _changed = (ConcurrentDictionary<long, StructureObj>)typeof(StructureStore)
                .GetField("ChangedObjects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_structures);
        }

        private StructureObj QueueChange(long id, DBACTION action)
        {
            var obj = new StructureObj(id, 1) { DBAction = action };
            _changed[id] = obj;
            return obj;
        }

        [Test]
        public async Task FailedUpdate_ReturnsFalseAndKeepsChangesQueued()
        {
            var updated = QueueChange(5, DBACTION.UPDATE);
            var deleted = QueueChange(6, DBACTION.DELETE);

            bool ok = await _structures.Save(CancellationToken.None);

            Assert.That(ok, Is.False);
            Assert.That(_changed.Values, Is.EquivalentTo(new[] { updated, deleted }));
        }

        /// <summary>
        /// Client creation fails before any RPC (bad endpoint, channel setup): the exception still reaches the
        /// caller, as in the WCF store's CreateProxy path, but every edit stays queued with its DBAction.
        /// </summary>
        [Test]
        public void ClientCreationFailure_RethrowsAndKeepsChangesQueued()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no channel"));
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<StructureObj, IStructure>>
                .Create("Convert", _ => null);
            var structures = new StructureStore(factory, null, toServer, null, null, null, null);
            var changed = (ConcurrentDictionary<long, StructureObj>)typeof(StructureStore)
                .GetField("ChangedObjects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(structures);

            var inserted = new StructureObj(4, 1) { DBAction = DBACTION.INSERT };
            var updated = new StructureObj(5, 1) { DBAction = DBACTION.UPDATE };
            var deleted = new StructureObj(6, 1) { DBAction = DBACTION.DELETE };
            changed[4] = inserted;
            changed[5] = updated;
            changed[6] = deleted;

            Assert.ThrowsAsync<InvalidOperationException>(() => structures.Save(CancellationToken.None));

            Assert.That(changed.Values, Is.EquivalentTo(new[] { inserted, updated, deleted }));
            Assert.That(inserted.DBAction, Is.EqualTo(DBACTION.INSERT));
            Assert.That(updated.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(deleted.DBAction, Is.EqualTo(DBACTION.DELETE));
        }
    }
}
