using System.Collections.Generic;

namespace WebAnnotation.UI.Commands.Segmentation
{
    /// <summary>
    /// Keys of the tiles a <see cref="SegmentationViewportSession"/> has had the server accept.
    /// </summary>
    /// <remarks>
    /// Thread safety: upload continuations run on thread-pool threads (<c>ConfigureAwait(false)</c>), while
    /// <see cref="SegmentationViewportSession.ClearImageId"/> runs from the UI thread when a camera move cancels
    /// work, and status code reads <see cref="Count"/>. A bare <see cref="HashSet{T}"/> corrupts under
    /// concurrent writes, so every member takes one lock. The lock is never held across an await or a call out.
    /// </remarks>
    internal sealed class TileUploadCache
    {
        private readonly object gate = new();
        private readonly HashSet<string> keys = [];

        /// <summary>Number of accepted tiles.</summary>
        public int Count
        {
            get
            {
                lock (gate)
                    return keys.Count;
            }
        }

        /// <summary>True when the server accepted this tile key.</summary>
        public bool Contains(string key)
        {
            lock (gate)
                return keys.Contains(key);
        }

        /// <summary>Records an accepted tile.</summary>
        public void Add(string key)
        {
            lock (gate)
                keys.Add(key);
        }

        /// <summary>
        /// Forgets one tile so it uploads again, for example after the server reports it missing.
        /// </summary>
        public void Remove(string key)
        {
            lock (gate)
                keys.Remove(key);
        }

        /// <summary>Forgets every tile, when the view identity or server image changes.</summary>
        public void Clear()
        {
            lock (gate)
                keys.Clear();
        }
    }
}
