using System;

namespace WebAnnotationModel
{
    /// <summary>
    /// Region refresh compares elapsed wall-clock time, not TimeSpan.Seconds (0–59).
    /// A 180s interval never matches .Seconds.
    /// </summary>
    public static class RegionRefreshTiming
    {
        /// <summary>
        /// True when more than <paramref name="intervalSeconds"/> have passed since lastQueryUtc.
        /// </summary>
        public static bool IsIntervalElapsed(DateTime lastQueryUtc, DateTime nowUtc, double intervalSeconds) =>
            (nowUtc - lastQueryUtc).TotalSeconds > intervalSeconds;
    }
}
