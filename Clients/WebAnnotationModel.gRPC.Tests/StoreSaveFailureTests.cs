using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
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

        /// <summary>Legacy's LastSaveFault: the failed save's exception is kept for the message the user sees.</summary>
        [Test]
        public async Task FailedUpdate_RecordsTheFault()
        {
            QueueChange(5, DBACTION.UPDATE);

            await _structures.Save(CancellationToken.None);

            Assert.That(_structures.LastSaveFault, Is.InstanceOf<InvalidOperationException>());
            Assert.That(_structures.LastSaveFault.Message, Is.EqualTo("server unavailable"));
        }

        [Test]
        public async Task SaveWithNothingQueued_ClearsTheFault()
        {
            QueueChange(5, DBACTION.UPDATE);
            await _structures.Save(CancellationToken.None);
            _changed.Clear();

            bool ok = await _structures.Save(CancellationToken.None);

            Assert.That(ok, Is.True);
            Assert.That(_structures.LastSaveFault, Is.Null);
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

        /// <summary>
        /// A structure store whose converter throws for the IDs in <paramref name="badIds"/> and whose update RPC
        /// succeeds, recording the IDs of each batch it is sent in <paramref name="sentBatches"/> and each ID the
        /// converter is asked for, in order, in <paramref name="convertAttempts"/>.
        /// </summary>
        private static StructureStore StoreWithUnconvertible(ICollection<long> badIds, List<long[]> sentBatches,
            out ConcurrentDictionary<long, StructureObj> changed, List<long> convertAttempts = null)
        {
            var client = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>
                .Create("UpdateAsync", args =>
                {
                    sentBatches.Add(((IEnumerable<IStructure>)args[0]).Select(s => s.ID).ToArray());
                    return Task.FromResult(new UpdateResults<long, IStructure>());
                });
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => client);
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<StructureObj, IStructure>>
                .Create("Convert", args =>
                {
                    var obj = (StructureObj)args[0];
                    convertAttempts?.Add(obj.ID);
                    if (badIds.Contains(obj.ID))
                        throw new FormatException($"cannot convert {obj.ID}");
                    return new Structure { Id = obj.ID, TypeId = obj.TypeID };
                });

            var store = new StructureStore(factory, null, toServer, null, null, null, null);
            changed = (ConcurrentDictionary<long, StructureObj>)typeof(StructureStore)
                .GetField("ChangedObjects", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(store);
            return store;
        }

        /// <summary>A structure the store holds, edited so it is queued for Save with <paramref name="action"/>.</summary>
        private static StructureObj Edited(StructureStore store, long id, DBACTION action)
        {
            var obj = store.GetOrAdd(id, key => new StructureObj(key, 1), out _);
            obj.DBAction = action;
            return obj;
        }

        /// <summary>
        /// D-SaveOuterCatch: one object the converter rejects is dropped with a trace and the failure still reaches
        /// the caller, but every other edit goes back on the dirty set unchanged and the next Save sends it.
        /// </summary>
        [Test]
        public async Task ConversionFailure_DropsOnlyTheBadObjectAndTheNextSaveSendsTheRest()
        {
            var sent = new List<long[]>();
            var store = StoreWithUnconvertible(new[] { 6L }, sent, out var changed);
            var updated = Edited(store, 5, DBACTION.UPDATE);
            var bad = Edited(store, 6, DBACTION.UPDATE);
            var deleted = Edited(store, 7, DBACTION.DELETE);

            var thrown = Assert.ThrowsAsync<FormatException>(() => store.Save(CancellationToken.None));

            Assert.That(store.LastSaveFault, Is.SameAs(thrown));
            Assert.That(sent, Is.Empty, "nothing is sent in the Save that dropped an object");
            Assert.That(changed.Values, Is.EquivalentTo(new[] { updated, deleted }));
            Assert.That(updated.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(deleted.DBAction, Is.EqualTo(DBACTION.DELETE));
            Assert.That(bad.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(store.Contains(6), Is.True, "a dropped update keeps the cached object");

            Assert.That(await store.Save(CancellationToken.None), Is.True);

            Assert.That(sent.Single(), Is.EquivalentTo(new[] { 5L, 7L }));
            Assert.That(changed, Is.Empty);
            Assert.That(store.LastSaveFault, Is.Null);
        }

        /// <summary>
        /// Every object fails conversion: all are dropped, nothing stays queued, no RPC is sent, and the first
        /// conversion failure is the one rethrown.
        /// </summary>
        [Test]
        public async Task ConversionFailureOfEveryObject_DropsThemAll()
        {
            var sent = new List<long[]>();
            var attempts = new List<long>();
            var store = StoreWithUnconvertible(new[] { 5L, 6L }, sent, out var changed, attempts);
            var first = Edited(store, 5, DBACTION.UPDATE);
            var second = Edited(store, 6, DBACTION.DELETE);

            var thrown = Assert.ThrowsAsync<FormatException>(() => store.Save(CancellationToken.None));

            Assert.That(attempts.Count, Is.EqualTo(2), "every object is tried, not just the first");
            Assert.That(thrown.Message, Is.EqualTo($"cannot convert {attempts[0]}"));

            Assert.That(changed, Is.Empty);
            Assert.That(first.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(second.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(await store.Save(CancellationToken.None), Is.True);
            Assert.That(sent, Is.Empty);
        }

        /// <summary>
        /// A rejected INSERT exists only locally, so dropping it removes it from the store rather than leaving an
        /// orphan the server never heard of; the other edit stays queued.
        /// </summary>
        [Test]
        public void ConversionFailureOfAnInsert_RemovesItFromTheStore()
        {
            var sent = new List<long[]>();
            var store = StoreWithUnconvertible(new[] { 9L }, sent, out var changed);
            var updated = Edited(store, 5, DBACTION.UPDATE);
            var inserted = Edited(store, 9, DBACTION.INSERT);

            Assert.ThrowsAsync<FormatException>(() => store.Save(CancellationToken.None));

            Assert.That(store.Contains(9), Is.False);
            Assert.That(inserted.DBAction, Is.EqualTo(DBACTION.NONE));
            Assert.That(changed.Values, Is.EquivalentTo(new[] { updated }));
            Assert.That(updated.DBAction, Is.EqualTo(DBACTION.UPDATE));
            Assert.That(store.Contains(5), Is.True);
        }
    }
}
