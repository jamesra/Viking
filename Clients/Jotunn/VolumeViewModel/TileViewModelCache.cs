using System;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
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
            return CreateEntry(key, valueFactory(key));
        }

        protected override Task<TileViewModelCacheEntry> CreateEntryAsync(string key, TileViewModel value)
        {
            var result = CreateEntry(key, value);
            return Task.FromResult(result);
        }
    }
}
