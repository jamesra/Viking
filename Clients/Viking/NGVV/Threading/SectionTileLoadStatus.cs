namespace Viking
{
    /// <summary>
    /// Reports whether the tiles of a section have finished loading, across the download queue and the
    /// queue that creates textures on the main thread.
    /// </summary>
    public static class SectionTileLoadStatus
    {
        /// <summary>
        /// True when no tile of <paramref name="section"/> is waiting to download or to become a texture.
        /// </summary>
        public static bool IsLoaded(int section) =>
            !TextureRequestQueue.HasPendingForSection(section) && !PendingTextureQueue.HasPendingForSection(section);
    }
}
