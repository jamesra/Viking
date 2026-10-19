using Geometry;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryTests
{
    /// <summary>
    /// Pins <see cref="DistanceToPoint{T}"/>, <see cref="DistanceList{T}"/> and <see cref="FixedSizeDistanceList{T}"/>,
    /// the nearest-neighbour result buffers that <c>QuadTree.FindNearestPoints</c> fills through <c>QuadTreeNode.FindNearestPoints</c>.
    /// The fixed-size list keeps the closest entries up to its capacity, but keeps every entry of a tied furthest distance
    /// when dropping them would leave it under capacity.
    /// </summary>
    [TestClass]
    public class DistanceListTests
    {
        private static DistanceToPoint<int> Item(double distance, int value = 0, double x = 0) => new(new Vector2(x, 0), distance, value);

        private static double[] Distances<T>(DistanceList<T> list) => list.Data.Keys.ToArray();

        #region DistanceToPoint

        [TestMethod]
        public void DistanceToPoint_CompareToAndOperatorsOrderByDistanceOnly()
        {
            DistanceToPoint<int> near = Item(1, value: 5, x: 100);
            DistanceToPoint<int> far = Item(2, value: 1, x: -100);
            DistanceToPoint<int> tiedNear = Item(1, value: 9, x: 7);

            Assert.IsTrue(near.CompareTo(far) < 0);
            Assert.IsTrue(far.CompareTo(near) > 0);
            Assert.AreEqual(0, near.CompareTo(tiedNear));
            Assert.IsTrue(near < far);
            Assert.IsTrue(far > near);
            Assert.IsTrue(near <= tiedNear);
            Assert.IsTrue(near >= tiedNear);
            Assert.IsFalse(near < tiedNear);
            Assert.IsFalse(near > tiedNear);
            Assert.IsFalse(far <= near);
            Assert.IsFalse(near >= far);
        }

        [TestMethod]
        public void DistanceToPoint_EqualityUsesPointAndDistanceAndIgnoresValue()
        {
            DistanceToPoint<int> a = Item(1, value: 1, x: 3);

            Assert.IsTrue(a.Equals(Item(1, value: 2, x: 3)));
            Assert.IsTrue(a == Item(1, value: 2, x: 3));
            Assert.IsTrue(a.Equals((object)Item(1, value: 2, x: 3)));
            Assert.AreEqual(a.GetHashCode(), Item(1, value: 2, x: 3).GetHashCode());

            Assert.IsFalse(a.Equals(Item(1, value: 1, x: 4)), "A different point is a different entry");
            Assert.IsFalse(a.Equals(Item(2, value: 1, x: 3)), "A different distance is a different entry");
            Assert.IsTrue(a != Item(2, value: 1, x: 3));
            Assert.IsFalse(a.Equals("not a DistanceToPoint"));
            Assert.IsFalse(a.Equals((object)null));
            Assert.IsFalse(a.Equals((object)new DistanceToPoint<string>(new Vector2(3, 0), 1, "x")), "A different value type never matches");
        }

        [TestMethod]
        public void DistanceToPoint_ComparesEqualWithoutBeingEqual()
        {
            DistanceToPoint<int> a = Item(1, x: 0);
            DistanceToPoint<int> b = Item(1, x: 5);

            Assert.AreEqual(0, a.CompareTo(b));
            Assert.IsFalse(a == b);
        }

        [TestMethod]
        public void DistanceToPoint_ToStringShowsPointAndDistance()
        {
            DistanceToPoint<int> a = new(new Vector2(1, 2), 3, 4);
            Assert.AreEqual(new Vector2(1, 2).ToString() + " 3", a.ToString());
        }

        [TestMethod]
        public void DistanceToPointSorter_OrdersByDistance()
        {
            DistanceToPointSorter<int> sorter = new();
            List<DistanceToPoint<int>> items = [Item(3, 3), Item(1, 1), Item(2, 2)];
            items.Sort(sorter);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, items.Select(i => i.Value).ToArray());
            Assert.AreEqual(0, sorter.Compare(Item(1, x: 0), Item(1, x: 9)));
        }

        #endregion

        #region DistanceList

        [TestMethod]
        public void DistanceList_EmptyListHasNoEntriesAndMinimumMaxDistance()
        {
            DistanceList<int> list = new(4);

            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(0, list.Data.Count);
            Assert.AreEqual(double.MinValue, list.MaxDistance);
            Assert.ThrowsException<KeyNotFoundException>(() => list[1.0]);
        }

        [TestMethod]
        public void DistanceList_KeepsDistancesSortedAscendingWhateverTheInsertOrder()
        {
            DistanceList<int> list = new(4);
            foreach (double d in new[] { 5.0, 1.0, 3.0, 4.0, 2.0 })
                list.Add(new Vector2(d, 0), d, (int)d);

            CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }, Distances(list));
            Assert.AreEqual(5, list.Count);
            Assert.AreEqual(5.0, list.MaxDistance);
        }

        [TestMethod]
        public void DistanceList_MaxDistanceOnlyGrows()
        {
            DistanceList<int> list = new(4);
            list.Add(new Vector2(0, 0), 5, 0);
            Assert.AreEqual(5.0, list.MaxDistance);

            list.Add(new Vector2(1, 0), 2, 1);
            Assert.AreEqual(5.0, list.MaxDistance, "A closer entry does not change the maximum");

            list.Add(new Vector2(2, 0), 9, 2);
            Assert.AreEqual(9.0, list.MaxDistance);
        }

        [TestMethod]
        public void DistanceList_EntriesAtTheSameDistanceShareOneBucketInInsertOrder()
        {
            DistanceList<int> list = new(4);
            list.Add(new Vector2(1, 0), 2, 10);
            list.Add(new Vector2(0, 1), 2, 11);
            list.Add(new Vector2(-1, 0), 2, 12);

            Assert.AreEqual(3, list.Count, "Count is per entry, not per distance");
            Assert.AreEqual(1, list.Data.Count);
            CollectionAssert.AreEqual(new[] { 10, 11, 12 }, list[2.0].Select(i => i.Value).ToArray());
            Assert.AreEqual(2.0, list.MaxDistance);
        }

        [TestMethod]
        public void DistanceList_AddByComponentsStoresPointDistanceAndValue()
        {
            DistanceList<string> list = new(1);
            list.Add(new Vector2(3, 4), 5, "value");

            DistanceToPoint<string> stored = list[5.0].Single();
            Assert.AreEqual(new Vector2(3, 4), stored.Point);
            Assert.AreEqual(5.0, stored.Distance);
            Assert.AreEqual("value", stored.Value);
        }

        [TestMethod]
        public void DistanceList_ZeroDistanceIsOrderedFirstAndRaisesMaxFromMinValue()
        {
            DistanceList<int> list = new(2);
            list.Add(new Vector2(0, 0), 0, 1);
            Assert.AreEqual(0.0, list.MaxDistance);

            list.Add(new Vector2(1, 0), 1, 2);
            CollectionAssert.AreEqual(new[] { 0.0, 1.0 }, Distances(list));
        }

        #endregion

        #region FixedSizeDistanceList

        [TestMethod]
        public void FixedSize_FillsToCapacityAcceptingEveryEntry()
        {
            FixedSizeDistanceList<int> list = new(3);

            Assert.IsTrue(list.TryAdd(Item(3, 3)));
            Assert.IsTrue(list.TryAdd(Item(1, 1)));
            Assert.IsTrue(list.TryAdd(Item(2, 2)));

            Assert.AreEqual(3, list.MaxCapacity);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(3.0, list.MaxDistance);
            CollectionAssert.AreEqual(new[] { 1.0, 2.0, 3.0 }, Distances(list));
        }

        [TestMethod]
        public void FixedSize_RejectsEntriesThatAreFurtherOrTiedWithTheMaximumWhenFull()
        {
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2));
            list.TryAdd(Item(3, 3));

            Assert.IsFalse(list.TryAdd(Item(4, 4)), "Further than every entry");
            Assert.IsFalse(list.TryAdd(Item(3, 5, x: 9)), "Tied with the maximum is not closer");

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(3.0, list.MaxDistance);
            CollectionAssert.AreEqual(new[] { 3 }, list[3.0].Select(i => i.Value).ToArray());
        }

        [TestMethod]
        public void FixedSize_ACloserEntryEvictsTheFurthestWhenFull()
        {
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2));
            list.TryAdd(Item(3, 3));

            Assert.IsTrue(list.TryAdd(Item(0.5, 0)));

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(2.0, list.MaxDistance);
            CollectionAssert.AreEqual(new[] { 0.5, 1.0, 2.0 }, Distances(list));
        }

        [TestMethod]
        public void FixedSize_EvictsFurthestBucketsOneAtATimeWhileCapacityStillHolds()
        {
            // Capacity 3 with buckets {1}, {2}, {3}: adding 1.5 leaves 4 entries and drops {3}.
            // Adding 0.5 leaves 4 again and drops {2}, which leaves exactly 3.
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2));
            list.TryAdd(Item(3, 3));
            list.TryAdd(Item(1.5, 15));
            Assert.AreEqual(2.0, list.MaxDistance);
            list.TryAdd(Item(0.5, 5));

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(1.5, list.MaxDistance);
            CollectionAssert.AreEqual(new[] { 0.5, 1.0, 1.5 }, Distances(list));
        }

        [TestMethod]
        public void FixedSize_KeepsATiedFurthestBucketThatWouldLeaveTheListUnderCapacity()
        {
            // Capacity 3 holding {1}, {2, 2}. Adding 0.5 gives 4 entries; dropping both 2s would leave 2 (< 3), so they stay.
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2, x: 1));
            list.TryAdd(Item(2, 3, x: 2));

            Assert.IsTrue(list.TryAdd(Item(0.5, 0)));

            Assert.AreEqual(4, list.Count, "Equidistant entries may take the list past capacity");
            Assert.AreEqual(2.0, list.MaxDistance);
            Assert.AreEqual(2, list[2.0].Count);
            CollectionAssert.AreEqual(new[] { 0.5, 1.0, 2.0 }, Distances(list));
        }

        [TestMethod]
        public void FixedSize_DropsAMultiEntryFurthestBucketWhenTheRemainderStillFillsCapacity()
        {
            // Capacity 3 holding {0.5}, {1}, {2, 2} (4 entries, kept because of the tie). One more closer entry
            // leaves 5; dropping both 2s leaves 3, which still fills capacity, so the whole tied bucket goes.
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2, x: 1));
            list.TryAdd(Item(2, 3, x: 2));
            list.TryAdd(Item(0.5, 0));
            Assert.AreEqual(4, list.Count);

            Assert.IsTrue(list.TryAdd(Item(0.2, 9)));

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(1.0, list.MaxDistance);
            CollectionAssert.AreEqual(new[] { 0.2, 0.5, 1.0 }, Distances(list));
        }

        [TestMethod]
        public void FixedSize_ACloserTieJoinsAnExistingBucketBelowTheMaximum()
        {
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2));
            list.TryAdd(Item(3, 3));

            Assert.IsTrue(list.TryAdd(Item(1, 10, x: 5)));

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(2.0, list.MaxDistance);
            CollectionAssert.AreEqual(new[] { 1, 10 }, list[1.0].Select(i => i.Value).ToArray());
        }

        [TestMethod]
        public void FixedSize_TiesAtTheMaximumBeforeFullAreAllKept()
        {
            FixedSizeDistanceList<int> list = new(3);
            list.TryAdd(Item(1, 1));
            list.TryAdd(Item(2, 2, x: 1));
            list.TryAdd(Item(2, 3, x: 2));

            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(2, list[2.0].Count);
            Assert.IsFalse(list.TryAdd(Item(2, 4, x: 3)), "Full now, and a tie with the maximum is rejected");
            Assert.AreEqual(2, list[2.0].Count);
        }

        [TestMethod]
        public void FixedSize_CapacityOneKeepsOnlyTheClosest()
        {
            FixedSizeDistanceList<int> list = new(1);

            Assert.IsTrue(list.TryAdd(Item(5, 5)));
            Assert.IsTrue(list.TryAdd(Item(2, 2)));
            Assert.IsFalse(list.TryAdd(Item(3, 3)));
            Assert.IsTrue(list.TryAdd(Item(1, 1)));

            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(1.0, list.MaxDistance);
            Assert.AreEqual(1, list[1.0].Single().Value);
        }

        [TestMethod]
        public void FixedSize_CapacityZeroNeverAccepts()
        {
            FixedSizeDistanceList<int> list = new(0);

            Assert.IsFalse(list.TryAdd(Item(0, 0)));
            Assert.IsFalse(list.TryAdd(Item(-1, 1)));
            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(double.MinValue, list.MaxDistance);
        }

        [TestMethod]
        public void FixedSize_KeepsTheClosestEntriesForRandomInsertOrders()
        {
            Random random = new(12345);
            for (int trial = 0; trial < 200; trial++)
            {
                int capacity = random.Next(1, 8);
                int n = random.Next(0, 40);
                double[] distances = Enumerable.Range(0, n).Select(_ => (double)random.Next(0, 15)).ToArray();

                FixedSizeDistanceList<int> list = new(capacity);
                for (int i = 0; i < n; i++)
                    list.TryAdd(Item(distances[i], i, x: i));

                double[] sorted = distances.OrderBy(d => d).ToArray();
                List<double> kept = list.Data.SelectMany(kv => kv.Value.Select(_ => kv.Key)).ToList();
                string where = " (trial " + trial + ")";

                Assert.AreEqual(list.Count, kept.Count, "Count matches stored entries" + where);
                Assert.IsTrue(kept.Count >= Math.Min(capacity, n), "At least capacity entries are kept" + where);
                // The kept entries are always a prefix of the sorted input.
                CollectionAssert.AreEqual(sorted.Take(kept.Count).ToArray(), kept.ToArray(), "Kept entries are the closest" + where);
                if (kept.Count > capacity)
                    Assert.AreEqual(list.Data.Last().Value.Count, kept.Count - kept.Count(d => d < list.MaxDistance), "Only the tied maximum bucket exceeds capacity" + where);
            }
        }

        #endregion

        #region QuadTree.FindNearestPoints usage

        private static QuadTree<int> LineTree(int count)
        {
            QuadTree<int> tree = new(new Rectangle(-100, 100, -100, 100));
            for (int i = 0; i < count; i++)
                tree.Add(new Vector2(i, 0), i);
            return tree;
        }

        [TestMethod]
        public void QuadTree_FindNearestPointsReturnsTheNearestInAscendingDistance()
        {
            QuadTree<int> tree = LineTree(10);

            List<DistanceToPoint<int>> found = tree.FindNearestPoints(new Vector2(0.25, 0), 3);

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, found.Select(f => f.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 0.25, 0.75, 1.75 }, found.Select(f => f.Distance).ToArray());
        }

        [TestMethod]
        public void QuadTree_FindNearestPointsReturnsEverythingWhenAskedForMoreThanItHolds()
        {
            QuadTree<int> tree = LineTree(4);

            List<DistanceToPoint<int>> found = tree.FindNearestPoints(new Vector2(0, 0), 10);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, found.Select(f => f.Value).ToArray());
        }

        [TestMethod]
        public void QuadTree_FindNearestPointsReturnsNothingForZeroPointsOrAnEmptyTree()
        {
            Assert.AreEqual(0, LineTree(4).FindNearestPoints(new Vector2(0, 0), 0).Count);
            Assert.AreEqual(0, new QuadTree<int>(new Rectangle(-10, 10, -10, 10)).FindNearestPoints(new Vector2(0, 0), 3).Count);
            Assert.ThrowsException<ArgumentException>(() => LineTree(4).FindNearestPoints(new Vector2(0, 0), -1));
        }

        [TestMethod]
        public void QuadTree_FindNearestPointsKeepsEquidistantPointsTogether()
        {
            QuadTree<int> tree = LineTree(6);

            // Points 2 and 4 are both 1 away from x=3, and 3 itself is 0 away.
            List<DistanceToPoint<int>> found = tree.FindNearestPoints(new Vector2(3, 0), 2);

            Assert.AreEqual(3, found[0].Value);
            Assert.AreEqual(0.0, found[0].Distance);
            Assert.AreEqual(3, found.Count, "Both tied neighbours are returned even though two were requested");
            CollectionAssert.AreEquivalent(new[] { 2, 4 }, found.Skip(1).Select(f => f.Value).ToArray());
            Assert.IsTrue(found.Skip(1).All(f => f.Distance == 1.0));
        }

        [TestMethod]
        public void QuadTree_FindNearestPointsMatchesBruteForceDistances()
        {
            Random random = new(2024);
            for (int trial = 0; trial < 50; trial++)
            {
                QuadTree<int> tree = new(new Rectangle(-1000, 1000, -1000, 1000));
                List<Vector2> points = [];
                int n = random.Next(1, 60);
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = new(random.NextDouble() * 1800 - 900, random.NextDouble() * 1800 - 900);
                    points.Add(p);
                    tree.Add(p, i);
                }

                Vector2 query = new(random.NextDouble() * 1800 - 900, random.NextDouble() * 1800 - 900);
                int nPoints = random.Next(1, 8);

                List<DistanceToPoint<int>> found = tree.FindNearestPoints(query, nPoints);
                double[] expected = points.Select(p => Vector2.Distance(p, query)).OrderBy(d => d).Take(Math.Min(nPoints, n)).ToArray();

                CollectionAssert.AreEqual(expected, found.Take(expected.Length).Select(f => f.Distance).ToArray(), "Trial " + trial);
            }
        }

        #endregion
    }
}
