using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Pins <see cref="CacheEntry{KEY}"/>: identity is the key alone (a hash set or dictionary of entries
    /// must not care about LastAccessed, Size or the checkpoint flags), while ordering is LastAccessed alone
    /// (<c>TimeQueueCache.Trim</c> sorts entries through <see cref="IComparable"/> to evict least recently used first).
    /// TileViewModelCache, TileCache and SectionAnnotationsViewModelCache entries all inherit this behavior.
    /// </summary>
    [TestClass]
    public class CacheEntryTests
    {
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Minimal concrete entry; Dispose is a no-op because the base contract is abstract.</summary>
        private sealed class TestEntry : CacheEntry<string>
        {
            public TestEntry(string key) : base(key) { }
            public TestEntry(string key, DateTime lastAccessed, long size) : base(key, lastAccessed, size) { }
            public override void Dispose() { }
        }

        /// <summary>A second subclass, to show equality ignores the runtime type.</summary>
        private sealed class OtherEntry : CacheEntry<string>
        {
            public OtherEntry(string key) : base(key) { }
            public override void Dispose() { }
        }

        private sealed class IntEntry : CacheEntry<int>
        {
            public IntEntry(int key) : base(key) { }
            public override void Dispose() { }
        }

        private static readonly Arbitrary<string> Keys = Arb.Default.NonEmptyString().Generator
            .Select(s => s.Get).ToArbitrary();

        private static readonly Arbitrary<DateTime> Times = Gen.Choose(0, 2_000_000)
            .Select(minutes => Epoch.AddMinutes(minutes)).ToArbitrary();

        /// <summary>FsCheck 2.x ForAll takes at most four arbitraries, so each entry is generated whole.</summary>
        private static readonly Arbitrary<TestEntry> AnyEntry =
            (from key in Keys.Generator
             from time in Times.Generator
             from size in Arb.Default.Int64().Generator
             from used in Arb.Default.Bool().Generator
             from exempt in Arb.Default.Bool().Generator
             select new TestEntry(key, time, size)
             {
                 WasUsedSinceLastCheckpoint = used,
                 CheckpointExempt = exempt
             }).ToArbitrary();

        private static readonly Arbitrary<TestEntry> AnyEntryWithKeyAndTime =
            (from key in Keys.Generator
             from time in Times.Generator
             select new TestEntry(key, time, 1)).ToArbitrary();

        private static int Sign(int value) => Math.Sign(value);

        [TestMethod]
        public void Constructor_KeyOnly_DefaultsSizeOneFlagsAndRecentAccessTime()
        {
            DateTime before = DateTime.UtcNow;
            var entry = new TestEntry("k");
            DateTime after = DateTime.UtcNow;

            Assert.AreEqual("k", entry.Key);
            Assert.AreEqual(1L, entry.Size);
            Assert.IsTrue(entry.WasUsedSinceLastCheckpoint, "new entries start as used, so the first checkpoint spares them");
            Assert.IsFalse(entry.CheckpointExempt);
            Assert.IsTrue(entry.LastAccessed >= before && entry.LastAccessed <= after);
            Assert.AreEqual(DateTimeKind.Utc, entry.LastAccessed.Kind);
        }

        [TestMethod]
        public void Constructor_WithAccessTimeAndSize_KeepsThemAndDefaultsFlags()
        {
            DateTime when = Epoch.AddDays(5);
            var entry = new TestEntry("k", when, 4096);

            Assert.AreEqual(when, entry.LastAccessed);
            Assert.AreEqual(4096L, entry.Size);
            Assert.IsTrue(entry.WasUsedSinceLastCheckpoint);
            Assert.IsFalse(entry.CheckpointExempt);
        }

        [TestMethod]
        public void Equals_Property_DependsOnKeyOnly()
        {
            Prop.ForAll(AnyEntry, AnyEntry, (a, b) =>
            {
                bool expected = a.Key == b.Key;
                return a.Equals((object)b) == expected
                    && ((IEquatable<CacheEntry<string>>)a).Equals(b) == expected;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void GetHashCode_Property_IsKeyHashAndIgnoresOtherFields()
        {
            Prop.ForAll(AnyEntry, Times, Arb.Default.Int64(), (entry, newTime, newSize) =>
            {
                int first = entry.GetHashCode();
                entry.LastAccessed = newTime;
                entry.Size = newSize;
                entry.WasUsedSinceLastCheckpoint = !entry.WasUsedSinceLastCheckpoint;
                entry.CheckpointExempt = !entry.CheckpointExempt;
                return first == entry.Key.GetHashCode() && entry.GetHashCode() == first;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void Equals_Object_NullAndForeignTypesAreNotEqual()
        {
            var entry = new TestEntry("k");

            Assert.IsFalse(entry.Equals((object)null));
            Assert.IsFalse(entry.Equals("k"), "a bare key is not equal to its entry");
            Assert.IsFalse(entry.Equals(new IntEntry(1)), "entries of a different KEY type are never equal");
            Assert.IsFalse(((IEquatable<CacheEntry<string>>)entry).Equals(null));
        }

        [TestMethod]
        public void Equals_DifferentSubclassSameKey_IsEqual()
        {
            var a = new TestEntry("tile-1");
            var b = new OtherEntry("tile-1");

            Assert.IsTrue(a.Equals((object)b));
            Assert.IsTrue(b.Equals((object)a));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void HashSet_EntriesWithSameKeyCollapse_RegardlessOfAccessTime()
        {
            var set = new HashSet<CacheEntry<string>>
            {
                new TestEntry("a", Epoch, 1),
                new TestEntry("a", Epoch.AddDays(1), 99),
                new TestEntry("b", Epoch, 1)
            };

            Assert.AreEqual(2, set.Count);
            Assert.IsTrue(set.Contains(new TestEntry("a", Epoch.AddYears(1), 7)));
        }

        [TestMethod]
        public void Compare_Property_SignMatchesLastAccessedAndIgnoresKey()
        {
            Prop.ForAll(AnyEntryWithKeyAndTime, AnyEntryWithKeyAndTime, (x, y) =>
            {
                IComparer<CacheEntry<string>> comparer = x;
                return Sign(comparer.Compare(x, y)) == Sign(x.LastAccessed.CompareTo(y.LastAccessed))
                    && Sign(comparer.Compare(y, x)) == -Sign(comparer.Compare(x, y));
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void CompareTo_Property_SignMatchesLastAccessed()
        {
            Prop.ForAll(AnyEntryWithKeyAndTime, AnyEntryWithKeyAndTime, (x, y) =>
                Sign(((IComparable)x).CompareTo(y)) == Sign(x.LastAccessed.CompareTo(y.LastAccessed)))
                .QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void Sort_Property_OrdersLeastRecentlyAccessedFirst()
        {
            var timeLists = Gen.ListOf(Times.Generator).Select(times => times.ToArray()).ToArbitrary();
            Prop.ForAll(timeLists, times =>
            {
                var entries = times.Select((t, i) => new TestEntry("key" + i, t, 1)).ToList();
                entries.Sort();
                for (int i = 1; i < entries.Count; i++)
                {
                    if (entries[i - 1].LastAccessed > entries[i].LastAccessed)
                        return false;
                }
                return entries.Count == times.Length;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void CompareTo_SameAccessTimeDifferentKeys_IsZeroEvenThoughNotEqual()
        {
            var a = new TestEntry("a", Epoch, 1);
            var b = new TestEntry("b", Epoch, 1);

            Assert.AreEqual(0, ((IComparable)a).CompareTo(b));
            Assert.IsFalse(a.Equals((object)b), "ordering ties are not equality");
        }

        [TestMethod]
        public void CompareTo_NullAndNonEntry_ReturnsZero()
        {
            IComparable entry = new TestEntry("k");

            Assert.AreEqual(0, entry.CompareTo(null));
            Assert.AreEqual(0, entry.CompareTo("k"));
            Assert.AreEqual(0, entry.CompareTo(new IntEntry(1)), "an entry of another KEY type is not comparable, so it ties");
        }

        [TestMethod]
        public void CompareTo_OlderEntrySortsBeforeNewer()
        {
            IComparable older = new TestEntry("a", Epoch, 1);
            var newer = new TestEntry("b", Epoch.AddSeconds(1), 1);

            Assert.IsTrue(older.CompareTo(newer) < 0);
            Assert.IsTrue(((IComparable)newer).CompareTo(older) > 0);
        }
    }
}
