using FsCheck;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Viking.VolumeModel;

namespace CommonTests
{
    /// <summary>
    /// Pins eviction of <see cref="SectionTransformsCache"/> through <see cref="MappingManager.ReduceCacheFootprint"/>,
    /// which Viking's cache-cleaning timer calls once a minute. The cache keeps
    /// <see cref="SectionTransformsCache.NumSectionsToKeepInMemory"/> sections and unloads the least recently used rest.
    /// </summary>
    [TestClass]
    public class SectionTransformsCacheTests
    {
        private const int SectionLimit = 6;

        /// <summary>
        /// The base cache trims on a background task, so tests poll. A failing run waits this long per case.
        /// </summary>
        private static readonly TimeSpan EvictionTimeout = TimeSpan.FromSeconds(2);

        private static readonly DateTime FirstAccess = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Exposes entries so tests can set distinct access times without sleeping.</summary>
        private sealed class StampableSectionTransformsCache : SectionTransformsCache
        {
            public void SetLastAccessed(int section, DateTime whenUtc) => dictEntries[section].LastAccessed = whenUtc;

            public int[] Sections => [.. dictEntries.Keys.OrderBy(k => k)];

            public SectionMappingsCacheEntry Entry(int section) => dictEntries[section];
        }

        private static (MappingManager Manager, StampableSectionTransformsCache Cache) CreateManager()
        {
            var cache = new StampableSectionTransformsCache();
            var manager = new MappingManager(null) { SectionMappingCache = cache };
            return (manager, cache);
        }

        /// <summary>
        /// Loads one section per element of <paramref name="accessRank"/>, section 100 + i last used at rank accessRank[i].
        /// Uses the Fetch ?? GetOrAdd pattern of <see cref="MappingManager.GetMapping"/>.
        /// </summary>
        private static int[] LoadSections(StampableSectionTransformsCache cache, int[] accessRank)
        {
            int[] sections = [.. Enumerable.Range(0, accessRank.Length).Select(i => 100 + i)];
            for (int i = 0; i < sections.Length; i++)
            {
                _ = cache.Fetch(sections[i]) ?? cache.GetOrAdd(sections[i], new SectionTransformsDictionary());
                cache.SetLastAccessed(sections[i], FirstAccess.AddSeconds(accessRank[i]));
            }

            return sections;
        }

        private static int[] WaitForSectionCountAtMost(StampableSectionTransformsCache cache, int count)
        {
            var timer = Stopwatch.StartNew();
            int[] sections = cache.Sections;
            while (sections.Length > count && timer.Elapsed < EvictionTimeout)
            {
                Thread.Sleep(5);
                sections = cache.Sections;
            }

            return sections;
        }

        private static int[] MostRecentlyUsed(int[] sections, int[] accessRank, int count) =>
            [.. sections.Zip(accessRank, (section, rank) => (section, rank))
                .OrderByDescending(p => p.rank)
                .Take(count)
                .Select(p => p.section)
                .OrderBy(s => s)];

        [TestMethod]
        public void ReduceCacheFootprint_Property_KeepsTheSixMostRecentlyUsedSections()
        {
            var accessOrders =
                from n in Gen.Choose(0, 24)
                from ranks in Gen.Shuffle(Enumerable.Range(0, n).ToArray())
                select ranks;

            Prop.ForAll(accessOrders.ToArbitrary(), accessRank =>
            {
                var (manager, cache) = CreateManager();
                int[] sections = LoadSections(cache, accessRank);

                manager.ReduceCacheFootprint();

                int[] kept = WaitForSectionCountAtMost(cache, SectionLimit);
                return kept.SequenceEqual(MostRecentlyUsed(sections, accessRank, SectionLimit))
                    .Label($"kept [{string.Join(",", kept)}]");
            }).QuickCheckThrowOnFailure();
        }

        [TestMethod]
        public void ReduceCacheFootprint_TenSectionsBrowsedInOrder_UnloadsTheFourOldestAndFreesThem()
        {
            var (manager, cache) = CreateManager();
            int[] sections = LoadSections(cache, [.. Enumerable.Range(0, 10)]);
            SectionMappingsCacheEntry[] entries = [.. sections.Select(cache.Entry)];

            manager.ReduceCacheFootprint();

            CollectionAssert.AreEqual(sections.Skip(4).ToArray(), WaitForSectionCountAtMost(cache, SectionLimit));
            Assert.IsTrue(entries.Take(4).All(e => e.TransformsForSection is null), "evicted sections were not disposed");
            Assert.IsTrue(entries.Skip(4).All(e => e.TransformsForSection is not null));
        }

        [TestMethod]
        public void ReduceCacheFootprint_RepeatedAtOrUnderTheLimit_KeepsIdleSections()
        {
            var (manager, cache) = CreateManager();
            int[] sections = LoadSections(cache, [.. Enumerable.Range(0, SectionLimit)]);

            manager.ReduceCacheFootprint();
            manager.ReduceCacheFootprint();
            manager.ReduceCacheFootprint();

            CollectionAssert.AreEqual(sections, cache.Sections);
        }

        [TestMethod]
        public void ReduceCacheFootprint_SectionRevisitedAfterBrowsing_IsKept()
        {
            var (manager, cache) = CreateManager();
            int[] sections = LoadSections(cache, [.. Enumerable.Range(0, 8)]);
            _ = cache.Fetch(sections[0]);

            manager.ReduceCacheFootprint();

            int[] kept = WaitForSectionCountAtMost(cache, SectionLimit);
            CollectionAssert.AreEqual(new[] { sections[0] }.Concat(sections.Skip(3)).ToArray(), kept);
        }
    }
}
