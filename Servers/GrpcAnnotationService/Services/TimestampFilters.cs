using System;
using Google.Protobuf.WellKnownTypes;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Incremental-query watermarks. Use this instead of Timestamp.ToDateTime() on ModifiedAfter fields.
    /// </summary>
    internal static class TimestampFilters
    {
        /// <summary>
        /// Treat unset / pre-SQL timestamps as "no lower bound" so clients that send
        /// DateTime.MinValue do not trip SqlDateTime overflow on DATETIME columns.
        /// </summary>
        public static DateTime? ModifiedAfterOrNull(Timestamp timestamp)
        {
            if (timestamp == null)
                return null;

            var value = timestamp.ToDateTime();
            var sqlMin = (DateTime)System.Data.SqlTypes.SqlDateTime.MinValue;
            return value < sqlMin ? null : value;
        }
    }
}
