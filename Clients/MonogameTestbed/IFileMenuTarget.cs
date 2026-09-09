namespace MonogameTestbed
{
    /// <summary>
    /// Optional File-menu hooks for tests that can export mesh data.
    /// </summary>
    interface IFileMenuTarget
    {
        /// <summary>
        /// Persist assembled meshes to the default output location. Returns false when nothing was ready to save.
        /// </summary>
        bool TrySaveMesh();
    }
}
