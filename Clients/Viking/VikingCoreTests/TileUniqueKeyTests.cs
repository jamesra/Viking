using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using Viking.VolumeModel;

namespace VikingCoreTests
{
    /// <summary>
    /// Pins <see cref="TileUniqueKey"/>, the cache key for tile view models and texture queues: null string
    /// inputs coalesce to empty, value equality and hash codes agree, operators mirror <see cref="Equals"/>,
    /// and <see cref="CompareTo"/> is a total order consistent with equality.
    /// </summary>
    [TestClass]
    public class TileUniqueKeyTests
    {
        private static readonly Arbitrary<int> Sections = Gen.Choose(0, 9999).ToArbitrary();
        private static readonly Arbitrary<int> Downsamples = Gen.Choose(1, 512).ToArbitrary();
        private static readonly Arbitrary<string> Names = Arb.Default.NonEmptyString().Generator.Select(s => s.Get).ToArbitrary();
        private static readonly Arbitrary<string?> NullableNames = Gen.OneOf(
            Gen.Constant<string?>(null),
            Names.Generator.Select(s => (string?)s),
            Gen.Constant<string?>(string.Empty)).ToArbitrary();

        private static readonly Arbitrary<TileUniqueKey> AnyKey =
            (from section in Sections.Generator
             from downsample in Downsamples.Generator
             from transform in Names.Generator
             from channel in Names.Generator
             from texture in Names.Generator
             select TileUniqueKey.Create(section, transform, channel, downsample, texture)).ToArbitrary();

        private static int ExpectedCompareTo(TileUniqueKey a, TileUniqueKey b)
        {
            int cmp = a.Section.CompareTo(b.Section);
            if (cmp != 0) return cmp;
            cmp = a.Downsample.CompareTo(b.Downsample);
            if (cmp != 0) return cmp;
            cmp = string.Compare(a.Transform, b.Transform, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            cmp = string.Compare(a.Channel, b.Channel, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return string.Compare(a.TextureName, b.TextureName, StringComparison.Ordinal);
        }

        [TestMethod]
        public void Create_NullStrings_BecomeEmpty()
        {
            var key = TileUniqueKey.Create(1, null, null, 4, null);
            Assert.AreEqual(string.Empty, key.Transform);
            Assert.AreEqual(string.Empty, key.Channel);
            Assert.AreEqual(string.Empty, key.TextureName);
            Assert.AreEqual(TileUniqueKey.Create(1, string.Empty, string.Empty, 4, string.Empty), key);
        }

        [TestMethod]
        public void Constructor_NullStrings_BecomeEmpty()
        {
            var key = new TileUniqueKey(2, null, null, 8, null);
            Assert.AreEqual(string.Empty, key.Transform);
            Assert.AreEqual(string.Empty, key.Channel);
            Assert.AreEqual(string.Empty, key.TextureName);
        }

        [TestMethod]
        public void GetHashCode_DiffersWhenAnySingleFieldDiffers()
        {
            var baseline = TileUniqueKey.Create(10, "Grid", "TEM", 16, "tile.png");
            Assert.AreNotEqual(baseline.GetHashCode(), TileUniqueKey.Create(11, "Grid", "TEM", 16, "tile.png").GetHashCode());
            Assert.AreNotEqual(baseline.GetHashCode(), TileUniqueKey.Create(10, "Grid", "TEM", 32, "tile.png").GetHashCode());
            Assert.AreNotEqual(baseline.GetHashCode(), TileUniqueKey.Create(10, "Stos", "TEM", 16, "tile.png").GetHashCode());
            Assert.AreNotEqual(baseline.GetHashCode(), TileUniqueKey.Create(10, "Grid", "DAPI", 16, "tile.png").GetHashCode());
            Assert.AreNotEqual(baseline.GetHashCode(), TileUniqueKey.Create(10, "Grid", "TEM", 16, "tile-2.png").GetHashCode());
        }

        [TestMethod]
        public void CompareTo_UsesSectionBeforeDownsampleThenStrings()
        {
            var baseKey = TileUniqueKey.Create(5, "M", "C", 8, "a.png");
            Assert.IsTrue(baseKey.CompareTo(TileUniqueKey.Create(6, "M", "C", 1, "a.png")) < 0, "section dominates downsample");
            Assert.IsTrue(baseKey.CompareTo(TileUniqueKey.Create(5, "M", "C", 16, "a.png")) < 0, "downsample before transform");
            Assert.IsTrue(baseKey.CompareTo(TileUniqueKey.Create(5, "N", "C", 8, "a.png")) < 0, "transform before channel");
            Assert.IsTrue(baseKey.CompareTo(TileUniqueKey.Create(5, "M", "D", 8, "a.png")) < 0, "channel before texture");
            Assert.IsTrue(baseKey.CompareTo(TileUniqueKey.Create(5, "M", "C", 8, "b.png")) < 0, "texture last");
        }

        [TestMethod]
        public void DistinctKeys_DifferByOneFieldOnly()
        {
            var baseline = TileUniqueKey.Create(10, "Grid", "TEM", 16, "tile-0-0.png");

            Assert.AreNotEqual(baseline, TileUniqueKey.Create(11, "Grid", "TEM", 16, "tile-0-0.png"));
            Assert.AreNotEqual(baseline, TileUniqueKey.Create(10, "Grid", "TEM", 32, "tile-0-0.png"));
            Assert.AreNotEqual(baseline, TileUniqueKey.Create(10, "Stos", "TEM", 16, "tile-0-0.png"));
            Assert.AreNotEqual(baseline, TileUniqueKey.Create(10, "Grid", "DAPI", 16, "tile-0-0.png"));
            Assert.AreNotEqual(baseline, TileUniqueKey.Create(10, "Grid", "TEM", 16, "tile-1-0.png"));
        }

        [TestMethod]
        public void EqualsAndGetHashCode_AgreeOnGeneratedPairs()
        {
            Prop.ForAll(AnyKey, AnyKey, (a, b) =>
            {
                bool eq = a.Equals(b);
                return eq == b.Equals(a)
                    && eq == (a == b)
                    && eq == !(a != b)
                    && (!eq || a.GetHashCode() == b.GetHashCode());
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void CompareTo_MatchesReferenceOrderAndEquality()
        {
            Prop.ForAll(AnyKey, AnyKey, (a, b) =>
            {
                int cmp = a.CompareTo(b);
                int expected = ExpectedCompareTo(a, b);
                bool eq = a.Equals(b);
                return cmp == expected
                    && Math.Sign(cmp) == Math.Sign(expected)
                    && (eq ? cmp == 0 : cmp != 0);
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void CompareTo_IsTotalOrderOnThreeKeys()
        {
            Prop.ForAll(AnyKey, AnyKey, AnyKey, (a, b, c) =>
            {
                int ab = Math.Sign(a.CompareTo(b));
                int bc = Math.Sign(b.CompareTo(c));
                int ac = Math.Sign(a.CompareTo(c));
                if (ab == 0) return bc == Math.Sign(b.CompareTo(c)) && ac == Math.Sign(a.CompareTo(c));
                if (bc == 0) return ab == Math.Sign(a.CompareTo(c));
                if (ab > 0 && bc > 0) return ac > 0;
                if (ab < 0 && bc < 0) return ac < 0;
                return true;
            }).QuickCheckThrowOnFailure();
        }

        private static readonly Arbitrary<(int Section, int Downsample, string Transform, string Channel, string Texture)> AnyKeyParts =
            (from section in Sections.Generator
             from downsample in Downsamples.Generator
             from transform in Names.Generator
             from channel in Names.Generator
             from texture in Names.Generator
             select (section, downsample, transform, channel, texture)).ToArbitrary();

        private static readonly Arbitrary<(int Section, int Downsample, string? Transform, string? Channel, string? Texture)> AnyNullableKeyParts =
            (from section in Sections.Generator
             from downsample in Downsamples.Generator
             from transform in NullableNames.Generator
             from channel in NullableNames.Generator
             from texture in NullableNames.Generator
             select (section, downsample, transform, channel, texture)).ToArbitrary();

        [TestMethod]
        public void Create_IsStableForSameInputs()
        {
            Prop.ForAll(AnyKeyParts, parts =>
            {
                var a = TileUniqueKey.Create(parts.Section, parts.Transform, parts.Channel, parts.Downsample, parts.Texture);
                var b = TileUniqueKey.Create(parts.Section, parts.Transform, parts.Channel, parts.Downsample, parts.Texture);
                return a.Equals(b) && a.GetHashCode() == b.GetHashCode() && a.CompareTo(b) == 0;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void Create_WithNullableStrings_MatchesEmptyStringKey()
        {
            Prop.ForAll(AnyNullableKeyParts, parts =>
            {
                var withNulls = TileUniqueKey.Create(parts.Section, parts.Transform, parts.Channel, parts.Downsample, parts.Texture);
                var normalized = TileUniqueKey.Create(
                    parts.Section,
                    parts.Transform ?? string.Empty,
                    parts.Channel ?? string.Empty,
                    parts.Downsample,
                    parts.Texture ?? string.Empty);
                return withNulls.Equals(normalized);
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void HashSet_UsesValueEquality()
        {
            var a = TileUniqueKey.Create(3, "Grid", "TEM", 1, "a.png");
            var b = TileUniqueKey.Create(3, "Grid", "TEM", 1, "a.png");
            var set = new HashSet<TileUniqueKey> { a };
            Assert.IsTrue(set.Contains(b));
            Assert.AreEqual(1, set.Count);
        }

        [TestMethod]
        public void EqualsObject_BoxedStruct_MatchesEquals()
        {
            var key = TileUniqueKey.Create(5, "X", "Y", 2, "z.png");
            object boxed = key;
            Assert.IsTrue(key.Equals(boxed));
            Assert.IsFalse(key.Equals((object)TileUniqueKey.Create(6, "X", "Y", 2, "z.png")));
        }
    }
}
