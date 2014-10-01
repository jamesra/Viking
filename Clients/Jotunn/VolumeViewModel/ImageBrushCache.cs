using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Common;
using System.Windows.Media;

namespace Viking.VolumeViewModel
{
    class BrushCacheEntry : Common.DataStructures.CacheEntry<string>
    {
        public Brush Brush;

        public BrushCacheEntry(string key, Brush ib)
            : base(key)
        {
            this.Brush = ib; 
        }

        public override void Dispose()
        {
            Brush = null; 
        }
    }

    class ImageBrushCache : Common.DataStructures.TimeQueueCache<string, BrushCacheEntry, Brush, Brush>
    {

        protected override Brush Fetch(BrushCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.Brush;
        }

        protected override BrushCacheEntry CreateEntry(string key, Brush value)
        {
            BrushCacheEntry cacheEntry = new BrushCacheEntry(key, value);
            return cacheEntry; 
        }
    }
}
