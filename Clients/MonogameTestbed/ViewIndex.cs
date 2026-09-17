namespace MonogameTestbed
{
    /// <summary>
    /// Shared clamp for optional Bajaj view indices. Empty collections must not produce -1,
    /// and Count is never a valid indexer.
    /// </summary>
    internal static class ViewIndex
    {
        /// <summary>Last valid index, or null when the collection is empty.</summary>
        public static int? LastOrNull(int count) => count > 0 ? count - 1 : null;

        /// <summary>True when the index can be used with a collection of the given count.</summary>
        public static bool InRange(int? index, int count) =>
            index.HasValue && index.Value >= 0 && index.Value < count;

        /// <summary>Clears the index when it is negative or past the last element.</summary>
        public static void ClampOrClear(ref int? index, int count)
        {
            if (index.HasValue && (index.Value < 0 || index.Value >= count))
                index = null;
        }
    }
}
