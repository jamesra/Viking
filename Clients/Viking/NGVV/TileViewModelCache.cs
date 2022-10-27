using System;
using System.Threading.Tasks;
using Viking.Common;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace Viking
{
    internal class TileViewModelCacheEntry : CacheEntry<string>
    {
        public TileView TileView;

        public TileViewModelCacheEntry(string Key, TileView T)
            : base(Key)
        {
            TileView = T;
            LastAccessed = DateTime.Now;
            Size = T.Size;
        }

        public override void Dispose()
        {
        }
    }
    /// <summary>
    /// This object manages construction of tile objects. 
    /// It first checks a cache for a tile matching the request.  If not found it creates a new tile object.
    /// </summary>
    internal class TileViewModelCache : TimeQueueCache<string, TileViewModelCacheEntry, TileView, TileView>
    {

        public TileViewModelCache() : base()
        {
            this.MaxCacheSize = 1 << 28;
        }

        static protected string TileKey(string textureFileName, string TransformName)
        {
            return $"{textureFileName} {TransformName}";
        }

        public TileView GetTile(string textureFileName, string TransformName)
        {
            return this.Fetch(TileKey(textureFileName, TransformName));
        }

        protected override TileView Fetch(TileViewModelCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.TileView;
        }

        public TileView ConstructTile(TileViewModel tileViewModel,
                                string textureFileName,
                                string cachedTextureFileName,
                                string TransformName,
                                int MipMapLevels, //Should be one, unless it is the minimum downsample level
                                int TextureSize)
        {
            //Check to see if this tile is already loaded
            string key = TileKey(textureFileName, TransformName);

            TileView tileView = null;
            bool added = false;
            try
            {
                tileView = new TileView(tileViewModel,
                    textureFileName,
                    cachedTextureFileName,
                    MipMapLevels,
                    TextureSize);

                added = Add(key, tileView);
                if (!added)
                {
                    tileView.Dispose();
                    tileView = null;
                }
            }
            catch (Exception)
            {
                if (tileView != null)
                {
                    tileView.Dispose();
                    tileView = null;
                }
                throw;
            }



            return tileView;
        }

        /// <summary>
        /// Retrieve existing tile if it exists, otherwise create a new one
        /// </summary>
        /// <param name="tileViewModel"></param>
        /// <param name="textureFileName"></param>
        /// <param name="cachedTextureFileName"></param>
        /// <param name="TransformName"></param>
        /// <param name="MipMapLevels"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public TileView FetchOrConstructTile(TileViewModel tileViewModel,
                                string textureFileName,
                                string cachedTextureFileName,
                                string TransformName,
                                int MipMapLevels //Should be one, unless it is the minimum downsample level
                                )
        {
            string key = TileKey(textureFileName, TransformName);

            TileView tileView = Fetch(key);
            if (tileView != null)
                return tileView;

            return ConstructTile(tileViewModel, textureFileName, cachedTextureFileName, TransformName, MipMapLevels, tileViewModel.TextureSize);
        }

        protected override TileViewModelCacheEntry CreateEntry(string key, TileView value)
        {
            TileViewModelCacheEntry entry = new TileViewModelCacheEntry(key, value);
            return entry;
        }

        protected override TileViewModelCacheEntry CreateEntry(string key, Func<string,TileView> valueFactory)
        {
            return new TileViewModelCacheEntry(key, valueFactory(key));
        }

        protected override Task<TileViewModelCacheEntry> CreateEntryAsync(string key, TileView value)
        {
            return Task.FromResult(CreateEntry(key, value));
        }

        /// <summary>
        /// Cleanup the memory allocated for this cache entry. 
        /// RemoveEntry() calls this function
        /// </summary>
        /// <param name="tile"></param>
        protected override bool OnRemoveEntry(TileViewModelCacheEntry entry)
        {
            entry.TileView.FreeTexture();
            entry.TileView.Dispose();

            return true;
        }

        /// <summary>
        /// When a tile fails a checkpoint it means it was not needed for the last draw operation. 
        /// If the tile does not have a texture loaded it is removed.
        /// Only one of these methods should be running at a time or much of the cache could be deleted
        /// </summary>
        /// <param name="SafeTiles"></param>
        protected override void OnCheckpointFailed(TileViewModelCacheEntry entry)
        {
            //return; 
            //RemoveEntry(entry); 

            if (dictEntries.ContainsKey(entry.Key))
            {
                //Nobody is using it, abort the request
                entry.TileView.AbortRequest();

                RemoveEntry(entry);
                //if (entry.TileViewModel.HasTexture == false)
                //{
                //    RemoveEntry(entry);
                //}
                //else
                //{
                //    //This tile has a texture, so lets keep it around until we reduce the memory footprint
                //    //Can't use yet because nothing sets CheckpointExempt to false
                //    //entry.CheckpointExempt = true; 
                //}
            }
        }

    }
}
