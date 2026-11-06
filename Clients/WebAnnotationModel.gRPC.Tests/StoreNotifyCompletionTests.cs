using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Viking.AnnotationServiceTypes;
using Viking.AnnotationServiceTypes.Interfaces;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using WebAnnotationModel.ServerInterface;

namespace WebAnnotationModel.gRPC.Tests
{
    /// <summary>
    /// Pins that the cache-only <c>Add</c> and <c>Remove</c> of the keyed stores complete only after their
    /// <c>OnCollectionChanged</c> listeners have run. With <see cref="State.UseAsynchEvents"/> (the default) listeners
    /// run on the thread pool, so a caller that awaits <c>Remove</c> and then acts on the view, or awaits <c>Add</c> and
    /// selects the new object, must not get ahead of them, and a listener's exception must reach the caller.
    /// Runs without a server; the client factories are never used.
    /// </summary>
    [TestFixture]
    public class StoreNotifyCompletionTests
    {
        private static readonly TimeSpan ListenerTimeout = TimeSpan.FromSeconds(10);

        private bool _savedUseAsynchEvents;

        [SetUp]
        public void SetUp()
        {
            _savedUseAsynchEvents = State.UseAsynchEvents;
            State.UseAsynchEvents = true;
        }

        [TearDown]
        public void TearDown() => State.UseAsynchEvents = _savedUseAsynchEvents;

        [Test]
        public async Task Add_CompletesAfterListener()
        {
            var structures = CreateStructureStore();

            await AssertCompletesAfterListener(structures, NotifyCollectionChangedAction.Add,
                () => structures.Add(new StructureObj(1, 1)));
        }

        [Test]
        public async Task AddCollection_CompletesAfterListener()
        {
            var structures = CreateStructureStore();

            await AssertCompletesAfterListener(structures, NotifyCollectionChangedAction.Add,
                () => structures.Add(new List<StructureObj> { new StructureObj(1, 1), new StructureObj(2, 1) }));
        }

        [Test]
        public async Task RemoveByKey_CompletesAfterListener()
        {
            var structures = CreateStructureStore();
            Seed<long, StructureObj>(structures, 1, new StructureObj(1, 1));

            await AssertCompletesAfterListener(structures, NotifyCollectionChangedAction.Remove,
                () => structures.Remove(1L));
        }

        [Test]
        public async Task RemoveObject_CompletesAfterListener()
        {
            var structures = CreateStructureStore();
            var cached = Seed<long, StructureObj>(structures, 1, new StructureObj(1, 1));

            await AssertCompletesAfterListener(structures, NotifyCollectionChangedAction.Remove,
                () => structures.Remove(cached));
        }

        [Test]
        public async Task PermittedLinkRemove_OfUnsavedLink_CompletesAfterListener()
        {
            var links = CreatePermittedLinkStore();
            var unsaved = new PermittedStructureLinkObj(1, 2, false);
            Assert.That(unsaved.DBAction, Is.EqualTo(DBACTION.INSERT), "the test needs a link that was never saved");
            Seed<PermittedStructureLinkKey, PermittedStructureLinkObj>(links, unsaved.ID, unsaved);

            await AssertCompletesAfterListener(links, NotifyCollectionChangedAction.Remove,
                () => links.Remove(unsaved.ID));
        }

        [Test]
        public async Task LocationRemove_CompletesAfterListener()
        {
            var locations = new LocationStore(null, null, null, null,
                new StructureStore(null, null, null, null, null, null, null), new LocationLinkStore(null, null, null, null));
            var cached = Seed<long, LocationObj>(locations, 5, new LocationObj(5, 2) { DBAction = DBACTION.NONE });

            Task<bool> removal = null;
            await AssertCompletesAfterListener(locations, NotifyCollectionChangedAction.Remove,
                () => removal = locations.Remove(cached));

            Assert.That(await removal, Is.True);
        }

        [Test]
        public async Task LocationLinkDelete_CompletesAfterListener()
        {
            var server = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>>
                .Create("Delete", args => Task.FromResult<LocationLinkKey?>((LocationLinkKey)args[0]));
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<LocationLinkKey, ILocationLink, ILocationLink, ILocationLink>>>
                .Create("GetOrCreate", _ => server);
            var links = new LocationLinkStore(factory, null, null, null);
            Seed<LocationLinkKey, LocationLinkObj>(links, new LocationLinkKey(1, 2), new LocationLinkObj(1, 2));

            await AssertCompletesAfterListener(links, NotifyCollectionChangedAction.Remove,
                () => links.DeleteLink(1, 2));
        }

        [Test]
        public void Add_ListenerException_ReachesCaller()
        {
            var structures = CreateStructureStore();
            structures.OnCollectionChanged += (_, _) => throw new InvalidOperationException("listener failed");

            Assert.That(async () => await structures.Add(new StructureObj(1, 1)),
                Throws.InvalidOperationException.With.Message.EqualTo("listener failed"));
        }

        [Test]
        public void Add_ListenerException_StillQueuesPendingObjectForSave()
        {
            var structures = CreateStructureStore();
            var pending = new StructureObj(1, 1) { DBAction = DBACTION.INSERT };
            structures.OnCollectionChanged += (_, _) => throw new InvalidOperationException("listener failed");

            Assert.That(async () => await structures.Add(pending), Throws.InvalidOperationException);

            // Save reaches the client factory only when something is queued.
            Assert.That(async () => await structures.Save(),
                Throws.InvalidOperationException.With.Message.EqualTo("no server in this test"));
        }

        [Test]
        public void AddCollection_ListenerException_StillQueuesPendingObjectsForSave()
        {
            var structures = CreateStructureStore();
            var pending = new StructureObj(1, 1) { DBAction = DBACTION.INSERT };
            structures.OnCollectionChanged += (_, _) => throw new InvalidOperationException("listener failed");

            Assert.That(async () => await structures.Add(new List<StructureObj> { pending }), Throws.InvalidOperationException);

            Assert.That(async () => await structures.Save(),
                Throws.InvalidOperationException.With.Message.EqualTo("no server in this test"));
        }

        /// <summary>
        /// Holds every listener call of <paramref name="store"/> until the operation's task has been checked, so a task
        /// that completes before its notification has run is caught without relying on timing.
        /// </summary>
        private static async Task AssertCompletesAfterListener(INotifyCollectionChanged store,
            NotifyCollectionChangedAction expected, Func<Task> operation)
        {
            using var release = new ManualResetEventSlim(false);
            int calls = 0;
            store.CollectionChanged += (_, e) =>
            {
                Assert.That(release.Wait(ListenerTimeout), Is.True, "the listener was never released");
                if (e.Action == expected)
                    Interlocked.Increment(ref calls);
            };

            Task pending = operation();
            bool completedBeforeListener = pending.IsCompleted;
            release.Set();
            await pending;

            Assert.That(completedBeforeListener, Is.False, "the store's task completed before its listener ran");
            Assert.That(calls, Is.EqualTo(1));
        }

        /// <summary>Caches <paramref name="obj"/> the way server ingest does, without raising a collection event.</summary>
        private static OBJECT Seed<KEY, OBJECT>(IStoreEditor<KEY, OBJECT> store, KEY key, OBJECT obj) =>
            store.GetOrAdd(key, _ => obj);

        private static StructureStore CreateStructureStore()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<long, IStructure, ICreateStructureAndLocationRequestParameter, ICreateStructureResponseParameter>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no server in this test"));
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<StructureObj, IStructure>>
                .Create("Convert", _ => null);
            return new StructureStore(factory, null, toServer, null, null, null, null);
        }

        private static PermittedStructureLinkStore CreatePermittedLinkStore()
        {
            var factory = StructureStoreRegionQueryTests.FakeInterface<IServerAnnotationsClientFactory<IServerAnnotationsClient<PermittedStructureLinkKey, IPermittedStructureLink, PermittedStructureLinkObj, IPermittedStructureLink>>>
                .Create("GetOrCreate", _ => throw new InvalidOperationException("no server in this test"));
            var toServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<PermittedStructureLinkObj, IPermittedStructureLink>>
                .Create("Convert", _ => null);
            var fromServer = StructureStoreRegionQueryTests.FakeInterface<IObjectConverter<IPermittedStructureLink, PermittedStructureLinkObj>>
                .Create("Convert", _ => null);
            return new PermittedStructureLinkStore(factory, toServer, fromServer, null);
        }
    }
}
