using System;
using System.Threading.Tasks;
using Viking.Common;
using Viking.VolumeModel;

namespace Viking.VolumeViewModel
{
    class TileViewModelCacheEntry : CacheEntry<TileUniqueKey>
    {
        public readonly TileViewModel Tile;

        public TileViewModelCacheEntry(TileUniqueKey key, TileViewModel t)
            : base(key)
        {
            this.Tile = t;
        }

        public override void Dispose()
        {

        }

    }

    class TileViewModelCache : TimeQueueCache<TileUniqueKey, TileViewModelCacheEntry, TileViewModel, TileViewModel>
    {
        protected override TileViewModel Fetch(TileViewModelCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.Tile;
        }

        protected override TileViewModelCacheEntry CreateEntry(TileUniqueKey key, TileViewModel value)
        {
            TileViewModelCacheEntry cacheEntry = new TileViewModelCacheEntry(key, value);
            return cacheEntry;
        }

        protected override TileViewModelCacheEntry CreateEntry(TileUniqueKey key, Func<TileUniqueKey, TileViewModel> valueFactory)
        {
            TileViewModel value = valueFactory(key);
            TileViewModelCacheEntry cacheEntry = new TileViewModelCacheEntry(key, value);
            return cacheEntry;
        }

        protected override Task<TileViewModelCacheEntry> CreateEntryAsync(TileUniqueKey key, TileViewModel value)
        {
            TileViewModelCacheEntry cacheEntry = new TileViewModelCacheEntry(key, value);
            return Task.FromResult(cacheEntry);
        }
    }
}
