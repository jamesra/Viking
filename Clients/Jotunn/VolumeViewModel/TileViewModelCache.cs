using System;
using System.Threading.Tasks;
using Viking.Common;

namespace Viking.VolumeViewModel
{
    class TileViewModelCacheEntry : CacheEntry<string>
    {
        public readonly TileViewModel Tile;

        public TileViewModelCacheEntry(string key, TileViewModel t)
            : base(key)
        {
            this.Tile = t;
        }

        public override void Dispose()
        {

        }

    }

    class TileViewModelCache : TimeQueueCache<string, TileViewModelCacheEntry, TileViewModel, TileViewModel>
    {
        protected override TileViewModel Fetch(TileViewModelCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.Tile;
        }

        protected override TileViewModelCacheEntry CreateEntry(string key, TileViewModel value)
        {
            TileViewModelCacheEntry cacheEntry = new TileViewModelCacheEntry(key, value);
            return cacheEntry;
        }

        protected override TileViewModelCacheEntry CreateEntry(string key, Func<string, TileViewModel> valueFactory)
        {
            TileViewModel value = valueFactory(key);
            TileViewModelCacheEntry cacheEntry = new TileViewModelCacheEntry(key, value);
            return cacheEntry;
        }

        protected override Task<TileViewModelCacheEntry> CreateEntryAsync(string key, TileViewModel value)
        {
            TileViewModelCacheEntry cacheEntry = new TileViewModelCacheEntry(key, value);
            return Task.FromResult(cacheEntry);
        }
    }
}
