using FsCheck;
using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="IndexingExtensions.CombinationPairs{T}(IReadOnlyList{T})"/> and its two-list overload, which
    /// the Delaunay 3D test view uses to walk every pair of cell vertices and face indices. The single-list form
    /// yields each unordered pair once (i &lt; j, row-major); the two-list form yields the full cross product.
    /// </summary>
    [TestClass]
    public class IndexingExtensionsTests
    {
        [TestMethod]
        public void SingleListEmptyOrOneElementYieldsNoPairs()
        {
            Assert.AreEqual(0, new int[0].CombinationPairs().Count());
            Assert.AreEqual(0, new[] { 7 }.CombinationPairs().Count());
        }

        [TestMethod]
        public void SingleListFourElementsYieldsSixPairsInRowMajorOrder()
        {
            string[] items = ["a", "b", "c", "d"];

            string[] pairs = [.. items.CombinationPairs().Select(c => $"{c.iA}{c.iB}:{c.A}{c.B}")];

            CollectionAssert.AreEqual(new[] { "01:ab", "02:ac", "03:ad", "12:bc", "13:bd", "23:cd" }, pairs);
        }

        [TestMethod]
        public void TwoListsYieldCrossProductWithFirstListOuter()
        {
            int[] a = [1, 2];
            int[] b = [10, 20, 30];

            string[] pairs = [.. a.CombinationPairs(b).Select(c => $"{c.iA}{c.iB}:{c.A}-{c.B}")];

            CollectionAssert.AreEqual(new[] { "00:1-10", "01:1-20", "02:1-30", "10:2-10", "11:2-20", "12:2-30" }, pairs);
        }

        [TestMethod]
        public void TwoListsWithAnEmptyListYieldNothing()
        {
            Assert.AreEqual(0, new[] { 1, 2 }.CombinationPairs(new int[0]).Count());
            Assert.AreEqual(0, new int[0].CombinationPairs(new[] { 1, 2 }).Count());
        }

        [TestMethod]
        public void SingleListPairsAreEveryUnorderedIndexPairOnce()
        {
            Prop.ForAll(Arb.From(Gen.Choose(0, 40)), n =>
            {
                int[] items = [.. Enumerable.Range(100, n)];
                List<(int, int)> indices = [.. items.CombinationPairs().Select(c => (c.iA, c.iB))];

                bool count = indices.Count == n * (n - 1) / 2;
                bool ordered = indices.All(p => p.Item1 < p.Item2 && p.Item2 < n);
                bool unique = indices.Distinct().Count() == indices.Count;
                bool values = items.CombinationPairs().All(c => c.A == items[c.iA] && c.B == items[c.iB]);
                return count && ordered && unique && values;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void TwoListPairCountIsProductOfLengths()
        {
            Prop.ForAll(Arb.From(Gen.Choose(0, 25)), Arb.From(Gen.Choose(0, 25)), (n, m) =>
            {
                int[] a = [.. Enumerable.Range(0, n)];
                int[] b = [.. Enumerable.Range(0, m)];
                return a.CombinationPairs(b).Count() == n * m
                    && a.CombinationPairs(b).All(c => c.A == a[c.iA] && c.B == b[c.iB]);
            }).QuickCheckThrowOnFailure();
        }
    }
}
