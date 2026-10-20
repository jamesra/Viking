using System;
using System.Threading.Tasks;
using Viking.Common;
using Viking.ViewModels;
using Viking.VolumeModel;

namespace Viking
{
    /// <summary>
    /// Cache entry holding one <see cref="TileView"/> and its key. <see cref="CacheEntry{TKey}.Size"/> is taken from the view
    /// at construction so the owning <see cref="TileViewModelCache"/> can bound memory use.
    /// </summary>
    public class TileViewModelCacheEntry : CacheEntry<TileUniqueKey>
    {
        /// <summary>The cached view; owned by the cache, which disposes it in <see cref="TileViewModelCache"/>.OnRemoveEntry.</summary>
        public TileView TileView;

        /// <summary>Wraps <paramref name="T"/> and stamps the access time.</summary>
        /// <param name="Key">Unique identity of the tile.</param>
        /// <param name="T">The view to cache; must not be null (its <c>Size</c> is read).</param>
        public TileViewModelCacheEntry(TileUniqueKey Key, TileView T)
            : base(Key)
        {
            TileView = T;
            LastAccessed = DateTime.Now;
            Size = T.Size;
        }

        public override string ToString() => $"{LastAccessed} - {TileView.TextureFileName}";

        /// <summary>No-op: the <see cref="TileView"/> is disposed by <see cref="TileViewModelCache"/>.OnRemoveEntry, which marshals to the UI thread.</summary>
        public override void Dispose()
        {
        }
    }
    /// <summary>
    /// This object manages construction of tile objects. 
    /// It first checks a cache for a tile matching the request.  If not found it creates a new tile object.
    /// </summary>
    public class TileViewModelCache : TimeQueueCache<TileUniqueKey, TileViewModelCacheEntry, TileView, TileView>
    {

        /// <summary>Creates an empty cache limited to 256 MiB (<c>1 &lt;&lt; 28</c>).</summary>
        public TileViewModelCache() : base()
        {
            this.MaxCacheSize = 1 << 28;
        }

        /// <summary>Returns the cached view for <paramref name="key"/> and marks it used for the current checkpoint, or null when absent.</summary>
        public TileView GetTile(TileUniqueKey key) => this.Fetch(key);

        /// <summary>Marks <paramref name="key"/> as used since the last checkpoint so it survives the next cleanup, and returns its view.</summary>
        protected override TileView Fetch(TileViewModelCacheEntry key)
        {
            key.WasUsedSinceLastCheckpoint = true;
            return key.TileView;
        }

        /// <summary>
        /// Creates a <see cref="TileView"/> for <paramref name="tileViewModel"/> and adds it to the cache.
        /// Texture size comes from <see cref="TileViewModel.TextureSize"/>.
        /// </summary>
        /// <returns>The new view, or null when another thread added the same key first (the loser is disposed).</returns>
        protected TileView? ConstructTile(TileViewModel tileViewModel,
                                string textureFileName,
                                string cachedTextureFileName,
                                string TransformName,
                                int MipMapLevels) //Should be one, unless it is the minimum downsample level
        {
            TileUniqueKey key = tileViewModel.UniqueKey;

            TileView tileView = null;
            bool added = false;
            try
            {
                tileView = new TileView(tileViewModel,
                    textureFileName,
                    cachedTextureFileName,
                    MipMapLevels,
                    tileViewModel.TextureSize,
                    TransformName);

                added = Add(key, tileView);
                if (!added)
                {
                    tileView.Dispose();
                    tileView = null;
                }
            }
            catch (Exception)
            {
                tileView?.Dispose();
                tileView = null;
                throw;
            }

            return tileView;
        }

        /// <summary>
        /// Retrieve existing tile if it exists, otherwise create a new one. Safe to call from several threads for the same key;
        /// the loser of a creation race receives the winner's view.
        /// </summary>
        /// <param name="tileViewModel">Supplies the unique key and texture size.</param>
        /// <param name="textureFileName">Source texture file name.</param>
        /// <param name="cachedTextureFileName">Local cache file name for the texture.</param>
        /// <param name="TransformName">Name of the section transform the tile is drawn with.</param>
        /// <param name="MipMapLevels">Mip levels to build; one unless this is the minimum downsample level.</param>
        /// <returns>The cached or new view; null only if the winning entry was evicted again before the retry fetch.</returns>
        public TileView FetchOrConstructTile(TileViewModel tileViewModel,
                                string textureFileName,
                                string cachedTextureFileName,
                                string TransformName,
                                int MipMapLevels //Should be one, unless it is the minimum downsample level
                                )
        {
            TileUniqueKey key = tileViewModel.UniqueKey;

            TileView tileView = Fetch(key);
            if (tileView != null)
                return tileView;

            tileView = ConstructTile(tileViewModel, textureFileName, cachedTextureFileName, TransformName, MipMapLevels);
            if (tileView != null)
                return tileView;

            // Race condition: another thread added the key between our Fetch and ConstructTile.
            // ConstructTile returned null because Add failed. Retry the fetch.
            return Fetch(key);
        }

        /// <summary>Wraps an existing view in a cache entry.</summary>
        protected override TileViewModelCacheEntry CreateEntry(TileUniqueKey key, TileView value)
        {
            TileViewModelCacheEntry entry = new(key, value);
            return entry;
        }

        /// <summary>Builds the view with <paramref name="valueFactory"/> and wraps it in a cache entry.</summary>
        protected override TileViewModelCacheEntry CreateEntry(TileUniqueKey key, Func<TileUniqueKey, TileView> valueFactory) => new TileViewModelCacheEntry(key, valueFactory(key));

        /// <summary>Synchronous wrapper: the view already exists, so no real async work is needed.</summary>
        protected override Task<TileViewModelCacheEntry> CreateEntryAsync(TileUniqueKey key, TileView value) => Task.FromResult(CreateEntry(key, value));

        /// <summary>
        /// Cleanup the memory allocated for this cache entry.
        /// RemoveEntry() calls this function. TileView owns GraphicsResources (VB/IB/texture)
        /// that must be disposed on the UI thread; we marshal disposal there when possible.
        /// </summary>
        protected override bool OnRemoveEntry(TileViewModelCacheEntry entry)
        {
            TileView tileView = entry.TileView;
            System.Windows.Threading.Dispatcher dispatcher = TileLoadEnvironment.UiDispatcher;

            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() =>
                {
                    tileView.FreeTexture();
                    tileView.Dispose();
                }));
            }
            else
            {
                tileView.FreeTexture();
                tileView.Dispose();
            }

            return true;
        }

        /// <summary>
        /// When a tile fails a checkpoint it means it was not needed for the last draw operation. 
        /// If the tile does not have a texture loaded it is removed.
        /// Only one of these methods should be running at a time or much of the cache could be deleted
        /// </summary>
        /// <param name="entry">The entry that was not used since the last checkpoint.</param>
        protected override void OnCheckpointFailed(TileViewModelCacheEntry entry)
        {
            if (dictEntries.ContainsKey(entry.Key))
            {
                //Nobody is using it, abort the request
                entry.TileView.AbortRequest();

                RemoveEntry(entry);
            }
        }

    }
}
