using System;
using System.Threading.Tasks;
using Viking.Common;

namespace Viking.VolumeModel
{
    public class TileCacheEntry : CacheEntry<TileUniqueKey>
    {
        public readonly TileViewModel TileViewModel;

        public TileCacheEntry(TileUniqueKey Key, TileViewModel T) : base(Key)
        {
            TileViewModel = T;
            LastAccessed = DateTime.UtcNow;
            Size = T is null ? 1 : T.Size;
        }

        public sealed override void Dispose()
        {
            return;
        }
    }

    /// <summary>
    /// This object manages construction of tile objects. 
    /// It first checks a cache for a tile matching the request.  If not found it creates a new tile object.
    /// </summary>
    public class TileCache : TimeQueueCache<TileUniqueKey, TileCacheEntry, TileViewModel, TileViewModel>
    {
        public TileCache()
        {
            this.MaxCacheSize = 1 << 21;
        }

        protected override TileViewModel Fetch(TileCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.TileViewModel;
        }

        public TileViewModel ConstructTile(TileUniqueKey key,
                                PositionNormalTextureVertex[] verticies,
                                int[] TriangleIndicies,
                                string textureFullPath,
                                string cacheFilePath,
                                /*PORT: ViewModel should handle cache names*/ //   string cachedTextureFileName,
                                string TransformName,
                                int downsample,
                                int MipMapLevels //Should be one, unless it is the minimum downsample level
            )
        {
            TileViewModel tileViewModel;

            if (verticies.Length < 3)
            {
                //Not enough verticies for a tile.  Return null
                tileViewModel = null;
            }
            else
            {
                tileViewModel = new TileViewModel(key,
                    verticies,
                    TriangleIndicies,
                    textureFullPath,
                    cacheFilePath,
                    //PORT cachedTextureFileName,
                    downsample
                    //PORT: MipMapLevels
                    );
            }

            //We can add a null tile to the cache to indicate it has been calculated and we do not have valid data for it.
            Add(key, tileViewModel);

            return tileViewModel;
        }

        protected override TileCacheEntry CreateEntry(TileUniqueKey key, TileViewModel value)
        {
            TileCacheEntry entry = new(key, value);
            return entry;
        }

        protected override TileCacheEntry CreateEntry(TileUniqueKey key, Func<TileUniqueKey, TileViewModel> valueFactory) => new TileCacheEntry(key, valueFactory(key));

        protected override Task<TileCacheEntry> CreateEntryAsync(TileUniqueKey key, TileViewModel value) => Task.FromResult(CreateEntry(key, value));

        /*
        /// <summary>
        /// Remove tile from cache if it exists
        /// </summary>
        /// <param name="tile"></param>
        protected override bool OnRemoveEntry(TileCacheEntry entry)
        {
            return true;                    
        }
        */

        /// <summary>
        /// Aborts all requests for tiles not on the provided list.
        /// Only one of these methods should be running at a time or much of the cache could be deleted
        /// </summary>
        /// <param name="SafeTiles"></param>
        protected override void OnCheckpointFailed(TileCacheEntry entry) =>
            // Trace.WriteLine("OnCheckpointFailed for transform: " + entry.Key);
            base.OnCheckpointFailed(entry);////RemoveEntry(entry);

    }
}
