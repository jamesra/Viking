using System;
using System.Threading.Tasks;
using Viking.Common;
using WebAnnotation.ViewModel;

namespace WebAnnotation
{

    class SectionAnnotationsViewCacheEntry : CacheEntry<int>
    {
        public readonly SectionAnnotationsView SLVModel = null;

        public SectionAnnotationsViewCacheEntry(int key, SectionAnnotationsView model) : base(key)
        {
            this.SLVModel = model;
            this.Size = 1;
        }

        public override void Dispose()
        {
        }
    }

    class SectionAnnotationsViewModelCache : TimeQueueCache<int, SectionAnnotationsViewCacheEntry, SectionAnnotationsView, SectionAnnotationsView>
    {
        protected override SectionAnnotationsView Fetch(SectionAnnotationsViewCacheEntry key)
        {
            SectionAnnotationsViewCacheEntry entry = null;
            bool found = dictEntries.TryGetValue(key.SLVModel.SectionNumber, out entry);
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

        protected override Task<SectionAnnotationsViewCacheEntry> CreateEntryAsync(int key, SectionAnnotationsView value)
        {
            return Task.FromResult(CreateEntry(key, value));
        }

        public bool RemoveEntry(int key)
        {
            SectionAnnotationsViewCacheEntry entry;
            return this.Remove(key);
        }

        /// <summary>
        /// Remove all cached entries
        /// </summary>
        public void Clear()
        {
            foreach (var s in this.dictEntries.Keys)
            {
                this.Remove(s);
            }
        }
    }
}
