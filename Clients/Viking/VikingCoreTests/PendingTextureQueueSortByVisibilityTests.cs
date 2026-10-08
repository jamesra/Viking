using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Pins <see cref="PendingTextureQueue.SortByVisibility"/>, which the section viewer timer uses so main-thread
    /// texture creation favors visible tiles, then nearest section, then coarsest downsample — matching
    /// <see cref="TextureRequestQueue.SortByPriority"/> on the HTTP side.
    /// </summary>
    /// <remarks>
    /// The queue is static; tests enqueue decoded-data placeholders without running <c>ProcessQueue</c>, read order
    /// via reflection on the private item list, and drain in cleanup so file claims and pending tiles are released.
    /// </remarks>
    [TestClass]
    public class PendingTextureQueueSortByVisibilityTests
    {
        private const int DefaultMaxWorkers = 32;
        private static readonly BindingFlags StaticPrivate = BindingFlags.NonPublic | BindingFlags.Static;

        private static readonly Type QueueType = typeof(PendingTextureQueue);

        private static readonly FieldInfo ItemsField = QueueType.GetField("_items", StaticPrivate)
            ?? throw new MissingFieldException(QueueType.Name, "_items");

        private static readonly MethodInfo ProcessQueue =
            QueueType.GetMethod("ProcessQueue", StaticPrivate)
            ?? throw new MissingMethodException(QueueType.Name, "ProcessQueue");

        [TestCleanup]
        public void Cleanup()
        {
            Drain();
            TextureRequestQueue.SetMaxWorkers(DefaultMaxWorkers);
        }

        private static TileView CreateTile(int section, string textureName, int downsample = 1, double x = 0, double y = 0)
        {
            PositionNormalTextureVertex[] verticies =
            [
                new(new Vector3(x, y, 0), Vector3.UnitZ, new Vector2(0, 0)),
                new(new Vector3(x + 256, y, 0), Vector3.UnitZ, new Vector2(1, 0)),
                new(new Vector3(x + 256, y + 256, 0), Vector3.UnitZ, new Vector2(1, 1)),
                new(new Vector3(x, y + 256, 0), Vector3.UnitZ, new Vector2(0, 1)),
            ];
            string url = $"http://tiles.test/{textureName}.png";
            var key = TileUniqueKey.Create(section, "Grid", "TEM", downsample, textureName);
            var model = new TileViewModel(key, verticies, [0, 1, 3, 3, 1, 2], url, textureName + ".png", downsample);
            return new TileView(model, url, textureName + ".png", downsample, model.Size, "Grid");
        }

        private static string UniqueName(string prefix) => prefix + "-" + Guid.NewGuid().ToString("N");

        private static void Drain()
        {
            for (int pass = 0; pass < 1000 && !PendingTextureQueue.IsEmpty; pass++)
                ProcessQueue.Invoke(null, null);
        }

        private static void EnqueueTile(TileView tile)
        {
            string fileKey = tile.TextureFileName;
            Assert.IsTrue(PendingTextureQueue.TryBeginLoadingFile(fileKey));
            PendingTextureQueue.Enqueue(default, false, new TaskCompletionSource<Texture2D>(), tile, fileKey);
        }

        private static List<string> QueuedTextureNames()
        {
            var names = new List<string>();
            foreach (object item in (IList)ItemsField.GetValue(null)!)
            {
                var tile = (TileView?)item.GetType().GetProperty("TileView", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(item);
                if (tile != null)
                    names.Add(tile.TextureFileName);
            }
            return names;
        }

        [TestMethod]
        public void SortByVisibilityPutsVisibleNearSectionAndCoarseTilesFirst()
        {
            Drain();
            TextureRequestQueue.SetMaxWorkers(4);
            string run = UniqueName("sort");
            var visible = new Rectangle(0, 1000, 0, 1000);
            var queued = new (string Name, int Section, int Downsample, double X)[]
            {
                ($"{run}-hidden-near-coarse", 10, 8, 5000),
                ($"{run}-visible-far", 15, 1, 0),
                ($"{run}-visible-near-fine", 10, 1, 0),
                ($"{run}-visible-near-coarse", 10, 4, 256),
            };
            foreach (var t in queued)
                EnqueueTile(CreateTile(t.Section, t.Name, t.Downsample, t.X));

            PendingTextureQueue.SortByVisibility(visible, 10);

            CollectionAssert.AreEqual(
                new[] { queued[3].Name + ".png", queued[2].Name + ".png", queued[1].Name + ".png", queued[0].Name + ".png" },
                QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray());
        }

        [TestMethod]
        public void SortByVisibilityLeavesTheQueueAloneWhileItIsShorterThanMaxWorkers()
        {
            Drain();
            TextureRequestQueue.SetMaxWorkers(8);
            string run = UniqueName("short");
            var visible = new Rectangle(0, 1000, 0, 1000);
            string[] names = [$"{run}-a", $"{run}-b", $"{run}-c"];
            EnqueueTile(CreateTile(10, names[0], 1, 5000));
            EnqueueTile(CreateTile(10, names[1], 1, 0));
            EnqueueTile(CreateTile(10, names[2], 8, 0));

            PendingTextureQueue.SortByVisibility(visible, 10);

            CollectionAssert.AreEqual(
                names.Select(n => n + ".png").ToArray(),
                QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray());
        }

        [TestMethod]
        public void SortByVisibilityReordersOnceTheQueueHoldsAsManyItemsAsMaxWorkers()
        {
            Drain();
            TextureRequestQueue.SetMaxWorkers(3);
            string run = UniqueName("equal");
            var visible = new Rectangle(0, 1000, 0, 1000);
            string[] names = [$"{run}-hidden", $"{run}-visible-fine", $"{run}-visible-coarse"];
            EnqueueTile(CreateTile(10, names[0], 8, 5000));
            EnqueueTile(CreateTile(10, names[1], 1, 0));
            EnqueueTile(CreateTile(10, names[2], 4, 0));

            PendingTextureQueue.SortByVisibility(visible, 10);

            CollectionAssert.AreEqual(
                new[] { names[2] + ".png", names[1] + ".png", names[0] + ".png" },
                QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray());
        }

        /// <summary>
        /// Whatever mix of visibility, section distance and downsample is queued, pending items are ordered by
        /// (visible first, nearest section, coarsest downsample), ties in arrival order.
        /// </summary>
        [TestMethod]
        public void SortByVisibilityOrdersAnyQueuedMixByVisibilityThenSectionDistanceThenDownsample()
        {
            int[] downsamples = [1, 2, 4, 8, 16];
            var visible = new Rectangle(0, 1000, 0, 1000);
            const int currentSection = 50;
            Gen<(bool Visible, int Dz, int Downsample)> tileSpec =
                from isVisible in Arb.Generate<bool>()
                from dz in Gen.Choose(-3, 3)
                from ds in Gen.Elements(downsamples)
                select (isVisible, dz, ds);
            Gen<(bool, int, int)[]> specs = Gen.Choose(1, 14).SelectMany(n => Gen.ArrayOf(n, tileSpec));

            Prop.ForAll(Arb.From(specs), ((bool Visible, int Dz, int Downsample)[] mix) =>
            {
                Drain();
                TextureRequestQueue.SetMaxWorkers(1);
                string run = UniqueName("prop");
                for (int i = 0; i < mix.Length; i++)
                {
                    var s = mix[i];
                    EnqueueTile(CreateTile(currentSection + s.Dz, $"{run}-{i}", s.Downsample, s.Visible ? 0 : 5000));
                }

                PendingTextureQueue.SortByVisibility(visible, currentSection);

                string[] expected = Enumerable.Range(0, mix.Length)
                    .OrderBy(i => mix[i].Visible ? 0 : 1)
                    .ThenBy(i => Math.Abs(mix[i].Dz))
                    .ThenByDescending(i => mix[i].Downsample)
                    .Select(i => $"{run}-{i}.png")
                    .ToArray();
                string[] actual = QueuedTextureNames().Select(System.IO.Path.GetFileName).ToArray()!;
                CollectionAssert.AreEqual(expected, actual);
                Drain();
            }).QuickCheckThrowOnFailure();
        }
    }
}
