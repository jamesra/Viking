using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace WebAnnotationTests
{
    /// <summary>
    /// Pins the equality, hash and ordering contract of <see cref="StructureLinkKey"/> and
    /// <see cref="PermittedStructureLinkKey"/>. A bidirectional link (A,B) is the same link as (B,A),
    /// so the two keys must be equal, hash alike, and compare as 0; the stores use the keys in
    /// <c>ConcurrentDictionary</c> and the section view tracks them in a <c>SortedSet</c>.
    /// </summary>
    [TestClass]
    public class StructureLinkKeyTests
    {
        /// <summary>IDs small enough to enumerate every pair, including self links.</summary>
        private static readonly long[] Ids = [1, 2, 3, 40];

        private static IEnumerable<StructureLinkKey> AllLinkKeys() =>
            from s in Ids from t in Ids from b in new[] { false, true } select new StructureLinkKey(s, t, b);

        private static IEnumerable<PermittedStructureLinkKey> AllPermittedKeys() =>
            from s in Ids from t in Ids from b in new[] { false, true } select new PermittedStructureLinkKey(s, t, b);

        /// <summary>Two links are the same when the endpoints match, or when both are bidirectional and the endpoints are swapped.</summary>
        private static bool SameLink(long s1, long t1, bool b1, long s2, long t2, bool b2) =>
            b1 == b2 && ((s1 == s2 && t1 == t2) || (b1 && s1 == t2 && t1 == s2));

        [TestMethod]
        public void StructureLinkKey_EqualityHashAndOrderAgree_ForEveryPair()
        {
            StructureLinkKey[] keys = [.. AllLinkKeys()];
            foreach (var a in keys)
            {
                foreach (var b in keys)
                {
                    bool expected = SameLink(a.SourceID, a.TargetID, a.Bidirectional, b.SourceID, b.TargetID, b.Bidirectional);
                    string pair = $"{a.SourceID},{a.TargetID},{a.Bidirectional} vs {b.SourceID},{b.TargetID},{b.Bidirectional}";
                    Assert.AreEqual(expected, a.Equals(b), "Equals " + pair);
                    Assert.AreEqual(expected, a.Equals((object)b), "Equals(object) " + pair);
                    Assert.AreEqual(expected, a == b, "== " + pair);
                    Assert.AreEqual(!expected, a != b, "!= " + pair);
                    Assert.AreEqual(expected, a.CompareTo(b) == 0, "CompareTo == 0 " + pair);
                    Assert.AreEqual(Math.Sign(a.CompareTo(b)), -Math.Sign(b.CompareTo(a)), "antisymmetric " + pair);
                    if (expected)
                        Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "hash " + pair);
                }
            }

            AssertTransitive(keys);
        }

        [TestMethod]
        public void PermittedStructureLinkKey_EqualityHashAndOrderAgree_ForEveryPair()
        {
            PermittedStructureLinkKey[] keys = [.. AllPermittedKeys()];
            foreach (var a in keys)
            {
                foreach (var b in keys)
                {
                    bool expected = SameLink(a.SourceTypeID, a.TargetTypeID, a.Bidirectional, b.SourceTypeID, b.TargetTypeID, b.Bidirectional);
                    string pair = $"{a.SourceTypeID},{a.TargetTypeID},{a.Bidirectional} vs {b.SourceTypeID},{b.TargetTypeID},{b.Bidirectional}";
                    Assert.AreEqual(expected, a.Equals(b), "Equals " + pair);
                    Assert.AreEqual(expected, a == b, "== " + pair);
                    Assert.AreEqual(!expected, a != b, "!= " + pair);
                    Assert.AreEqual(expected, a.CompareTo(b) == 0, "CompareTo == 0 " + pair);
                    Assert.AreEqual(Math.Sign(a.CompareTo(b)), -Math.Sign(b.CompareTo(a)), "antisymmetric " + pair);
                    if (expected)
                        Assert.AreEqual(a.GetHashCode(), b.GetHashCode(), "hash " + pair);
                }
            }

            AssertTransitive(keys);
        }

        private static void AssertTransitive<T>(T[] keys) where T : IComparable<T>
        {
            foreach (var a in keys)
                foreach (var b in keys)
                    foreach (var c in keys)
                        if (a.CompareTo(b) < 0 && b.CompareTo(c) < 0)
                            Assert.IsTrue(a.CompareTo(c) < 0, $"transitive {a} < {b} < {c}");
        }

        /// <summary>
        /// Regression: the pen tool builds its candidate from the structure the user started on, so a
        /// bidirectional link the server stored the other way round must still be found in the store's dictionary.
        /// </summary>
        [TestMethod]
        public void StructureLinkKey_ReversedBidirectionalKey_FindsStoredLink()
        {
            var stored = new Dictionary<StructureLinkKey, string> { [new StructureLinkKey(900, 12, true)] = "link" };
            Assert.IsTrue(stored.ContainsKey(new StructureLinkKey(12, 900, true)));
            Assert.IsFalse(stored.ContainsKey(new StructureLinkKey(12, 900, false)), "A directional key is a different link.");
        }

        /// <summary>
        /// Regression: two bidirectional links sharing their lower structure ID are different links, so the
        /// section view's sorted key tracker must keep both (CompareTo used to compare the second high ID with itself).
        /// </summary>
        [TestMethod]
        public void StructureLinkKey_BidirectionalLinksSharingLowId_AreBothKeptInSortedSet()
        {
            var set = new SortedSet<StructureLinkKey> { new(5, 10, true), new(5, 20, true), new(20, 5, true) };
            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(new StructureLinkKey(5, 10, true).CompareTo(new StructureLinkKey(5, 20, true)) < 0);
            Assert.IsTrue(new StructureLinkKey(1, 9, false).CompareTo(new StructureLinkKey(2, 3, false)) < 0, "Directional keys still sort by source, then target.");
        }

        [TestMethod]
        public void PermittedStructureLinkKey_BidirectionalLinksSharingLowId_AreBothKeptInSortedSet()
        {
            var set = new SortedSet<PermittedStructureLinkKey> { new(5, 10, true), new(5, 20, true), new(20, 5, true) };
            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(new PermittedStructureLinkKey(5, 10, true).CompareTo(new PermittedStructureLinkKey(5, 20, true)) < 0);
        }

        /// <summary>Link objects whose IDs are equal must hash alike, whichever way round the server sent the endpoints.</summary>
        [TestMethod]
        public void LinkObjects_WithReversedBidirectionalEndpoints_AreEqualAndHashAlike()
        {
            var a = new StructureLinkObj(new AnnotationService.Types.StructureLink { SourceID = 7, TargetID = 3, Bidirectional = true });
            var b = new StructureLinkObj(new AnnotationService.Types.StructureLink { SourceID = 3, TargetID = 7, Bidirectional = true });
            Assert.IsTrue(a.Equals(b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

            var p = new PermittedStructureLinkObj(new AnnotationService.Types.PermittedStructureLink { SourceTypeID = 7, TargetTypeID = 3, Bidirectional = true });
            var q = new PermittedStructureLinkObj(new AnnotationService.Types.PermittedStructureLink { SourceTypeID = 3, TargetTypeID = 7, Bidirectional = true });
            Assert.IsTrue(p.Equals(q));
            Assert.AreEqual(p.GetHashCode(), q.GetHashCode());
        }
    }
}
