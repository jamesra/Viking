using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Viking.ViewModels;
using Viking.Common;
using Viking.VolumeModel; 

namespace Viking
{
        internal class TileViewModelCacheEntry : CacheEntry<string>
        {
            public TileViewModel TileViewModel;

            public TileViewModelCacheEntry(string Key, TileViewModel T)
                : base(Key)
            {
                TileViewModel = T;
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
        internal class TileViewModelCache : TimeQueueCache<string, TileViewModelCacheEntry, TileViewModel, TileViewModel>
        {

            public TileViewModelCache() : base()
            {
                this.MaxCacheSize = 1 << 28;
            }

            static protected string TileKey(string textureFileName, string TransformName)
            {
                return textureFileName + " " + TransformName;
            }

            public TileViewModel GetTile(string textureFileName, string TransformName)
            {
                return this.Fetch(TileKey(textureFileName, TransformName));
            }

            protected override TileViewModel Fetch(TileViewModelCacheEntry key)
            {
                key.WasUsedSinceLastCheckpoint = true;
                return key.TileViewModel;
            }

            public TileViewModel ConstructTile(Tile tile, 
                                    string textureFileName,
                                    string cachedTextureFileName,
                                    string TransformName,
                                    int MipMapLevels, //Should be one, unless it is the minimum downsample level
                                    int TextureSize)
            { 
                //Check to see if this tile is already loaded
                string key = TileKey(textureFileName, TransformName);
                
                TileViewModel tileViewModel = null;
                bool added = false;
                try
                {
                    tileViewModel = new TileViewModel(tile, 
                        textureFileName,
                        cachedTextureFileName,
                        MipMapLevels,
                        TextureSize);

                    added = Add(key, tileViewModel);
                    if (!added)
                    {
                        tileViewModel.Dispose();
                        tileViewModel = null;
                    }
                }
                catch (Exception)
                {
                    if(tileViewModel != null)
                    {
                        tileViewModel.Dispose();
                        tileViewModel = null;
                    }
                    throw;
                }



                return tileViewModel;
            }

            /// <summary>
            /// Retrieve existing tile if it exists, otherwise create a new one
            /// </summary>
            /// <param name="tile"></param>
            /// <param name="textureFileName"></param>
            /// <param name="cachedTextureFileName"></param>
            /// <param name="TransformName"></param>
            /// <param name="MipMapLevels"></param>
            /// <param name="size"></param>
            /// <returns></returns>
            public TileViewModel FetchOrConstructTile(Tile tile,
                                    string textureFileName,
                                    string cachedTextureFileName,
                                    string TransformName,
                                    int MipMapLevels //Should be one, unless it is the minimum downsample level
                                    )
            {
                string key = TileKey(textureFileName, TransformName);

                TileViewModel tileViewModel = Fetch(key);
                if (tileViewModel != null)
                    return tileViewModel;

                return ConstructTile(tile, textureFileName, cachedTextureFileName, TransformName, MipMapLevels, tile.TextureSize); 
            }

            protected override TileViewModelCacheEntry CreateEntry(string key, TileViewModel value)
            {
                TileViewModelCacheEntry entry = new TileViewModelCacheEntry(key, value);
                return entry;
            }

            /// <summary>
            /// Cleanup the memory allocated for this cache entry. 
            /// RemoveEntry() calls this function
            /// </summary>
            /// <param name="tile"></param>
            protected override bool OnRemoveEntry(TileViewModelCacheEntry entry)
            {
               entry.TileViewModel.FreeTexture();
               entry.TileViewModel.Dispose();
               
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
                    entry.TileViewModel.AbortRequest();

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
