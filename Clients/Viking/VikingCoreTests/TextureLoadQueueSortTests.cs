using System;
using System.Collections.Generic;
using System.Linq;
using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking;
using Viking.ViewModels;
using Viking.VolumeModel;
using VikingXNA;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TextureLoadQueueSort.GetSortKeys"/> and <see cref="TextureLoadQueueSort.SortToList"/>, the
    /// shared stable sort used by <see cref="TextureRequestQueue.SortByPriority"/> and
    /// <see cref="PendingTextureQueue.SortByVisibility"/>.
    /// </summary>
    [TestClass]
    public class TextureLoadQueueSortTests
    {
        private static readonly Rectangle DefaultVisible = new(0, 1000, 0, 1000);

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

        [TestMethod]
        public void GetSortKeys_NullTileView_IsNonVisibleWithZeroDistanceAndDownsample()
        {
            var keys = TextureLoadQueueSort.GetSortKeys(null, DefaultVisible, 42);
            Assert.AreEqual(1, keys.visibilityRank);
            Assert.AreEqual(0, keys.sectionDistance);
            Assert.AreEqual(0, keys.downsample);
        }

        [TestMethod]
        public void GetSortKeys_VisibleInBounds_UsesTileSectionAndDownsample()
        {
            var tile = CreateTile(12, "in-bounds", downsample: 8, x: 0);
            var keys = TextureLoadQueueSort.GetSortKeys(tile, DefaultVisible, currentSectionZ: 10);
            Assert.AreEqual(0, keys.visibilityRank);
            Assert.AreEqual(2, keys.sectionDistance);
            Assert.AreEqual(8, keys.downsample);
        }

        [TestMethod]
        public void GetSortKeys_OutOfBounds_IsNonVisibleEvenOnCurrentSection()
        {
            var tile = CreateTile(10, "far", downsample: 4, x: 5000);
            var keys = TextureLoadQueueSort.GetSortKeys(tile, DefaultVisible, currentSectionZ: 10);
            Assert.AreEqual(1, keys.visibilityRank);
            Assert.AreEqual(0, keys.sectionDistance);
            Assert.AreEqual(4, keys.downsample);
        }

        [TestMethod]
        public void GetSortKeys_SectionDistance_IsAbsolute()
        {
            var below = CreateTile(7, "below", x: 0);
            var above = CreateTile(13, "above", x: 0);
            Assert.AreEqual(
                TextureLoadQueueSort.GetSortKeys(below, DefaultVisible, 10).sectionDistance,
                TextureLoadQueueSort.GetSortKeys(above, DefaultVisible, 10).sectionDistance);
        }

        [TestMethod]
        public void SortToList_OrdersVisibleBeforeSectionDistanceBeforeDescendingDownsample()
        {
            var tiles = new[]
            {
                CreateTile(10, "hidden-near-coarse", 8, 5000),
                CreateTile(15, "visible-far", 1, 0),
                CreateTile(10, "visible-near-fine", 1, 0),
                CreateTile(10, "visible-near-coarse", 4, 256),
            };
            var items = tiles.Select((t, i) => (Id: i, Tile: (TileView?)t)).ToList();

            var sorted = TextureLoadQueueSort.SortToList(
                items,
                DefaultVisible,
                10,
                x => x.Tile);

            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, sorted.Select(x => x.Id).ToArray());
        }

        [TestMethod]
        public void SortToList_KeepsStableOrderWhenSortKeysTie()
        {
            var tiles = new[]
            {
                CreateTile(10, "first", 4, 0),
                CreateTile(10, "second", 4, 128),
                CreateTile(10, "third", 4, 256),
            };
            var items = tiles.Select((t, i) => (Id: i, Tile: (TileView?)t)).ToList();

            var sorted = TextureLoadQueueSort.SortToList(items, DefaultVisible, 10, x => x.Tile);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, sorted.Select(x => x.Id).ToArray());
        }

        /// <summary>
        /// Any mix of visibility, section offset, and downsample sorts by (visible, |dz|, descending downsample),
        /// preserving input order on ties.
        /// </summary>
        [TestMethod]
        public void SortToList_MatchesReferenceOrderForAnyMix()
        {
            int[] downsamples = [1, 2, 4, 8, 16];
            const int currentSection = 50;
            Gen<(bool Visible, int Dz, int Downsample)> tileSpec =
                from isVisible in Arb.Generate<bool>()
                from dz in Gen.Choose(-3, 3)
                from ds in Gen.Elements(downsamples)
                select (isVisible, dz, ds);
            Gen<(bool, int, int)[]> specs = Gen.Choose(1, 12).SelectMany(n => Gen.ArrayOf(n, tileSpec));

            Prop.ForAll(Arb.From(specs), ((bool Visible, int Dz, int Downsample)[] mix) =>
            {
                var items = mix.Select((s, i) =>
                {
                    var tile = CreateTile(
                        currentSection + s.Dz,
                        $"prop-{i}",
                        s.Downsample,
                        s.Visible ? 0 : 5000);
                    return (Id: i, Tile: (TileView?)tile);
                }).ToList();

                var sorted = TextureLoadQueueSort.SortToList(items, DefaultVisible, currentSection, x => x.Tile);

                int[] expected = Enumerable.Range(0, mix.Length)
                    .OrderBy(i => mix[i].Visible ? 0 : 1)
                    .ThenBy(i => Math.Abs(mix[i].Dz))
                    .ThenByDescending(i => mix[i].Downsample)
                    .ToArray();
                CollectionAssert.AreEqual(expected, sorted.Select(x => x.Id).ToArray());
            }).QuickCheckThrowOnFailure();
        }
    }
}
