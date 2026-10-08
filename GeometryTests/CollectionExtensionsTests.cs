using FsCheck;
using Geometry;
using GeometryTests.FSCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="CollectionExtensions"/>: the array copy helpers behind polygon vertex insert and remove
    /// (<c>Polygon.InsertVertex</c> and <c>RemoveVertex</c> call InsertIntoClosedRing and RemoveFromClosedRing on the exterior ring)
    /// <c>AddToSet</c>, and <c>GetSetOrEmpty</c>, which <c>DelaunayMesh.RejectedBaselinePairs</c> uses to remember and read rejected baseline pairs.
    /// Closed rings are generated closed (first == last) because the helpers only assert that precondition in Debug.
    /// </summary>
    [TestClass]
    public class CollectionExtensionsTests
    {
        private sealed class ArrayAt
        {
            public int[] Array;
            public int Index;
            public int Value;
        }

        private sealed class Pairs
        {
            public int[] Keys;
            public int[] Values;
        }

        /// <summary>Open array of 0-12 values with an index in 0..Length (valid for Insert) and a value to insert.</summary>
        private static Arbitrary<ArrayAt> ArbOpenArray() =>
            Arb.From(
                from values in Gen.ArrayOf(Gen.Choose(-1000, 1000)).Resize(12)
                from index in Gen.Choose(0, values.Length)
                from value in Gen.Choose(-1000, 1000)
                select new ArrayAt { Array = values, Index = index, Value = value });

        /// <summary>
        /// Closed ring of 3-10 distinct body vertices plus the repeated first vertex, with an index in 0..Length-1 (the
        /// range of Polygon vertex indices, including the duplicated closing vertex) and a value to insert.
        /// </summary>
        private static Arbitrary<ArrayAt> ArbClosedRing() =>
            Arb.From(
                from count in Gen.Choose(3, 10)
                from body in Gen.Shuffle(Enumerable.Range(0, 20)).Select(s => s.Take(count).ToArray())
                let ring = body.Concat(new[] { body[0] }).ToArray()
                from index in Gen.Choose(0, ring.Length - 1)
                from value in Gen.Choose(100, 200)
                select new ArrayAt { Array = ring, Index = index, Value = value });

        private static Arbitrary<Pairs> ArbPairs() =>
            Arb.From(
                from count in Gen.Choose(0, 40)
                from keys in Gen.ArrayOf(count, Gen.Choose(0, 5))
                from values in Gen.ArrayOf(count, Gen.Choose(0, 8))
                select new Pairs { Keys = keys, Values = values });

        [TestMethod]
        public void AddThenRemoveLastRoundTripsAndLeavesSourceUntouched() =>
            CoreCheck.Run(
                Prop.ForAll(ArbOpenArray(), a =>
                {
                    int[] before = (int[])a.Array.Clone();
                    int[] added = a.Array.Add(a.Value);
                    return added.Length == before.Length + 1
                        && added[before.Length] == a.Value
                        && added.RemoveAt(before.Length).SequenceEqual(before)
                        && a.Array.SequenceEqual(before);
                }),
                nameof(AddThenRemoveLastRoundTripsAndLeavesSourceUntouched));

        [TestMethod]
        public void InsertThenRemoveAtRoundTrips() =>
            CoreCheck.Run(
                Prop.ForAll(ArbOpenArray(), a =>
                {
                    int[] inserted = a.Array.Insert(a.Index, a.Value);
                    return inserted.Length == a.Array.Length + 1
                        && inserted[a.Index] == a.Value
                        && inserted.RemoveAt(a.Index).SequenceEqual(a.Array);
                }),
                nameof(InsertThenRemoveAtRoundTrips));

        [TestMethod]
        public void AddRangeAppendsInOrderWithoutMutatingInputs() =>
            CoreCheck.Run(
                Prop.ForAll(ArbOpenArray(), ArbOpenArray(), (a, b) =>
                {
                    int[] leftBefore = (int[])a.Array.Clone();
                    int[] rightBefore = (int[])b.Array.Clone();
                    int[] joined = a.Array.AddRange(b.Array);
                    return joined.SequenceEqual(leftBefore.Concat(rightBefore))
                        && a.Array.SequenceEqual(leftBefore)
                        && b.Array.SequenceEqual(rightBefore);
                }),
                nameof(AddRangeAppendsInOrderWithoutMutatingInputs));

        [TestMethod]
        public void InsertIntoClosedRingKeepsRingClosedAndAddsOneVertex() =>
            CoreCheck.Run(
                Prop.ForAll(ArbClosedRing(), a =>
                {
                    int[] output = a.Array.InsertIntoClosedRing(a.Index, a.Value);
                    return output.Length == a.Array.Length + 1
                        && output[0] == output[output.Length - 1]
                        && output[a.Index] == a.Value;
                }),
                nameof(InsertIntoClosedRingKeepsRingClosedAndAddsOneVertex));

        [TestMethod]
        public void RemoveFromClosedRingKeepsRingClosedAndDropsOneVertex() =>
            CoreCheck.Run(
                Prop.ForAll(ArbClosedRing(), a =>
                {
                    int[] output = a.Array.RemoveFromClosedRing(a.Index);
                    return output.Length == a.Array.Length - 1
                        && output[0] == output[output.Length - 1];
                }),
                nameof(RemoveFromClosedRingKeepsRingClosedAndDropsOneVertex));

        [TestMethod]
        public void InsertThenRemoveOnClosedRingRestoresTheRing() =>
            CoreCheck.Run(
                Prop.ForAll(ArbClosedRing(), a =>
                    a.Array.InsertIntoClosedRing(a.Index, a.Value)
                        .RemoveFromClosedRing(a.Index)
                        .SequenceEqual(a.Array)),
                nameof(InsertThenRemoveOnClosedRingRestoresTheRing));

        [TestMethod]
        public void AddToSetMatchesGroupedDistinctValues() =>
            CoreCheck.Run(
                Prop.ForAll(ArbPairs(), p =>
                {
                    Dictionary<int, SortedSet<int>> dict = new Dictionary<int, SortedSet<int>>();
                    for (int i = 0; i < p.Keys.Length; i++)
                        dict.AddToSet(p.Keys[i], p.Values[i]);

                    var expected = p.Keys.Zip(p.Values, (k, v) => new { k, v })
                        .GroupBy(x => x.k)
                        .ToDictionary(g => g.Key, g => g.Select(x => x.v).Distinct().OrderBy(v => v).ToArray());

                    return dict.Count == expected.Count
                        && expected.All(kv => dict.TryGetValue(kv.Key, out SortedSet<int> set) && set.SequenceEqual(kv.Value));
                }),
                nameof(AddToSetMatchesGroupedDistinctValues));

        [TestMethod]
        public void InsertIntoClosedRingAtZeroReplacesTheClosingVertex()
        {
            int[] output = new[] { 1, 2, 3, 1 }.InsertIntoClosedRing(0, 9);
            CollectionAssert.AreEqual(new[] { 9, 1, 2, 3, 9 }, output);
        }

        [TestMethod]
        public void InsertIntoClosedRingAtLastIndexInsertsBeforeTheClosingVertex()
        {
            int[] output = new[] { 1, 2, 3, 1 }.InsertIntoClosedRing(3, 9);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 9, 1 }, output);
        }

        [TestMethod]
        public void InsertIntoClosedRingInTheMiddleLeavesEndsAlone()
        {
            int[] output = new[] { 1, 2, 3, 1 }.InsertIntoClosedRing(2, 9);
            CollectionAssert.AreEqual(new[] { 1, 2, 9, 3, 1 }, output);
        }

        [TestMethod]
        public void RemoveFromClosedRingAtZeroMovesTheNewFirstVertexToTheTail()
        {
            int[] output = new[] { 1, 2, 3, 1 }.RemoveFromClosedRing(0);
            CollectionAssert.AreEqual(new[] { 2, 3, 2 }, output);
        }

        /// <summary>The duplicate closing vertex is the same vertex as index 0, so removing it removes the ring's first vertex.</summary>
        [TestMethod]
        public void RemoveFromClosedRingAtLastIndexRemovesTheFirstVertex()
        {
            int[] output = new[] { 1, 2, 3, 1 }.RemoveFromClosedRing(3);
            CollectionAssert.AreEqual(new[] { 2, 3, 2 }, output);
        }

        [TestMethod]
        public void RemoveFromClosedRingInTheMiddleLeavesEndsAlone()
        {
            int[] output = new[] { 1, 2, 3, 4, 1 }.RemoveFromClosedRing(2);
            CollectionAssert.AreEqual(new[] { 1, 2, 4, 1 }, output);
        }

        [TestMethod]
        public void AddToSetCreatesThenMergesAndKeepsSetsPerKey()
        {
            Dictionary<int, SortedSet<int>> dict = new Dictionary<int, SortedSet<int>>();

            dict.AddToSet(1, 5);
            dict.AddToSet(2, 5);
            dict.AddToSet(1, 3);
            dict.AddToSet(1, 5);

            Assert.AreEqual(2, dict.Count);
            CollectionAssert.AreEqual(new[] { 3, 5 }, dict[1].ToArray());
            CollectionAssert.AreEqual(new[] { 5 }, dict[2].ToArray());
            Assert.AreNotSame(dict[1], dict[2]);
        }

        [TestMethod]
        public void AddToSetKeepsExistingSetInstance()
        {
            SortedSet<int> existing = new SortedSet<int> { 7 };
            Dictionary<int, SortedSet<int>> dict = new Dictionary<int, SortedSet<int>> { { 4, existing } };

            dict.AddToSet(4, 2);

            Assert.AreSame(existing, dict[4]);
            CollectionAssert.AreEqual(new[] { 2, 7 }, existing.ToArray());
        }

        [TestMethod]
        public void GetSetOrEmptyReturnsStoredSetWithoutAddingKey()
        {
            SortedSet<int> stored = new SortedSet<int> { 1, 3 };
            Dictionary<int, SortedSet<int>> dict = new Dictionary<int, SortedSet<int>> { { 2, stored } };

            Assert.AreSame(stored, dict.GetSetOrEmpty(2));
            Assert.IsFalse(dict.ContainsKey(9));
            Assert.AreEqual(0, dict.GetSetOrEmpty(9).Count);
            Assert.IsFalse(dict.ContainsKey(9));
        }

        [TestMethod]
        public void GetSetOrEmptyMatchesTryGetValueOrEmpty() =>
            CoreCheck.Run(
                Prop.ForAll(ArbPairs(), p =>
                {
                    Dictionary<int, SortedSet<int>> dict = new Dictionary<int, SortedSet<int>>();
                    for (int i = 0; i < p.Keys.Length; i++)
                        dict.AddToSet(p.Keys[i], p.Values[i]);

                    int probeKey = p.Keys.Length > 0 ? p.Keys[0] : 0;
                    SortedSet<int> fromHelper = dict.GetSetOrEmpty(probeKey);
                    bool found = dict.TryGetValue(probeKey, out SortedSet<int>? fromDict);
                    if (!found)
                        return fromHelper.Count == 0;

                    return ReferenceEquals(fromDict, fromHelper)
                        && fromHelper.SequenceEqual(fromDict);
                }),
                nameof(GetSetOrEmptyMatchesTryGetValueOrEmpty));

        [TestMethod]
        public void AddAndAddRangeOnEmptyArrays()
        {
            CollectionAssert.AreEqual(new[] { 4 }, new int[0].Add(4));
            CollectionAssert.AreEqual(new[] { 1, 2 }, new int[0].AddRange(new[] { 1, 2 }));
            CollectionAssert.AreEqual(new[] { 1, 2 }, new[] { 1, 2 }.AddRange(new int[0]));
            CollectionAssert.AreEqual(new int[0], new[] { 5 }.RemoveAt(0));
        }
    }
}
