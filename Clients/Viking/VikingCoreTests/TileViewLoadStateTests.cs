using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework.Graphics;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins the load-gating state of <see cref="TileView"/> that <c>SectionViewerControl.DrawSection</c> relies on
    /// to avoid launching a second load for a tile every frame, and the key identity that the pending-tile sets
    /// use to deduplicate tiles. No graphics device is needed: a cancelled token makes
    /// <see cref="TileView.GetOrLoadTextureAsync"/> return before it touches the request queues.
    /// </summary>
    [TestClass]
    public class TileViewLoadStateTests
    {
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

        private static string UniqueName(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

        [TestMethod]
        public void FreshTileNeedsLoading()
        {
            TileView tile = CreateTile(4, UniqueName("fresh"));

            Assert.IsTrue(tile.TextureNeedsLoading);
            Assert.IsFalse(tile.SectionLoadingCancelled);
        }

        [TestMethod]
        public void QueuedLoadSuppressesNeedsLoading()
        {
            TileView tile = CreateTile(4, UniqueName("queued"));

            tile.MarkLoadQueued();

            Assert.IsFalse(tile.TextureNeedsLoading,
                "DrawSection would launch a second Task.Run for the same tile on the next frame.");
        }

        [TestMethod]
        public void StartingTheLoadClearsTheQueuedFlagEvenWhenTheSectionTokenIsCancelled()
        {
            TileView tile = CreateTile(4, UniqueName("cancelled"));
            tile.MarkLoadQueued();
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Task<Texture2D> load = tile.GetOrLoadTextureAsync(null!, cts.Token);

            Assert.IsTrue(load.IsCompleted, "A cancelled section token must return before the tile is handed to the request queue.");
            Assert.IsNull(load.GetAwaiter().GetResult());
            Assert.IsTrue(tile.TextureNeedsLoading,
                "A load abandoned by a section change must leave the tile loadable when the user returns.");
            Assert.IsTrue(tile.SectionLoadingCancelled);
        }

        [TestMethod]
        public void ServerTextureNotFoundStopsFurtherLoads()
        {
            TileView tile = CreateTile(4, UniqueName("missing"));

            tile.ServerTextureNotFound = true;

            Assert.IsFalse(tile.TextureNeedsLoading);
        }

        [TestMethod]
        public void AbortRequestMarksSectionLoadingCancelled()
        {
            TileView tile = CreateTile(4, UniqueName("abort"));

            tile.AbortRequest();

            Assert.IsTrue(tile.SectionLoadingCancelled);
            Assert.IsTrue(tile.TextureNeedsLoading, "Abort cancels the in-flight load; it does not mark the tile as loaded.");
        }

        [TestMethod]
        public void StartingANewLoadClearsAnEarlierAbort()
        {
            TileView tile = CreateTile(4, UniqueName("reload"));
            tile.AbortRequest();
            Assert.IsTrue(tile.SectionLoadingCancelled);
            tile.ServerTextureNotFound = true;
            using var cts = new CancellationTokenSource();

            Task<Texture2D> load = tile.GetOrLoadTextureAsync(null!, cts.Token);

            Assert.IsTrue(load.IsCompleted, "A tile the server does not have must not be handed to the request queue.");
            Assert.IsNull(load.GetAwaiter().GetResult());
            Assert.IsFalse(tile.SectionLoadingCancelled,
                "Returning to a section starts a fresh load; the abort from leaving it must not linger.");
        }

        [TestMethod]
        public void TilesWithTheSameUniqueKeyAreEqualAndDeduplicateInSets()
        {
            string name = UniqueName("same");
            TileView first = CreateTile(9, name);
            TileView rebuilt = CreateTile(9, name);

            Assert.IsFalse(ReferenceEquals(first, rebuilt));
            Assert.IsTrue(first.Equals(rebuilt));
            Assert.AreEqual(first.GetHashCode(), rebuilt.GetHashCode());
            var set = new HashSet<TileView> { first };
            Assert.IsFalse(set.Add(rebuilt));
        }

        [TestMethod]
        public void TilesWithDifferentSectionOrTextureAreDistinct()
        {
            string name = UniqueName("distinct");
            TileView tile = CreateTile(9, name);

            Assert.IsFalse(tile.Equals(CreateTile(10, name)));
            Assert.IsFalse(tile.Equals(CreateTile(9, name + "-other")));
            Assert.IsFalse(tile.Equals((TileView?)null));
        }
    }
}
