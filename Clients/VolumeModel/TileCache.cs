using System;
using System.Threading.Tasks;
using Viking.Common;

namespace Viking.VolumeModel
{
    public class TileCacheEntry : CacheEntry<string>
    {
        public readonly Tile Tile;

        public TileCacheEntry(string Key, Tile T) : base(Key)
        {
            Tile = T;
            LastAccessed = DateTime.UtcNow;
            Size = T == null ? 1 : T.Size;
        }

        public override sealed void Dispose()
        {
            return;
        }
    }

    /// <summary>
    /// This object manages construction of tile objects. 
    /// It first checks a cache for a tile matching the request.  If not found it creates a new tile object.
    /// </summary>
    public class TileCache : TimeQueueCache<string, TileCacheEntry, Tile, Tile>
    {
        public TileCache()
        {
            this.MaxCacheSize = 1 << 21;
        }

        static protected string TileKey(string textureFileName, string TransformName)
        {
            return textureFileName + " " + TransformName;
        }

        protected override Tile Fetch(TileCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.Tile;
        }

        public Tile ConstructTile(string TileUniqueKey,
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
            //Check to see if this tile is already loaded
            string key = TileUniqueKey;
            Tile tile;

            if (verticies.Length < 3)
            {
                //Not enough verticies for a tile.  Return null
                tile = null;
            }
            else
            {
                tile = new Tile(TileUniqueKey,
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
            Add(key, tile);

            return tile;
        }

        protected override TileCacheEntry CreateEntry(string key, Tile value)
        {
            TileCacheEntry entry = new TileCacheEntry(key, value);
            return entry;
        }

        protected override Task<TileCacheEntry> CreateEntryAsync(string key, Tile value)
        {
            return Task.FromResult(CreateEntry(key, value));
        }

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
        protected override void OnCheckpointFailed(TileCacheEntry entry)
        {
            // Trace.WriteLine("OnCheckpointFailed for transform: " + entry.Key);
            base.OnCheckpointFailed(entry);
            //
            //RemoveEntry(entry);
        }

    }
}
