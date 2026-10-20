namespace WebAnnotationModel
{
    /// <summary>
    /// Cache-only lookups for UI code that runs on the UI thread and must not wait on the server.
    /// </summary>
    public static class StoreCacheExtensions
    {
        /// <summary>
        /// The cached object for <paramref name="key"/>, or null when the store has not loaded it. Never calls the server.
        /// </summary>
        public static OBJECT GetCachedObject<KEY, OBJECT>(this IStoreWithKey<KEY, OBJECT> store, KEY key)
            where KEY : struct, System.IEquatable<KEY>
            where OBJECT : class
        {
            return store.TryGetObjectByID(key, out OBJECT obj) ? obj : null;
        }
    }
}
