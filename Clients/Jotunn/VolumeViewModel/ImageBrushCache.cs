using System.Windows.Media;

namespace Viking.VolumeViewModel
{
    class BrushCacheEntry : Viking.Common.CacheEntry<string>
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

    class ImageBrushCache : Viking.Common.TimeQueueCache<string, BrushCacheEntry, Brush, Brush>
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
