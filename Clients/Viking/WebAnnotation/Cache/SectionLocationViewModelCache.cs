using System;
using System.Threading.Tasks;
using Viking.Common;
using WebAnnotation.ViewModel;

namespace WebAnnotation
{
    internal class SectionAnnotationsViewCacheEntry : CacheEntry<int>
    {
        public readonly SectionAnnotationsView SLVModel = null;

        public SectionAnnotationsViewCacheEntry(int key, SectionAnnotationsView model) : base(key)
        {
            SLVModel = model;
            Size = 1;
        }

        public override void Dispose()
        {
        }
    }

    internal class SectionAnnotationsViewModelCache : TimeQueueCache<int, SectionAnnotationsViewCacheEntry, SectionAnnotationsView, SectionAnnotationsView>
    {
        protected override SectionAnnotationsView Fetch(SectionAnnotationsViewCacheEntry key)
        {
            bool found = dictEntries.TryGetValue(key.SLVModel.SectionNumber, out SectionAnnotationsViewCacheEntry entry);
            if (found)
            {
                key.WasUsedSinceLastCheckpoint = true;

                entry.LastAccessed = DateTime.UtcNow;
                return entry.SLVModel;
            }

            return null;
        }



        protected override SectionAnnotationsViewCacheEntry CreateEntry(int key, SectionAnnotationsView value)
        {
            return new SectionAnnotationsViewCacheEntry(key, value);
        }

        protected override SectionAnnotationsViewCacheEntry CreateEntry(int key, Func<int, SectionAnnotationsView> valueFactory)
        {
            return new SectionAnnotationsViewCacheEntry(key, valueFactory(key));
        }

        protected override Task<SectionAnnotationsViewCacheEntry> CreateEntryAsync(int key, SectionAnnotationsView value)
        {
            return Task.FromResult(CreateEntry(key, value));
        }

        public bool RemoveEntry(int key)
        {
            return Remove(key);
        }

        /// <summary>
        /// Remove all cached entries
        /// </summary>
        public void Clear()
        {
            foreach (int s in dictEntries.Keys)
            {
                Remove(s);
            }
        }
    }
}
