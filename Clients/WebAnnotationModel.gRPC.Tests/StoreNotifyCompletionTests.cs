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
        public async Task StoreWithKeyGetOrAdd_CompletesAfterListener()
        {
            var locations = CreateLocationStore();
            IStoreWithKey<long, LocationObj> store = locations;

            Task<LocationObj> pending = null;
            await AssertCompletesAfterListener(locations, NotifyCollectionChangedAction.Add,
                () => pending = store.GetOrAdd(5, id => new LocationObj(id, 2), out _));

            Assert.That((await pending).ID, Is.EqualTo(5));
        }

        [Test]
        public async Task StoreWithKeyGetOrAdd_OfCachedKey_RaisesNothing()
        {
            var locations = CreateLocationStore();
            var cached = Seed<long, LocationObj>(locations, 5, new LocationObj(5, 2));
            int calls = 0;
            locations.OnCollectionChanged += (_, _) => Interlocked.Increment(ref calls);

            Task<LocationObj> pending = ((IStoreWithKey<long, LocationObj>)locations)
                .GetOrAdd(5, id => new LocationObj(id, 2), out bool added);

            Assert.That(pending.IsCompleted, Is.True);
            Assert.That(await pending, Is.SameAs(cached));
            Assert.That(added, Is.False);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void StoreWithKeyGetOrAdd_ListenerException_ReachesCaller()
        {
            var locations = CreateLocationStore();
            locations.OnCollectionChanged += (_, _) => throw new InvalidOperationException("listener failed");
            IStoreWithKey<long, LocationObj> store = locations;

            Assert.That(async () => await store.GetOrAdd(5, id => new LocationObj(id, 2), out _),
                Throws.InvalidOperationException.With.Message.EqualTo("listener failed"));
        }

        [Test]
        public async Task ParentedStoreWithKeyGetOrAdd_CompletesAfterListener()
        {
            var structures = CreateStructureStore();
            IStoreWithKey<long, StructureObj> store = structures;

            Task<StructureObj> pending = null;
            await AssertCompletesAfterListener(structures, NotifyCollectionChangedAction.Add,
                () => pending = store.GetOrAdd(1, id => new StructureObj(id, 1), out _));

            Assert.That((await pending).ID, Is.EqualTo(1));
        }

        [Test]
        public void ParentedStoreWithKeyGetOrAdd_ListenerException_ReachesCaller()
        {
            var structures = CreateStructureStore();
            structures.OnCollectionChanged += (_, _) => throw new InvalidOperationException("listener failed");
            IStoreWithKey<long, StructureObj> store = structures;

            Assert.That(async () => await store.GetOrAdd(1, id => new StructureObj(id, 1), out _),
                Throws.InvalidOperationException.With.Message.EqualTo("listener failed"));
        }

        [Test]
        public async Task ParentedStoreWithKeyGetOrAdd_WiresParentAndRoots()
        {
            var structures = CreateStructureStore();
            var parent = Seed<long, StructureObj>(structures, 1, new StructureObj(1, 1) { DBAction = DBACTION.NONE });
            IStoreWithKey<long, StructureObj> store = structures;

            StructureObj child = await store.GetOrAdd(2, id => new StructureObj(id, 1) { ParentID = 1 }, out bool childAdded);
            StructureObj root = await store.GetOrAdd(3, id => new StructureObj(id, 1), out bool rootAdded);

            Assert.That(childAdded && rootAdded, Is.True);
            Assert.That(child.Parent, Is.SameAs(parent));
            Assert.That(parent.Children, Does.Contain(child));
            Assert.That(structures.RootObjects, Is.EqualTo(new[] { 3L }));
        }

        [Test]
        public async Task ParentedStoreWithKeyGetOrAdd_OfCachedKey_RaisesNothing()
        {
            var structures = CreateStructureStore();
            var cached = Seed<long, StructureObj>(structures, 1, new StructureObj(1, 1));
            int calls = 0;
            structures.OnCollectionChanged += (_, _) => Interlocked.Increment(ref calls);

            Task<StructureObj> pending = ((IStoreWithKey<long, StructureObj>)structures)
                .GetOrAdd(1, id => new StructureObj(id, 1), out bool added);

            Assert.That(pending.IsCompleted, Is.True);
            Assert.That(await pending, Is.SameAs(cached));
            Assert.That(added, Is.False);
            Assert.That(calls, Is.Zero);
            Assert.That(structures.RootObjects, Is.Empty);
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

        [Test]
        public void SyncGetOrAdd_ListenerException_IsTracedNotThrown()
        {
            var locations = CreateLocationStore();
            const string message = "sync GetOrAdd listener failed";
            locations.OnCollectionChanged += (_, _) => throw new InvalidOperationException(message);

            AssertListenerFaultIsTraced(message, () =>
            {
                LocationObj result = locations.GetOrAdd(5, id => new LocationObj(id, 2), out bool added);
                Assert.That(added, Is.True);
                Assert.That(result.ID, Is.EqualTo(5));
            });
        }

        [Test]
        public void ForgetLocally_ListenerException_IsTracedNotThrown()
        {
            var structures = CreateStructureStore();
            Seed<long, StructureObj>(structures, 1, new StructureObj(1, 1));
            const string message = "ForgetLocally listener failed";
            structures.OnCollectionChanged += (_, _) => throw new InvalidOperationException(message);

            AssertListenerFaultIsTraced(message, () =>
            {
                List<StructureObj> forgotten = structures.ForgetLocally(new[] { 1L });
                Assert.That(forgotten, Has.Count.EqualTo(1));
                Assert.That(structures.Contains(1L), Is.False);
            });
        }

        /// <summary>
        /// Runs a synchronous store call whose listener throws, and checks the call returns normally while the
        /// listener's exception still reaches <see cref="System.Diagnostics.Trace"/> instead of being lost with an
        /// unobserved task.
        /// </summary>
        private static void AssertListenerFaultIsTraced(string listenerMessage, Action syncCall)
        {
            using var traced = new ManualResetEventSlim(false);
            var listener = new MessageTraceListener(listenerMessage, traced);
            System.Diagnostics.Trace.Listeners.Add(listener);
            try
            {
                Assert.That(() => syncCall(), Throws.Nothing);
                Assert.That(traced.Wait(ListenerTimeout), Is.True, "the listener's exception was not traced");
            }
            finally
            {
                System.Diagnostics.Trace.Listeners.Remove(listener);
            }
        }

        private sealed class MessageTraceListener : System.Diagnostics.TraceListener
        {
            private readonly string _expected;
            private readonly ManualResetEventSlim _seen;

            public MessageTraceListener(string expected, ManualResetEventSlim seen)
            {
                _expected = expected;
                _seen = seen;
            }

            public override void Write(string message) { }

            public override void WriteLine(string message) { }

            public override void TraceEvent(System.Diagnostics.TraceEventCache eventCache, string source,
                System.Diagnostics.TraceEventType eventType, int id, string message)
            {
                if (eventType == System.Diagnostics.TraceEventType.Error && message != null && message.Contains(_expected))
                    _seen.Set();
            }
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

        private static LocationStore CreateLocationStore() =>
            new LocationStore(null, null, null, null,
                new StructureStore(null, null, null, null, null, null, null), new LocationLinkStore(null, null, null, null));

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
