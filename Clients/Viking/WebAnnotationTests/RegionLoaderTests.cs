using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WebAnnotationModel;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins <see cref="RegionLoader{KEY, OBJECT}"/> and <see cref="RegionRequestData{OBJECT}"/>, the per-cell
    /// cache that decides when the annotation overlay re-queries the server. The store is a fake whose
    /// server requests complete only when the test says so, so no test depends on timing.
    /// </summary>
    [TestClass]
    public class RegionLoaderTests
    {
        private sealed class Item { }

        /// <summary>One server request the loader issued to the fake store.</summary>
        private sealed class IssuedQuery(long section, DateTime? lastQueryUtc, Action<ICollection<Item>> onLoaded, TaskCompletionSource<bool> gate)
        {
            public readonly long Section = section;
            public readonly DateTime? LastQueryUtc = lastQueryUtc;
            private readonly Action<ICollection<Item>> OnLoaded = onLoaded;
            private readonly TaskCompletionSource<bool> Gate = gate;

            /// <summary>Completes the request the way the real store does: finish the async result, then call back.</summary>
            public void Complete(ICollection<Item>? objects)
            {
                Gate.TrySetResult(true);
                OnLoaded(objects!);
            }
        }

        private sealed class FakeRegionQuery : IRegionQuery<long, Item>
        {
            public readonly List<IssuedQuery> Queries = [];
            public ICollection<Item> KnownObjects = [];
            public bool StartServerRequest = true;
            public int LocalLookups;

            public ICollection<Item> GetLocalObjectsInRegion(long SectionNumber, Rectangle bounds, double MinRadius)
            {
                Interlocked.Increment(ref LocalLookups);
                return KnownObjects;
            }

            public ICollection<Item> GetObjectsInRegion(long SectionNumber, Rectangle bounds, double MinRadius, DateTime? LastQueryUtc) => KnownObjects;

            public MixedLocalAndRemoteQueryResults<long, Item> GetObjectsInRegionAsync(long SectionNumber, Rectangle bounds, double MinRadius, DateTime? LastQueryUtc, Action<ICollection<Item>> OnLoadedCallback, CancellationToken token = default)
            {
                if (!StartServerRequest)
                    return new(null!, KnownObjects);

                TaskCompletionSource<bool> gate = new();
                Queries.Add(new IssuedQuery(SectionNumber, LastQueryUtc, OnLoadedCallback, gate));
                // A Task is an IAsyncResult whose IsCompleted flips when the gate is released.
                return new(gate.Task, KnownObjects);
            }
        }

        private const int Section = 7;

        // A box well inside one 6000 x 6000 cell (level 1 at pixel size 1: 2000 * 3), and a box straddling the x = 6000 cell border.
        private static readonly Rectangle OneCell = new(100, 500, 100, 500);
        private static readonly Rectangle TwoCells = new(5900, 6100, 100, 500);

        private static RegionLoader<long, Item> NewLoader(FakeRegionQuery store) => new(store);

        private static void Load(RegionLoader<long, Item> loader, Rectangle? bounds, Action<ICollection<Item>>? server, Action<ICollection<Item>>? cached = null, CancellationToken token = default) =>
            loader.LoadSectionAnnotationsInRegion(bounds, 1.0, Section, server!, cached!, token);

        /// <summary>Collects callback invocations (they run via Task.Run) and waits for a count without a fixed sleep.</summary>
        private sealed class CallbackProbe(int expected)
        {
            private readonly CountdownEvent done = new(expected);
            public readonly ConcurrentBag<ICollection<Item>> Received = [];

            public void Callback(ICollection<Item> items)
            {
                Received.Add(items);
                if (!done.IsSet)
                    done.Signal();
            }

            /// <summary>Waits for the expected number of invocations; returns false on timeout.</summary>
            public bool WaitForAll() => done.Wait(TimeSpan.FromSeconds(10));

            /// <summary>Short wait used only to show a callback did NOT run; cannot cause a false failure.</summary>
            public bool AnyArrivesWithinGrace() => done.Wait(TimeSpan.FromMilliseconds(300));
        }

        [TestMethod]
        public void NullBounds_IssuesNoQuery()
        {
            FakeRegionQuery store = new();
            Load(NewLoader(store), null, _ => { });
            Assert.AreEqual(0, store.Queries.Count);
        }

        [TestMethod]
        public void FirstLoad_IssuesOneQueryPerCoveredCell_WithNoPriorQueryTime()
        {
            FakeRegionQuery one = new();
            Load(NewLoader(one), OneCell, _ => { });
            Assert.AreEqual(1, one.Queries.Count);
            Assert.IsNull(one.Queries[0].LastQueryUtc);
            Assert.AreEqual(Section, one.Queries[0].Section);

            FakeRegionQuery two = new();
            Load(NewLoader(two), TwoCells, _ => { });
            Assert.AreEqual(2, two.Queries.Count);
        }

        [TestMethod]
        public void RepeatLoadWhileOutstanding_DoesNotIssueAnotherQuery()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            Load(loader, OneCell, _ => { });
            Assert.AreEqual(1, store.Queries.Count);
        }

        [TestMethod]
        public void SecondCallerDuringOutstandingQuery_ReceivesResultsOnceAlongsideFirst()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            CallbackProbe first = new(1), second = new(1);

            Load(loader, OneCell, first.Callback);
            Load(loader, OneCell, second.Callback);
            Item[] loaded = [new(), new()];
            store.Queries[0].Complete(loaded);

            Assert.IsTrue(first.WaitForAll());
            Assert.IsTrue(second.WaitForAll());
            Assert.AreEqual(1, first.Received.Count);
            Assert.AreEqual(1, second.Received.Count);
            CollectionAssert.AreEqual(loaded, first.Received.Single().ToArray());
            CollectionAssert.AreEqual(loaded, second.Received.Single().ToArray());
            Assert.AreEqual(1, store.Queries.Count);
        }

        [TestMethod]
        public void SecondCallerWithNullCallbackDuringOutstandingQuery_IsIgnored()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            CallbackProbe first = new(1);

            Load(loader, OneCell, first.Callback);
            Load(loader, OneCell, null);
            store.Queries[0].Complete([new()]);

            Assert.IsTrue(first.WaitForAll());
            Assert.AreEqual(1, store.Queries.Count);
        }

        [TestMethod]
        public void LoadWithinWarmWindow_IssuesNoQueryAndRoutesLocalObjectsToCachedCallback()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            store.Queries[0].Complete([]);

            Item local = new();
            store.KnownObjects = [local];
            CallbackProbe cached = new(1);
            int serverCallbacks = 0;
            Load(loader, OneCell, _ => Interlocked.Increment(ref serverCallbacks), cached.Callback);

            Assert.IsTrue(cached.WaitForAll());
            Assert.AreEqual(1, store.Queries.Count);
            Assert.AreSame(local, cached.Received.Single().Single());
            Assert.AreEqual(0, serverCallbacks);
        }

        [TestMethod]
        public void LoadWithinWarmWindow_WithoutCachedCallback_DoesNotLookUpLocalObjects()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            store.Queries[0].Complete([]);

            Load(loader, OneCell, _ => { }, null);

            Assert.AreEqual(1, store.Queries.Count);
            Assert.AreEqual(0, store.LocalLookups);
        }

        [TestMethod]
        public void LateCallerAfterQueryCompleted_DoesNotGetServerCallback()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            store.Queries[0].Complete([new()]);

            CallbackProbe late = new(1);
            CallbackProbe cached = new(1);
            Load(loader, OneCell, late.Callback, cached.Callback);

            Assert.IsTrue(cached.WaitForAll());
            Assert.IsFalse(late.AnyArrivesWithinGrace());
        }

        [TestMethod]
        public void KnownObjectsAreHandedToServerCallbackImmediatelyOnFirstLoad()
        {
            FakeRegionQuery store = new();
            Item known = new();
            store.KnownObjects = [known];
            List<ICollection<Item>> received = [];

            Load(NewLoader(store), OneCell, received.Add);

            // Synchronous: no wait. Known objects are delivered before the server answers.
            Assert.AreEqual(1, received.Count);
            Assert.AreSame(known, received[0].Single());
        }

        [TestMethod]
        public void NullCompletion_ClearsCallbacksAndStateSoNextLoadRequeries()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            CallbackProbe probe = new(1);
            Load(loader, OneCell, probe.Callback);

            store.Queries[0].Complete(null);

            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));
            Load(loader, OneCell, _ => { });
            Assert.AreEqual(2, store.Queries.Count);
            Assert.IsNull(store.Queries[1].LastQueryUtc, "a failed query must not leave the cell stamped as queried");
            store.Queries[1].Complete([new()]);
            Assert.IsFalse(probe.AnyArrivesWithinGrace(), "the cleared callback must never fire");
        }

        [TestMethod]
        public void CancelledCompletion_ClearsStateSoNextLoadRequeries()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            using CancellationTokenSource cts = new();
            CallbackProbe probe = new(1);
            Load(loader, OneCell, probe.Callback, null, cts.Token);

            cts.Cancel();
            store.Queries[0].Complete([new()]);

            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));
            Load(loader, OneCell, _ => { });
            Assert.AreEqual(2, store.Queries.Count);
            Assert.IsFalse(probe.AnyArrivesWithinGrace());
        }

        [TestMethod]
        public void TokenCancelledBeforeLoad_IssuesNoQuery()
        {
            FakeRegionQuery store = new();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Load(NewLoader(store), TwoCells, _ => { }, null, cts.Token);

            Assert.AreEqual(0, store.Queries.Count);
        }

        [TestMethod]
        public void ServerRequestThatDidNotStart_LeavesCellUnstampedSoNextLoadRetries()
        {
            FakeRegionQuery store = new() { StartServerRequest = false };
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));

            store.StartServerRequest = true;
            Load(loader, OneCell, _ => { });

            Assert.AreEqual(1, store.Queries.Count);
        }

        [TestMethod]
        public void InvalidateSection_ForcesRequeryAfterCompletedLoad()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            store.Queries[0].Complete([]);
            Assert.IsTrue(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));

            loader.InvalidateSection(Section);

            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));
            Load(loader, OneCell, _ => { });
            Assert.AreEqual(2, store.Queries.Count);
        }

        [TestMethod]
        public void InvalidateSection_LeavesOtherSectionsWarm()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            loader.LoadSectionAnnotationsInRegion(OneCell, 1.0, 1, _ => { }, null!, CancellationToken.None);
            loader.LoadSectionAnnotationsInRegion(OneCell, 1.0, 2, _ => { }, null!, CancellationToken.None);
            store.Queries.ForEach(q => q.Complete([]));

            loader.InvalidateSection(1);

            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, 1));
            Assert.IsTrue(loader.AreRegionQueriesComplete(OneCell, 1.0, 2));
        }

        [TestMethod]
        public void AreRegionQueriesComplete_FalseForNullBoundsAndUnknownSection()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            store.Queries[0].Complete([]);

            Assert.IsFalse(loader.AreRegionQueriesComplete(null, 1.0, Section));
            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, Section + 1));
            Assert.IsTrue(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));
        }

        [TestMethod]
        public void AreRegionQueriesComplete_FalseWhileInFlight_TrueAfterCompletion()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            Assert.IsFalse(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));

            store.Queries[0].Complete([]);

            Assert.IsTrue(loader.AreRegionQueriesComplete(OneCell, 1.0, Section));
        }

        [TestMethod]
        public void AreRegionQueriesComplete_NeedsEveryCoveredCell()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, TwoCells, _ => { });

            store.Queries[0].Complete([]);
            Assert.IsFalse(loader.AreRegionQueriesComplete(TwoCells, 1.0, Section));

            store.Queries[1].Complete([]);
            Assert.IsTrue(loader.AreRegionQueriesComplete(TwoCells, 1.0, Section));
        }

        [TestMethod]
        public void AreRegionQueriesComplete_FalseWhenViewCoversAnUnqueriedCell()
        {
            FakeRegionQuery store = new();
            RegionLoader<long, Item> loader = NewLoader(store);
            Load(loader, OneCell, _ => { });
            store.Queries[0].Complete([]);

            Assert.IsFalse(loader.AreRegionQueriesComplete(TwoCells, 1.0, Section));
        }

        /// <summary>DEBUG builds key an internal dictionary by debug_message, so a hand-built cell needs one.</summary>
        private static RegionRequestData<Item> NewCell()
        {
            RegionRequestData<Item> cell = new();
#if DEBUG
            cell.debug_message = "RegionLoaderTests";
#endif
            return cell;
        }

        [TestMethod]
        public void RegionRequestData_NewCellIsNotQueriedOrOutstanding()
        {
            RegionRequestData<Item> cell = NewCell();
            Assert.IsFalse(cell.HasBeenQueried);
            Assert.IsFalse(cell.OutstandingQuery);
        }

        [TestMethod]
        public void RegionRequestData_SetQueryStampsAndTracksOutstanding()
        {
            RegionRequestData<Item> cell = NewCell();
            TaskCompletionSource<bool> gate = new();
            DateTime before = DateTime.UtcNow;

            cell.SetQuery(gate.Task);

            Assert.IsTrue(cell.HasBeenQueried);
            Assert.IsTrue(cell.OutstandingQuery);
            Assert.IsTrue(cell.LastQuery!.Value >= before);
            gate.SetResult(true);
            Assert.IsFalse(cell.OutstandingQuery, "a finished result is not outstanding");
        }

        [TestMethod]
        public void RegionRequestData_NullCallbackIsNotQueued()
        {
            RegionRequestData<Item> cell = NewCell();
            cell.AddCallback(null!);
            Assert.AreEqual(0, cell.OnCompletionCallbacks.Count);
        }

        [TestMethod]
        public void RegionRequestData_SuccessfulCompletionRunsCallbacksAndKeepsLastQuery()
        {
            RegionRequestData<Item> cell = NewCell();
            cell.SetQuery(Task.FromResult(true));
            CallbackProbe probe = new(2);
            cell.AddCallback(probe.Callback);
            cell.AddCallback(probe.Callback);

            cell.OnLoadCompleted([new()]);

            Assert.IsTrue(probe.WaitForAll());
            Assert.AreEqual(0, cell.OnCompletionCallbacks.Count);
            Assert.IsNull(cell.AsyncResult);
            Assert.IsTrue(cell.HasBeenQueried);
        }

        [TestMethod]
        public void RegionRequestData_NullCompletionClearsLastQueryAndAsyncResult()
        {
            RegionRequestData<Item> cell = NewCell();
            cell.SetQuery(new TaskCompletionSource<bool>().Task);
            CallbackProbe probe = new(1);
            cell.AddCallback(probe.Callback);

            cell.OnLoadCompleted(null!);

            Assert.IsFalse(cell.HasBeenQueried);
            Assert.IsNull(cell.AsyncResult);
            Assert.AreEqual(0, cell.OnCompletionCallbacks.Count);
            Assert.IsFalse(probe.AnyArrivesWithinGrace());
        }
    }
}
