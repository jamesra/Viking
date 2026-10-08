using System;
using System.Collections.Generic;
using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TileViewModel"/> path normalization, identity keyed by <see cref="TileUniqueKey"/>,
    /// world bounds from vertex positions, in-memory <see cref="TileViewModel.Size"/>, and the rough
    /// <see cref="TileViewModel.TextureSize"/> estimate used by tile caches and load queues.
    /// </summary>
    [TestClass]
    public class TileViewModelTests
    {
        private static readonly int[] DefaultTriangulation = [0, 1, 3, 3, 1, 2];

        private static PositionNormalTextureVertex[] UnitSquareAt(double x, double y, double width, double height)
        {
            return
            [
                new(new Vector3(x, y, 0), Vector3.UnitZ, new Vector2(0, 0)),
                new(new Vector3(x + width, y, 0), Vector3.UnitZ, new Vector2(1, 0)),
                new(new Vector3(x + width, y + height, 0), Vector3.UnitZ, new Vector2(1, 1)),
                new(new Vector3(x, y + height, 0), Vector3.UnitZ, new Vector2(0, 1)),
            ];
        }

        private static TileViewModel Create(
            TileUniqueKey key,
            PositionNormalTextureVertex[] verticies,
            string textureFullPath,
            string cachePath,
            int downsample)
        {
            return new TileViewModel(key, verticies, DefaultTriangulation, textureFullPath, cachePath, downsample);
        }

        [TestMethod]
        public void TextureFullPath_NormalizesBackslashesToForwardSlashes()
        {
            var key = TileUniqueKey.Create(1, "Grid", "TEM", 1, "tile.png");
            var model = Create(key, UnitSquareAt(0, 0, 256, 256), @"http://host\vol\Grid\tile.png", "cache.png", 1);
            Assert.AreEqual("http://host/vol/Grid/tile.png", model.TextureFullPath);
        }

        [TestMethod]
        public void TextureCacheFilePath_NormalizesForwardSlashesToBackslashes()
        {
            var key = TileUniqueKey.Create(2, "Grid", "TEM", 4, "tile.png");
            var model = Create(key, UnitSquareAt(0, 0, 128, 128), "http://host/tile.png", @"cache/dir/tile.png", 4);
            Assert.AreEqual(@"cache\dir\tile.png", model.TextureCacheFilePath);
        }

        [TestMethod]
        public void Bounds_MatchesVertexPositionExtents()
        {
            var key = TileUniqueKey.Create(3, "M", "C", 1, "a.png");
            var model = Create(key, UnitSquareAt(100, 200, 512, 384), "http://h/a.png", "a.png", 1);
            Assert.AreEqual(100, model.Bounds.Left);
            Assert.AreEqual(612, model.Bounds.Right);
            Assert.AreEqual(200, model.Bounds.Bottom);
            Assert.AreEqual(584, model.Bounds.Top);
        }

        [TestMethod]
        public void Size_MatchesVertexIndexAndPathFormula()
        {
            var key = TileUniqueKey.Create(7, "Grid", "TEM", 2, "tile-0-0.png");
            const string fullPath = "http://tiles.test/Grid/tile-0-0.png";
            var verticies = UnitSquareAt(0, 0, 256, 256);
            var model = Create(key, verticies, fullPath, "tile-0-0.png", 2);
            int expected = (verticies.Length * 8 * 8) + (DefaultTriangulation.Length * 4) + fullPath.Length + key.ToString().Length;
            Assert.AreEqual(expected, model.Size);
        }

        [TestMethod]
        public void TextureSize_IsIntegerEstimateFromBoundsAndDownsample()
        {
            var key = TileUniqueKey.Create(5, "G", "C", 4, "t.png");
            var model = Create(key, UnitSquareAt(0, 0, 400, 300), "http://h/t.png", "t.png", 4);
            int expected = (int)(model.Bounds.Width / model.Downsample * (model.Bounds.Height / model.Downsample));
            Assert.AreEqual(expected, model.TextureSize);
        }

        [TestMethod]
        public void EqualsAndGetHashCode_DependOnlyOnUniqueKey()
        {
            var key = TileUniqueKey.Create(10, "Grid", "TEM", 8, "same.png");
            var a = Create(key, UnitSquareAt(0, 0, 256, 256), "http://a/same.png", "a.png", 8);
            var b = Create(key, UnitSquareAt(1000, 2000, 64, 64), "http://b/other.png", "b.png", 1);
            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsFalse(a.Equals(null));
            Assert.IsFalse(a.Equals(TileUniqueKey.Create(11, "Grid", "TEM", 8, "same.png")));
        }

        [TestMethod]
        public void HashSet_DeduplicatesByUniqueKeyOnly()
        {
            var key = TileUniqueKey.Create(4, "X", "Y", 1, "z.png");
            var a = Create(key, UnitSquareAt(0, 0, 10, 10), "http://h/z.png", "z.png", 1);
            var b = Create(key, UnitSquareAt(5, 5, 20, 20), "http://h2/z.png", "z2.png", 2);
            var set = new HashSet<TileViewModel> { a };
            Assert.IsTrue(set.Contains(b));
            Assert.AreEqual(1, set.Count);
        }

        private static readonly Arbitrary<(int Section, int Downsample, string Transform, string Channel, string Texture)> AnyKeyParts =
            (from section in Gen.Choose(0, 500).ToArbitrary().Generator
             from downsample in Gen.Choose(1, 64).ToArbitrary().Generator
             from transform in Arb.Default.NonEmptyString().Generator
             from channel in Arb.Default.NonEmptyString().Generator
             from texture in Arb.Default.NonEmptyString().Generator
             select (section, downsample, transform.Get, channel.Get, texture.Get)).ToArbitrary();

        [TestMethod]
        public void PathNormalization_PreservesForwardFullPathAndBackslashCache()
        {
            Prop.ForAll(AnyKeyParts, parts =>
            {
                var key = TileUniqueKey.Create(parts.Section, parts.Transform, parts.Channel, parts.Downsample, parts.Texture);
                const string mixedFull = @"http://host\vol/Grid\tile.png";
                const string mixedCache = @"cache/dir\tile.png";
                var model = Create(key, UnitSquareAt(0, 0, 64, 64), mixedFull, mixedCache, parts.Downsample);
                return model.TextureFullPath == "http://host/vol/Grid/tile.png"
                    && model.TextureCacheFilePath == @"cache\dir\tile.png";
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void EqualsAndGetHashCode_AgreeWhenUniqueKeyMatches()
        {
            Prop.ForAll(AnyKeyParts, parts =>
            {
                var key = TileUniqueKey.Create(parts.Section, parts.Transform, parts.Channel, parts.Downsample, parts.Texture);
                var a = Create(key, UnitSquareAt(0, 0, 32, 32), "http://a/t.png", "a.png", parts.Downsample);
                var b = Create(key, UnitSquareAt(10, 20, 48, 48), "http://b/u.png", "b.png", parts.Downsample + 1);
                return a.Equals(b) && a.GetHashCode() == b.GetHashCode();
            }).QuickCheckThrowOnFailure();
        }
    }
}
