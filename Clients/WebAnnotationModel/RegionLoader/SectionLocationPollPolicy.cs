using System;

namespace WebAnnotationModel
{
    /// <summary>
    /// Pure rules for the idle visible-section location poll (30s <see cref="GetLocationChanges"/>).
    /// Used by AnnotationOverlay and unit tests so skip/watermark behavior stays free of WCF.
    /// </summary>
    public static class SectionLocationPollPolicy
    {
        /// <summary>How often Viking asks the server for locations changed since the section watermark.</summary>
        public const int IntervalMilliseconds = 30_000;

        /// <summary>
        /// True when a section poll should run. False until a server query has seeded the watermark,
        /// or while a full-section WCF query or region annotation load for that section is already in flight.
        /// </summary>
        public static bool ShouldPollSection(
            DateTime lastQueryForSection,
            bool hasOutstandingSectionQuery,
            bool sectionAnnotationLoadInFlight)
        {
            if (lastQueryForSection == DateTime.MinValue)
                return false;
            if (hasOutstandingSectionQuery)
                return false;
            if (sectionAnnotationLoadInFlight)
                return false;
            return true;
        }

        /// <summary>
        /// Monotonic merge of an existing section watermark with server <c>QueryExecutedTime</c> ticks.
        /// Ignores non-positive ticks so a failed or empty out-parameter cannot rewind the watermark.
        /// </summary>
        public static DateTime MergeWatermark(DateTime existing, long ticksAtQueryExecute)
        {
            if (ticksAtQueryExecute <= 0)
                return existing;

            DateTime next = new(ticksAtQueryExecute, DateTimeKind.Utc);
            return next > existing ? next : existing;
        }
    }
}
