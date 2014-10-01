using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common.DataStructures;
using WebAnnotation.ViewModel;

namespace WebAnnotation
{
    class SectionLocationViewModelCacheEntry : CacheEntry<int>
    {
        public readonly SectionLocationsViewModel SLVModel = null;

        public SectionLocationViewModelCacheEntry(int key, SectionLocationsViewModel model) : base(key)
        {
            this.SLVModel = model;
            this.Size = 1; 
        }

        public override void Dispose()
        {
        }
    }

    class SectionLocationViewModelCache : TimeQueueCache<int, SectionLocationViewModelCacheEntry, SectionLocationsViewModel, SectionLocationsViewModel>
    {
        protected override SectionLocationsViewModel Fetch(SectionLocationViewModelCacheEntry key)
        {
            SectionLocationViewModelCacheEntry entry = null;
            bool found = dictEntries.TryGetValue(key.Key, out entry);
            if (found)
            {
                key.WasUsedSinceLastCheckpoint = true;

                entry.LastAccessed = DateTime.UtcNow;
                return entry.SLVModel; 
            }

            return null;
        }

        

        protected override SectionLocationViewModelCacheEntry CreateEntry(int key, SectionLocationsViewModel value)
        {
            return new SectionLocationViewModelCacheEntry(key, value); 
        }
    }
}
