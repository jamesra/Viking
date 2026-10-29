using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="SortingExtensions.SortAndIndex{T}(T[], IComparer{T})"/> (in-place sort that returns, for each
    /// sorted position, the index the element had before the sort) and <see cref="SortingExtensions.IndexOf{T}"/>
    /// (index of the first equal item, or -1). The sort is not stable, so tests with equal keys check the mapping back
    /// to the original items rather than a particular order among them.
    /// </summary>
    [TestClass]
    public class SortingExtensionsTests
    {
        /// <summary>An item whose sort key can repeat while its <see cref="Id"/> stays unique.</summary>
        private sealed class Item
        {
            public Item(int key, int id)
            {
                Key = key;
                Id = id;
            }

            public int Key { get; }

            public int Id { get; }
        }

        private sealed class KeyComparer : IComparer<Item>
        {
            public int Compare(Item x, Item y) => x.Key.CompareTo(y.Key);
        }

        private sealed class DescendingComparer : IComparer<int>
        {
            public int Compare(int x, int y) => y.CompareTo(x);
        }

        [TestMethod]
        public void SortAndIndexSortsInPlaceAndReturnsOriginalIndices()
        {
            int[] values = [30, 10, 20];

            int[] keys = values.SortAndIndex();

            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, values);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, keys);
        }

        [TestMethod]
        public void SortAndIndexAlreadySortedReturnsIdentity()
        {
            int[] values = [1, 2, 3, 4];

            int[] keys = values.SortAndIndex();

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, values);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, keys);
        }

        [TestMethod]
        public void SortAndIndexCustomComparerSortsDescending()
        {
            int[] values = [5, 9, 1, 7];

            int[] keys = values.SortAndIndex(new DescendingComparer());

            CollectionAssert.AreEqual(new[] { 9, 7, 5, 1 }, values);
            CollectionAssert.AreEqual(new[] { 1, 3, 0, 2 }, keys);
        }

        [TestMethod]
        public void SortAndIndexNullComparerUsesDefaultOrdering()
        {
            string[] values = ["pear", "apple", "fig"];

            int[] keys = values.SortAndIndex(null);

            CollectionAssert.AreEqual(new[] { "apple", "fig", "pear" }, values);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, keys);
        }

        [TestMethod]
        public void SortAndIndexEmptyArrayReturnsEmptyKeys()
        {
            int[] values = [];

            int[] keys = values.SortAndIndex();

            Assert.AreEqual(0, keys.Length);
            Assert.AreEqual(0, values.Length);
        }

        [TestMethod]
        public void SortAndIndexSingleElementReturnsKeyZero()
        {
            int[] values = [42];

            int[] keys = values.SortAndIndex();

            CollectionAssert.AreEqual(new[] { 42 }, values);
            CollectionAssert.AreEqual(new[] { 0 }, keys);
        }

        [TestMethod]
        public void SortAndIndexSingleElementWithComparerReturnsKeyZero()
        {
            int[] values = [42];

            int[] keys = values.SortAndIndex(new DescendingComparer());

            CollectionAssert.AreEqual(new[] { 0 }, keys);
        }

        [TestMethod]
        public void SortAndIndexAllEqualReturnsPermutationOfIndices()
        {
            int[] values = [7, 7, 7, 7, 7];

            int[] keys = values.SortAndIndex();

            CollectionAssert.AreEqual(new[] { 7, 7, 7, 7, 7 }, values);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4 }, keys);
        }

        [TestMethod]
        public void SortAndIndexDuplicateKeysMapEachSortedItemBackToItsOriginalPosition()
        {
            Item[] original = [new(2, 0), new(1, 1), new(2, 2), new(1, 3), new(0, 4)];
            Item[] values = (Item[])original.Clone();

            int[] keys = values.SortAndIndex(new KeyComparer());

            Assert.IsTrue(values.Zip(values.Skip(1), (a, b) => a.Key <= b.Key).All(ok => ok));
            for (int i = 0; i < values.Length; i++)
                Assert.AreSame(original[keys[i]], values[i], $"sorted position {i}");
        }

        [TestMethod]
        public void SortAndIndexKeysAreAPermutationThatMapsBackToTheOriginalValues()
        {
            Gen<int[]> cases = Gen.ArrayOf(Gen.Choose(-1000, 1000));

            CoreCheck.Run(
                Prop.ForAll(Arb.From(cases), original =>
                {
                    int[] values = (int[])original.Clone();

                    int[] keys = values.SortAndIndex();

                    bool sorted = values.Zip(values.Skip(1), (a, b) => a <= b).All(ok => ok);
                    bool permutation = keys.OrderBy(k => k).SequenceEqual(Enumerable.Range(0, original.Length));
                    bool mapsBack = keys.Select((k, i) => original[k] == values[i]).All(ok => ok);
                    return (sorted && permutation && mapsBack).ToProperty();
                }),
                nameof(SortAndIndexKeysAreAPermutationThatMapsBackToTheOriginalValues));
        }

        [TestMethod]
        public void SortAndIndexWithComparerMapsBackAndOrdersByComparer()
        {
            Gen<Item[]> cases =
                from keysSeed in Gen.ArrayOf(Gen.Choose(0, 5))
                select keysSeed.Select((k, id) => new Item(k, id)).ToArray();

            CoreCheck.Run(
                Prop.ForAll(Arb.From(cases), original =>
                {
                    Item[] values = (Item[])original.Clone();

                    int[] keys = values.SortAndIndex(new KeyComparer());

                    bool sorted = values.Zip(values.Skip(1), (a, b) => a.Key <= b.Key).All(ok => ok);
                    bool permutation = keys.OrderBy(k => k).SequenceEqual(Enumerable.Range(0, original.Length));
                    bool mapsBack = keys.Select((k, i) => ReferenceEquals(original[k], values[i])).All(ok => ok);
                    return (sorted && permutation && mapsBack).ToProperty();
                }),
                nameof(SortAndIndexWithComparerMapsBackAndOrdersByComparer));
        }

        [TestMethod]
        public void IndexOfReturnsPositionOfFirstMatch()
        {
            int[] values = [4, 8, 15, 8, 16];

            Assert.AreEqual(0, values.IndexOf(4));
            Assert.AreEqual(1, values.IndexOf(8));
            Assert.AreEqual(4, values.IndexOf(16));
        }

        [TestMethod]
        public void IndexOfReturnsMinusOneWhenMissing()
        {
            Assert.AreEqual(-1, new[] { 4, 8, 15 }.IndexOf(99));
        }

        [TestMethod]
        public void IndexOfEmptySequenceReturnsMinusOne()
        {
            Assert.AreEqual(-1, Array.Empty<int>().IndexOf(0));
            Assert.AreEqual(-1, Enumerable.Empty<string>().IndexOf("a"));
        }

        [TestMethod]
        public void IndexOfWorksOnAnyEnumerableAndStopsAtFirstMatch()
        {
            int pulled = 0;
            IEnumerable<int> Lazy()
            {
                for (int i = 0; i < 100; i++)
                {
                    pulled++;
                    yield return i;
                }
            }

            Assert.AreEqual(3, Lazy().IndexOf(3));
            Assert.AreEqual(4, pulled, "enumeration must stop at the first match");
        }

        [TestMethod]
        public void IndexOfUsesValueEqualityNotReference()
        {
            string[] values = ["alpha", new string(['b', 'e', 't', 'a']), "gamma"];

            Assert.AreEqual(1, values.IndexOf("beta"));
        }

        [TestMethod]
        public void IndexOfMatchesArrayIndexOf()
        {
            Gen<Tuple<int[], int>> cases =
                from values in Gen.ArrayOf(Gen.Choose(0, 20))
                from probe in Gen.Choose(-2, 22)
                select Tuple.Create(values, probe);

            CoreCheck.Run(
                Prop.ForAll(Arb.From(cases), c => (c.Item1.IndexOf(c.Item2) == Array.IndexOf(c.Item1, c.Item2)).ToProperty()),
                nameof(IndexOfMatchesArrayIndexOf));
        }
    }
}
