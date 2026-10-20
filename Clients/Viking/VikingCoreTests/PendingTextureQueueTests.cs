using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the bookkeeping of <see cref="PendingTextureQueue"/>: every dequeued item, including one whose tile
    /// was aborted (section change, cache eviction) while it waited for the main thread, leaves the pending-tile
    /// set and releases its file claim. A tile left in the set makes <see cref="TileView.GetOrLoadTextureAsync"/>
    /// return null forever for that tile key, so the tile stays blank when the user returns to the section.
    /// </summary>
    /// <remarks>
    /// The queue is static, so every test uses fresh texture names. No dispatcher or viewer is wired up:
    /// <c>ProcessQueue</c> is invoked directly, and a non-cancelled item meets a null graphics device and
    /// completes with a null texture through the same cleanup.
    /// </remarks>
    [TestClass]
    public class PendingTextureQueueTests
    {
        private static readonly MethodInfo ProcessQueue =
            typeof(PendingTextureQueue).GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(nameof(PendingTextureQueue), "ProcessQueue");

        private static TileView CreateTile(int section, string textureName)
        {
            PositionNormalTextureVertex[] verticies =
            [
                new(new Vector3(0, 0, 0), Vector3.UnitZ, new Vector2(0, 0)),
                new(new Vector3(256, 0, 0), Vector3.UnitZ, new Vector2(1, 0)),
                new(new Vector3(256, 256, 0), Vector3.UnitZ, new Vector2(1, 1)),
                new(new Vector3(0, 256, 0), Vector3.UnitZ, new Vector2(0, 1)),
            ];
            string url = $"http://tiles.test/{textureName}.png";
            var key = TileUniqueKey.Create(section, "Grid", "TEM", 1, textureName);
            var model = new TileViewModel(key, verticies, [0, 1, 3, 3, 1, 2], url, textureName + ".png", 1);
            return new TileView(model, url, textureName + ".png", 1, model.Size, "Grid");
        }

        private static void Drain()
        {
            for (int pass = 0; pass < 1000 && !PendingTextureQueue.IsEmpty; pass++)
                ProcessQueue.Invoke(null, null);
            Assert.IsTrue(PendingTextureQueue.IsEmpty, "Queue did not drain.");
        }

        private sealed class Queued(TileView tile, string fileKey, TaskCompletionSource<Texture2D> tcs)
        {
            public TileView Tile { get; } = tile;
            public string FileKey { get; } = fileKey;
            public TaskCompletionSource<Texture2D> Tcs { get; } = tcs;
        }

        private static Queued Enqueue(int section, string textureName)
        {
            TileView tile = CreateTile(section, textureName);
            string fileKey = tile.TextureFileName;
            Assert.IsTrue(PendingTextureQueue.TryBeginLoadingFile(fileKey));
            var tcs = new TaskCompletionSource<Texture2D>();
            PendingTextureQueue.Enqueue(default, false, tcs, tile, fileKey);
            Assert.IsTrue(PendingTextureQueue.IsTileViewPending(tile));
            return new Queued(tile, fileKey, tcs);
        }

        private static void AssertReleased(Queued q)
        {
            Assert.IsTrue(q.Tcs.Task.IsCompleted, $"{q.Tile} task never completed.");
            Assert.IsNull(q.Tcs.Task.Result);
            Assert.IsFalse(PendingTextureQueue.IsTileViewPending(q.Tile), $"{q.Tile} still pending.");
            Assert.IsTrue(PendingTextureQueue.TryBeginLoadingFile(q.FileKey), $"{q.FileKey} still claimed.");
            PendingTextureQueue.EndLoadingFile(q.FileKey);
        }

        [TestMethod]
        public void AbortedTileIsNotPendingAfterDequeue()
        {
            string name = "aborted-" + Guid.NewGuid().ToString("N");
            Queued q = Enqueue(12, name);
            q.Tile.AbortRequest();

            Drain();

            AssertReleased(q);
            Assert.IsFalse(PendingTextureQueue.IsTileViewPending(CreateTile(12, name)),
                "A fresh TileView for the same tile key (cache rebuilt it on return to the section) must be free to load.");
        }

        [TestMethod]
        public void TileWithoutDeviceIsNotPendingAfterDequeue()
        {
            Queued q = Enqueue(3, "no-device-" + Guid.NewGuid().ToString("N"));

            Drain();

            AssertReleased(q);
        }

        /// <summary>
        /// One pump pass skips aborted items and keeps going, but stops at the first live item when the viewer
        /// has no graphics device, leaving later items queued for the next pass.
        /// </summary>
        [TestMethod]
        public void OnePassSkipsAbortedItemsAndStopsAtFirstLiveItemWithoutDevice()
        {
            Drain();
            string run = Guid.NewGuid().ToString("N");
            Queued abortedFirst = Enqueue(7, $"pass-{run}-a");
            Queued live = Enqueue(7, $"pass-{run}-b");
            Queued abortedLast = Enqueue(7, $"pass-{run}-c");
            abortedFirst.Tile.AbortRequest();
            abortedLast.Tile.AbortRequest();

            ProcessQueue.Invoke(null, null);

            AssertReleased(abortedFirst);
            AssertReleased(live);
            Assert.IsTrue(PendingTextureQueue.IsTileViewPending(abortedLast.Tile));
            Assert.IsFalse(abortedLast.Tcs.Task.IsCompleted);

            Drain();
            AssertReleased(abortedLast);
        }

        [TestMethod]
        public void EveryDequeuedTileIsReleasedWhateverWasAborted()
        {
            Gen<bool[]> abortFlags = Gen.Choose(1, 24).SelectMany(n => Gen.ArrayOf(n, Arb.Generate<bool>()));
            Prop.ForAll(Arb.From(abortFlags), (bool[] aborted) =>
            {
                string run = Guid.NewGuid().ToString("N");
                Queued[] queued = aborted.Select((_, i) => Enqueue(100 + (i % 3), $"prop-{run}-{i}")).ToArray();
                for (int i = 0; i < aborted.Length; i++)
                {
                    if (aborted[i])
                        queued[i].Tile.AbortRequest();
                }

                Drain();

                foreach (Queued q in queued)
                    AssertReleased(q);
            }).QuickCheckThrowOnFailure();
        }
    }
}
