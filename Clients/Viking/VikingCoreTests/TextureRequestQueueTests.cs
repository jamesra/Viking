using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the bookkeeping of <see cref="TextureRequestQueue"/>, the priority queue that feeds the HTTP texture
    /// workers: every request leaves the pending-tile set when it completes, is cancelled while queued, or is
    /// cancelled while it waits for a worker slot. A tile left in the set makes <see cref="TileView"/> loads
    /// return null for that tile key for the rest of the session (the blank-tile class fixed on the
    /// <see cref="PendingTextureQueue"/> side), because <see cref="TileView"/> equality is by tile key.
    /// It also pins the order <see cref="TextureRequestQueue.SortByPriority"/> hands requests to the workers.
    /// </summary>
    /// <remarks>
    /// The queue is static and owns a pool of 32 workers. The tests that need a request to stay queued use a
    /// <see cref="Rig"/>, which takes every slot of the worker throttle through reflection so requests park
    /// deterministically (dequeued and waiting for a slot, or still in the list behind a full worker pool), and
    /// releases everything again on dispose. <see cref="Cleanup"/> stops the worker pool after every test.
    /// <c>UI.State.volume</c> is null in these tests, so a request that reaches a worker completes with a null
    /// texture without touching a graphics device.
    /// </remarks>
    [TestClass]
    public class TextureRequestQueueTests
    {
        private const int DefaultMaxWorkers = 32;
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        private static readonly BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;

        private static readonly Type QueueType = typeof(TextureRequestQueue);

        private static readonly FieldInfo ThrottleField = QueueType.GetField("_throttle", StaticPrivate)
            ?? throw new MissingFieldException(QueueType.Name, "_throttle");

        private static readonly FieldInfo RequestsField = QueueType.GetField("_requests", StaticPrivate)
            ?? throw new MissingFieldException(QueueType.Name, "_requests");

        private static readonly FieldInfo LockField = QueueType.GetField("_lock", StaticPrivate)
            ?? throw new MissingFieldException(QueueType.Name, "_lock");

        private VolumeViewModel? _savedVolume;

        [TestInitialize]
        public void Initialize()
        {
            _savedVolume = Viking.UI.State.volume;
            Viking.UI.State.volume = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            TextureRequestQueue.StopWorkers();
            TextureRequestQueue.SetMaxWorkers(DefaultMaxWorkers);
            Viking.UI.State.volume = _savedVolume;
        }

        private static TileView CreateTile(int section, string textureName, int downsample = 1, double x = 0, double y = 0)
        {
            PositionNormalTextureVertex[] verticies =
            [
                new(new Geometry.Vector3(x, y, 0), Geometry.Vector3.UnitZ, new Geometry.Vector2(0, 0)),
                new(new Geometry.Vector3(x + 256, y, 0), Geometry.Vector3.UnitZ, new Geometry.Vector2(1, 0)),
                new(new Geometry.Vector3(x + 256, y + 256, 0), Geometry.Vector3.UnitZ, new Geometry.Vector2(1, 1)),
                new(new Geometry.Vector3(x, y + 256, 0), Geometry.Vector3.UnitZ, new Geometry.Vector2(0, 1)),
            ];
            string url = $"http://tiles.test/{textureName}.png";
            var key = TileUniqueKey.Create(section, "Grid", "TEM", downsample, textureName);
            var model = new TileViewModel(key, verticies, [0, 1, 3, 3, 1, 2], url, textureName + ".png", downsample);
            return new TileView(model, url, textureName + ".png", downsample, model.Size, "Grid");
        }

        private static string UniqueName(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

        private static void AwaitCompleted(Task task, string what)
        {
            Assert.IsTrue(task.Wait(Patience), $"{what} did not complete within {Patience.TotalSeconds:0} s.");
        }

        private static void AssertCompletedNullAndReleased(Task<Texture2D> task, TileView tile)
        {
            AwaitCompleted(task, tile.ToString());
            Assert.IsNull(task.Result);
            Assert.IsFalse(TextureRequestQueue.IsTileViewPending(tile), $"{tile} is still pending after it completed.");
        }

        private static void WaitUntil(Func<bool> condition, string what)
        {
            DateTime deadline = DateTime.UtcNow + Patience;
            while (!condition())
            {
                Assert.IsTrue(DateTime.UtcNow < deadline, $"Timed out waiting for {what}.");
                Thread.Sleep(5);
            }
        }

        private static List<string> QueuedTextureNames()
        {
            lock (LockField.GetValue(null)!)
            {
                var names = new List<string>();
                foreach (object item in (IList)RequestsField.GetValue(null)!)
                {
                    var tile = (TileView)item.GetType().GetProperty("TileView", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(item)!;
                    names.Add(tile.TextureFileName);
                }
                return names;
            }
        }

        /// <summary>
        /// Holds every slot of the worker throttle so requests park instead of finishing, and tracks every request
        /// and token source it hands out so <see cref="Dispose"/> can cancel, drain and restore the queue.
        /// </summary>
        private sealed class Rig : IDisposable
        {
            private readonly SemaphoreSlim _throttle;
            private readonly int _held;
            private bool _released;
            private readonly List<CancellationTokenSource> _sources = [];
            private readonly List<Task<Texture2D>> _tasks = [];

            /// <param name="maxWorkers">Value passed to <see cref="TextureRequestQueue.SetMaxWorkers"/>; also how
            /// many throttle slots are held and the queue length from which SortByPriority reorders.</param>
            /// <param name="fillWorkerPool">Enqueue one request per worker so the whole pool is parked on the
            /// throttle and later requests stay in the queue list.</param>
            public Rig(int maxWorkers, bool fillWorkerPool)
            {
                TextureRequestQueue.SetMaxWorkers(maxWorkers);
                _throttle = (SemaphoreSlim)ThrottleField.GetValue(null)!;
                _held = TextureRequestQueue.MaxWorkers;
                for (int i = 0; i < _held; i++)
                    Assert.IsTrue(_throttle.Wait(Patience), "Worker throttle slot was not free.");

                if (fillWorkerPool)
                {
                    for (int i = 0; i < DefaultMaxWorkers; i++)
                        Enqueue(CreateTile(0, UniqueName("filler")), NewSource().Token);
                    WaitUntil(() => QueuedTextureNames().Count == 0, "the worker pool to take the filler requests");
                }
            }

            public CancellationTokenSource NewSource()
            {
                var cts = new CancellationTokenSource();
                lock (_sources)
                    _sources.Add(cts);
                return cts;
            }

            public Task<Texture2D> Enqueue(TileView tile, CancellationToken token)
            {
                Task<Texture2D> task = TextureRequestQueue.EnqueueRequest(tile, null!, token);
                lock (_tasks)
                    _tasks.Add(task);
                return task;
            }

            public void WaitUntilRequestsLeftTheList() =>
                WaitUntil(() => QueuedTextureNames().Count == 0, "the request to be taken by a worker");

            /// <summary>Gives the held slots back early so parked workers continue; <see cref="Dispose"/> then releases nothing.</summary>
            public void ReleaseThrottle()
            {
                if (_released)
                    return;
                _released = true;
                _throttle.Release(_held);
            }

            public void Dispose()
            {
                lock (_sources)
                {
                    foreach (CancellationTokenSource cts in _sources)
                        cts.Cancel();
                }
                ReleaseThrottle();
                Task[] pending;
                lock (_tasks)
                    pending = _tasks.ToArray();
                Assert.IsTrue(Task.WaitAll(pending, Patience), "Requests did not drain after cancellation.");
                lock (_sources)
                {
                    foreach (CancellationTokenSource cts in _sources)
                        cts.Dispose();
                    _sources.Clear();
                }
                TextureRequestQueue.SetMaxWorkers(DefaultMaxWorkers);
            }
        }

        [TestMethod]
        public void RequestWithoutVolumeCompletesNullAndLeavesPendingSet()
        {
            TileView tile = CreateTile(4, UniqueName("no-volume"));

            Task<Texture2D> task = TextureRequestQueue.EnqueueRequest(tile, null!, CancellationToken.None);

            AssertCompletedNullAndReleased(task, tile);
        }

        [TestMethod]
        public void NullTileIsNeverPending()
        {
            Assert.IsFalse(TextureRequestQueue.IsTileViewPending(null!));
        }

        /// <summary>
        /// A worker slot freed by a finished request must be handed on: with one slot, a second and third
        /// request only complete if the first released it.
        /// </summary>
        [TestMethod]
        public void FinishedRequestsHandTheWorkerSlotOn()
        {
            TextureRequestQueue.SetMaxWorkers(1);
            TileView[] tiles = Enumerable.Range(0, 3).Select(i => CreateTile(6, UniqueName($"slot-{i}"))).ToArray();

            Task<Texture2D>[] tasks = tiles
                .Select(t => TextureRequestQueue.EnqueueRequest(t, null!, CancellationToken.None))
                .ToArray();

            Assert.IsTrue(Task.WaitAll(tasks, Patience), "A request never got the worker slot the previous one held.");
            for (int i = 0; i < tiles.Length; i++)
                AssertCompletedNullAndReleased(tasks[i], tiles[i]);
        }

        /// <summary>
        /// The worker claims the texture file name through <see cref="PendingTextureQueue"/> while it loads; that
        /// claim must be gone once the request completed, or the file is never fetched again this session.
        /// </summary>
        [TestMethod]
        public void CompletedRequestLeavesNoFileClaimBehind()
        {
            TileView tile = CreateTile(6, UniqueName("claim"));

            Task<Texture2D> task = TextureRequestQueue.EnqueueRequest(tile, null!, CancellationToken.None);

            AssertCompletedNullAndReleased(task, tile);
            Assert.IsTrue(PendingTextureQueue.TryBeginLoadingFile(tile.TextureFileName), "The request left its file claim behind.");
            PendingTextureQueue.EndLoadingFile(tile.TextureFileName);
        }

        [TestMethod]
        public void RequestForAFileAnotherLoaderHoldsCompletesWithoutTakingTheClaim()
        {
            TileView tile = CreateTile(6, UniqueName("foreign-claim"));
            Assert.IsTrue(PendingTextureQueue.TryBeginLoadingFile(tile.TextureFileName));
            try
            {
                Task<Texture2D> task = TextureRequestQueue.EnqueueRequest(tile, null!, CancellationToken.None);

                AssertCompletedNullAndReleased(task, tile);
                Assert.IsFalse(PendingTextureQueue.TryBeginLoadingFile(tile.TextureFileName), "The request released a claim it does not own.");
            }
            finally
            {
                PendingTextureQueue.EndLoadingFile(tile.TextureFileName);
            }
        }

        /// <summary>
        /// Several cancelled requests queued back to back are all completed by the one dequeue pass that finds
        /// them; none may be forgotten with its tile left in the pending set.
        /// </summary>
        [TestMethod]
        public void SeveralCancelledRequestsAheadOfALiveOneAreAllReleased()
        {
            using var rig = new Rig(1, fillWorkerPool: true);
            string run = UniqueName("burst");
            var cancelled = new List<(TileView Tile, Task<Texture2D> Task)>();
            for (int i = 0; i < 4; i++)
            {
                TileView tile = CreateTile(11, $"{run}-{i}");
                tile.AbortRequest();
                cancelled.Add((tile, rig.Enqueue(tile, rig.NewSource().Token)));
            }
            TileView live = CreateTile(11, $"{run}-live");
            Task<Texture2D> liveTask = rig.Enqueue(live, rig.NewSource().Token);

            rig.ReleaseThrottle();

            foreach (var (tile, task) in cancelled)
                AssertCompletedNullAndReleased(task, tile);
            AssertCompletedNullAndReleased(liveTask, live);
        }

        [TestMethod]
        public void RequestIsPendingWhileItWaitsForAWorkerSlot()
        {
            using var rig = new Rig(1, fillWorkerPool: false);
            TileView tile = CreateTile(4, UniqueName("waiting"));

            Task<Texture2D> task = rig.Enqueue(tile, rig.NewSource().Token);
            rig.WaitUntilRequestsLeftTheList();

            Assert.IsTrue(TextureRequestQueue.IsTileViewPending(tile));
            Assert.IsFalse(task.IsCompleted, "A request waiting for a worker slot completed.");
        }

        [TestMethod]
        public void CancellingTheSectionTokenWhileWaitingForASlotReleasesTheTile()
        {
            using var rig = new Rig(1, fillWorkerPool: false);
            string name = UniqueName("cancel-waiting");
            TileView tile = CreateTile(4, name);
            CancellationTokenSource section = rig.NewSource();

            Task<Texture2D> task = rig.Enqueue(tile, section.Token);
            rig.WaitUntilRequestsLeftTheList();
            Assert.IsTrue(TextureRequestQueue.IsTileViewPending(tile));

            section.Cancel();

            AssertCompletedNullAndReleased(task, tile);
            AssertFreshTileForSameKeyIsAccepted(rig, 4, name);
        }

        [TestMethod]
        public void SectionTokenCancelledBeforeDequeueReleasesTheTile()
        {
            using var rig = new Rig(1, fillWorkerPool: false);
            string name = UniqueName("cancel-queued");
            TileView tile = CreateTile(9, name);
            CancellationTokenSource section = rig.NewSource();
            section.Cancel();

            Task<Texture2D> task = rig.Enqueue(tile, section.Token);

            AssertCompletedNullAndReleased(task, tile);
            AssertFreshTileForSameKeyIsAccepted(rig, 9, name);
        }

        [TestMethod]
        public void AbortedTileIsReleasedAtDequeueAndAFreshTileForTheSameKeyLoads()
        {
            using var rig = new Rig(1, fillWorkerPool: false);
            string name = UniqueName("aborted");
            TileView tile = CreateTile(9, name);
            tile.AbortRequest();

            Task<Texture2D> task = rig.Enqueue(tile, rig.NewSource().Token);

            AssertCompletedNullAndReleased(task, tile);
            AssertFreshTileForSameKeyIsAccepted(rig, 9, name);
        }

        /// <summary>
        /// A rebuilt TileView for a key whose earlier request was cancelled must be queued, not swallowed by the
        /// duplicate check: its task stays open while the worker pool is held, and only cancelling finishes it.
        /// </summary>
        private static void AssertFreshTileForSameKeyIsAccepted(Rig rig, int section, string name)
        {
            TileView fresh = CreateTile(section, name);
            CancellationTokenSource token = rig.NewSource();

            Task<Texture2D> task = rig.Enqueue(fresh, token.Token);
            rig.WaitUntilRequestsLeftTheList();

            Assert.IsTrue(TextureRequestQueue.IsTileViewPending(fresh), "The fresh tile was not queued.");
            Assert.IsFalse(task.IsCompleted, "The fresh tile completed at once, as a duplicate request does.");

            token.Cancel();
            AssertCompletedNullAndReleased(task, fresh);
        }

        [TestMethod]
        public void DuplicateRequestCompletesWithNullAndLeavesTheOriginalPending()
        {
            using var rig = new Rig(1, fillWorkerPool: false);
            string name = UniqueName("duplicate");
            TileView original = CreateTile(2, name);
            CancellationTokenSource section = rig.NewSource();
            Task<Texture2D> first = rig.Enqueue(original, section.Token);
            rig.WaitUntilRequestsLeftTheList();

            Task<Texture2D> second = rig.Enqueue(CreateTile(2, name), rig.NewSource().Token);

            Assert.IsTrue(second.IsCompleted, "A duplicate request must complete at once.");
            Assert.IsNull(second.Result);
            Assert.IsTrue(TextureRequestQueue.IsTileViewPending(original), "The duplicate removed the original from the pending set.");
            Assert.IsFalse(first.IsCompleted, "The duplicate completed the original request.");

            section.Cancel();

            AssertCompletedNullAndReleased(first, original);
            AssertFreshTileForSameKeyIsAccepted(rig, 2, name);
        }

        [TestMethod]
        public void SortByPriorityPutsVisibleNearSectionAndCoarseTilesFirst()
        {
            using var rig = new Rig(1, fillWorkerPool: true);
            string run = UniqueName("sort");
            var visible = new Geometry.Rectangle(0, 1000, 0, 1000);
            var queued = new (string Name, int Section, int Downsample, double X)[]
            {
                ($"{run}-hidden-near-coarse", 10, 8, 5000),
                ($"{run}-visible-far", 15, 1, 0),
                ($"{run}-visible-near-fine", 10, 1, 0),
                ($"{run}-visible-near-coarse", 10, 4, 256),
            };
            foreach (var t in queued)
                rig.Enqueue(CreateTile(t.Section, t.Name, t.Downsample, t.X), rig.NewSource().Token);

            TextureRequestQueue.SortByPriority(visible, 10);

            CollectionAssert.AreEqual(
                new[] { queued[3].Name + ".png", queued[2].Name + ".png", queued[1].Name + ".png", queued[0].Name + ".png" },
                QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray());
        }

        [TestMethod]
        public void SortByPriorityLeavesTheQueueAloneWhileItIsShorterThanTheWorkerCount()
        {
            using var rig = new Rig(8, fillWorkerPool: true);
            string run = UniqueName("short");
            var visible = new Geometry.Rectangle(0, 1000, 0, 1000);
            string[] names = [$"{run}-a", $"{run}-b", $"{run}-c"];
            rig.Enqueue(CreateTile(10, names[0], 1, 5000), rig.NewSource().Token);
            rig.Enqueue(CreateTile(10, names[1], 1, 0), rig.NewSource().Token);
            rig.Enqueue(CreateTile(10, names[2], 8, 0), rig.NewSource().Token);

            TextureRequestQueue.SortByPriority(visible, 10);

            CollectionAssert.AreEqual(
                names.Select(n => n + ".png").ToArray(),
                QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray());
        }

        [TestMethod]
        public void SortByPriorityReordersOnceTheQueueHoldsAsManyRequestsAsWorkers()
        {
            using var rig = new Rig(3, fillWorkerPool: true);
            string run = UniqueName("equal");
            var visible = new Geometry.Rectangle(0, 1000, 0, 1000);
            string[] names = [$"{run}-hidden", $"{run}-visible-fine", $"{run}-visible-coarse"];
            rig.Enqueue(CreateTile(10, names[0], 8, 5000), rig.NewSource().Token);
            rig.Enqueue(CreateTile(10, names[1], 1, 0), rig.NewSource().Token);
            rig.Enqueue(CreateTile(10, names[2], 4, 0), rig.NewSource().Token);

            TextureRequestQueue.SortByPriority(visible, 10);

            CollectionAssert.AreEqual(
                new[] { names[2] + ".png", names[1] + ".png", names[0] + ".png" },
                QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray());
        }

        /// <summary>
        /// Whatever mix of visibility, section distance and downsample is queued, the workers receive the
        /// requests ordered by (visible first, nearest section, coarsest downsample), ties in arrival order.
        /// </summary>
        [TestMethod]
        public void SortByPriorityOrdersAnyQueuedMixByVisibilityThenSectionDistanceThenDownsample()
        {
            int[] downsamples = [1, 2, 4, 8, 16];
            var visible = new Geometry.Rectangle(0, 1000, 0, 1000);
            const int currentSection = 50;
            Gen<(bool Visible, int Dz, int Downsample)> tileSpec =
                from isVisible in Arb.Generate<bool>()
                from dz in Gen.Choose(-3, 3)
                from ds in Gen.Elements(downsamples)
                select (isVisible, dz, ds);
            Gen<(bool, int, int)[]> specs = Gen.Choose(1, 14).SelectMany(n => Gen.ArrayOf(n, tileSpec));

            Prop.ForAll(Arb.From(specs), ((bool Visible, int Dz, int Downsample)[] mix) =>
            {
                using var rig = new Rig(1, fillWorkerPool: true);
                string run = UniqueName("prop");
                for (int i = 0; i < mix.Length; i++)
                {
                    var s = mix[i];
                    rig.Enqueue(CreateTile(currentSection + s.Dz, $"{run}-{i}", s.Downsample, s.Visible ? 0 : 5000), rig.NewSource().Token);
                }

                TextureRequestQueue.SortByPriority(visible, currentSection);

                string[] expected = Enumerable.Range(0, mix.Length)
                    .OrderBy(i => mix[i].Visible ? 0 : 1)
                    .ThenBy(i => Math.Abs(mix[i].Dz))
                    .ThenByDescending(i => mix[i].Downsample)
                    .Select(i => $"{run}-{i}.png")
                    .ToArray();
                string[] actual = QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray()!;
                CollectionAssert.AreEqual(expected, actual);
            }).QuickCheckThrowOnFailure();
        }
    }
}
