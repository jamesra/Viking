using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viking.Common;

namespace CommonTests
{
    /// <summary>
    /// Characterizes <see cref="TimeQueueCache{KEY,CACHEENTRY,ADDTYPE,FETCHTYPE}"/> size accounting,
    /// access marking, checkpoint eviction, and footprint reduction. Does not change production eviction
    /// semantics (see ledger decision uncheckpointed-cache-eviction).
    /// </summary>
    [TestClass]
    public class TimeQueueCacheTests
    {
        private static readonly DateTime Epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Reference-type payload returned from the cache.</summary>
        private sealed class Payload
        {
            public long Value { get; }
            public Payload(long value) => Value = value;
        }

        private sealed class TestEntry : CacheEntry<string>
        {
            public bool Disposed { get; private set; }

            public TestEntry(string key, long size, DateTime lastAccessed)
                : base(key, lastAccessed, size)
            {
            }

            public override void Dispose() => Disposed = true;
        }

        /// <summary>Minimal concrete cache for string keys and sized entries.</summary>
        private sealed class TestCache : TimeQueueCache<string, TestEntry, Payload, Payload>
        {
            public readonly List<string> RemovedKeys = new List<string>();

            protected override Payload Fetch(TestEntry entry) => new Payload(entry.Size);

            protected override TestEntry CreateEntry(string key, Payload value) =>
                new TestEntry(key, value.Value, Epoch);

            protected override TestEntry CreateEntry(string key, Func<string, Payload> valueFactory) =>
                CreateEntry(key, valueFactory(key));

            protected override Task<TestEntry> CreateEntryAsync(string key, Payload value) =>
                Task.FromResult(CreateEntry(key, value));

            protected override bool OnRemoveEntry(TestEntry entry)
            {
                RemovedKeys.Add(entry.Key);
                return true;
            }

            public long Recount() => RecountCacheSize();

            public void SeedEntry(TestEntry entry) => AddEntry(entry);
        }

        private static Payload P(long size) => new Payload(size);

        private static void WaitUntilUnderMax(TestCache cache, int timeoutMs = 8000)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (cache.CachedSize <= cache.MaxCacheSize)
                    return;
                Thread.Sleep(25);
            }

            Assert.Fail(
                $"ReduceCacheFootprint did not finish within {timeoutMs} ms; CachedSize={cache.CachedSize}, Max={cache.MaxCacheSize}");
        }

        [TestMethod]
        public void Add_IncreasesCachedSizeByEntrySize()
        {
            var cache = new TestCache();
            Assert.IsTrue(cache.Add("a", P(40)));
            Assert.AreEqual(40L, cache.CachedSize);
        }

        [TestMethod]
        public void Remove_DecreasesCachedSizeAndDisposesEntry()
        {
            var cache = new TestCache();
            cache.Add("a", P(30));
            Assert.IsTrue(cache.Remove("a"));
            Assert.AreEqual(0L, cache.CachedSize);
            Assert.AreEqual(1, cache.RemovedKeys.Count);
        }

        [TestMethod]
        public void GetOrAdd_NewKey_AddsOnceToCachedSize()
        {
            var cache = new TestCache();
            cache.GetOrAdd("k", P(25));
            Assert.AreEqual(25L, cache.CachedSize);
            Assert.IsTrue(cache.ContainsKey("k"));
        }

        [TestMethod]
        public void GetOrAdd_ExistingKey_DoesNotDoubleCountSize()
        {
            var cache = new TestCache();
            cache.GetOrAdd("k", P(25));
            cache.GetOrAdd("k", P(99));
            Assert.AreEqual(25L, cache.CachedSize);
        }

        [TestMethod]
        public void RecountCacheSize_ReflectsMutatedEntrySize()
        {
            var cache = new TestCache();
            var entry = new TestEntry("x", 10, Epoch);
            cache.SeedEntry(entry);
            Assert.AreEqual(10L, cache.CachedSize);

            entry.Size = 50;
            Assert.AreEqual(10L, cache.CachedSize, "running total does not track in-place size changes");

            Assert.AreEqual(50L, cache.Recount());
            Assert.AreEqual(50L, cache.CachedSize);
        }

        [TestMethod]
        public void Fetch_MarksUsedAndUpdatesLastAccessed()
        {
            var cache = new TestCache();
            var entry = new TestEntry("a", 1, Epoch.AddHours(1))
            {
                WasUsedSinceLastCheckpoint = false
            };
            cache.SeedEntry(entry);
            DateTime before = DateTime.UtcNow;

            Payload payload = cache.Fetch("a");

            Assert.IsNotNull(payload);
            Assert.IsTrue(entry.WasUsedSinceLastCheckpoint);
            Assert.IsTrue(entry.LastAccessed >= before);
        }

        [TestMethod]
        public void TryGetValue_MarksUsedAndReturnsTrueWhenPresent()
        {
            var cache = new TestCache();
            var entry = new TestEntry("a", 1, Epoch)
            {
                WasUsedSinceLastCheckpoint = false
            };
            cache.SeedEntry(entry);

            Assert.IsTrue(cache.TryGetValue("a", out Payload output));
            Assert.IsNotNull(output);
            Assert.IsTrue(entry.WasUsedSinceLastCheckpoint);
        }

        [TestMethod]
        public void GetOrAdd_ExistingKey_DoesNotMarkWasUsedSinceLastCheckpoint()
        {
            var cache = new TestCache();
            var entry = new TestEntry("a", 1, Epoch) { WasUsedSinceLastCheckpoint = false };
            cache.SeedEntry(entry);

            cache.GetOrAdd("a", P(1));

            Assert.IsFalse(entry.WasUsedSinceLastCheckpoint,
                "GetOrAdd hits protected Fetch only; public Fetch(key) marks access");
        }

        [TestMethod]
        public void Checkpoint_RemovesColdEntriesAndSparesCheckpointExempt()
        {
            var cache = new TestCache();
            var cold = new TestEntry("cold", 1, Epoch);
            var exempt = new TestEntry("exempt", 1, Epoch) { CheckpointExempt = true };
            cache.SeedEntry(cold);
            cache.SeedEntry(exempt);

            cache.Checkpoint();
            cold.WasUsedSinceLastCheckpoint = false;
            exempt.WasUsedSinceLastCheckpoint = false;

            cache.Checkpoint();

            Assert.IsFalse(cache.ContainsKey("cold"));
            Assert.IsTrue(cache.ContainsKey("exempt"));
            Assert.IsTrue(cold.Disposed);
        }

        [TestMethod]
        public void Checkpoint_UsedSinceLastCheckpoint_ClearsFlagWithoutRemoving()
        {
            var cache = new TestCache();
            cache.Add("a", P(1));
            cache.Checkpoint();
            Assert.IsTrue(cache.ContainsKey("a"));
        }

        [TestMethod]
        public void ReduceCacheFootprint_TrimsLeastRecentlyUsedColdEntries()
        {
            var cache = new TestCache { MaxCacheSize = 10 };
            var oldest = new TestEntry("oldest", 5, Epoch) { WasUsedSinceLastCheckpoint = false };
            var middle = new TestEntry("middle", 5, Epoch.AddMinutes(1)) { WasUsedSinceLastCheckpoint = false };
            var newest = new TestEntry("newest", 5, Epoch.AddMinutes(2)) { WasUsedSinceLastCheckpoint = false };
            cache.SeedEntry(oldest);
            cache.SeedEntry(middle);
            cache.SeedEntry(newest);
            Assert.AreEqual(15L, cache.CachedSize);

            cache.ReduceCacheFootprint(null);
            WaitUntilUnderMax(cache);

            Assert.IsFalse(cache.ContainsKey("oldest"), "oldest cold entry should be trimmed first");
            Assert.IsTrue(cache.ContainsKey("middle") || cache.ContainsKey("newest"));
            Assert.IsTrue(cache.CachedSize <= cache.MaxCacheSize);
        }

        [TestMethod]
        public void ReduceCacheFootprint_SkipsEntriesUsedSinceLastCheckpoint()
        {
            var cache = new TestCache { MaxCacheSize = 5 };
            var cold = new TestEntry("cold", 8, Epoch) { WasUsedSinceLastCheckpoint = false };
            var hot = new TestEntry("hot", 8, Epoch.AddMinutes(1)) { WasUsedSinceLastCheckpoint = true };
            cache.SeedEntry(cold);
            cache.SeedEntry(hot);
            Assert.AreEqual(16L, cache.CachedSize);

            cache.ReduceCacheFootprint(null);
            Thread.Sleep(500);

            Assert.IsTrue(cache.ContainsKey("hot"));
            Assert.IsFalse(cache.ContainsKey("cold"));
            Assert.AreEqual(8L, cache.CachedSize,
                "hot entries are never trimmed while WasUsedSinceLastCheckpoint stays true, so size can remain above MaxCacheSize");
        }

        [TestMethod]
        public void CachedSize_Property_MonotonicOnSequentialAddThenRemove()
        {
            var keys = Arb.Default.NonEmptyString().Generator.Select(s => s.Get).ToArbitrary();
            var sizes = Gen.Choose(1, 500).Select(i => (long)i).ToArbitrary();

            Prop.ForAll(keys, sizes, (key, size) =>
            {
                var cache = new TestCache();
                long before = cache.CachedSize;
                if (!cache.Add(key, P(size)))
                    return false;
                if (cache.CachedSize != before + size)
                    return false;
                if (!cache.Remove(key))
                    return false;
                return cache.CachedSize == before;
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void CachedSize_Property_NeverNegativeAfterMixedOperations()
        {
            var counts = Gen.Choose(0, 40).ToArbitrary();
            Prop.ForAll(counts, count =>
            {
                var cache = new TestCache();
                for (int i = 0; i < count; i++)
                {
                    cache.Add("k" + i, P(3));
                    if (cache.CachedSize < 0)
                        return false;
                }

                for (int i = 0; i < count; i++)
                {
                    cache.Remove("k" + i);
                    if (cache.CachedSize < 0)
                        return false;
                }

                return cache.CachedSize >= 0;
            }).QuickCheckThrowOnFailure();
        }
    }
}
