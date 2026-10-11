using System;
using System.Data.SqlTypes;
using FsCheck;
using Google.Protobuf.WellKnownTypes;
using gRPCAnnotationService;
using Xunit;

namespace ConnectomeDataModelCoreTests
{
    /// <summary>
    /// Pins <see cref="TimestampFilters.ModifiedAfterOrNull"/>, the watermark every incremental
    /// "modified after" RPC passes to EF. Anything earlier than <see cref="SqlDateTime.MinValue"/>
    /// (1753-01-01) must become "no lower bound" so a client sending <see cref="DateTime.MinValue"/>
    /// does not overflow a SQL DATETIME parameter; anything at or after it must pass through unchanged.
    /// </summary>
    public class TimestampFiltersTests
    {
        private static readonly DateTime SqlMinUtc =
            DateTime.SpecifyKind((DateTime)SqlDateTime.MinValue, DateTimeKind.Utc);

        [Fact]
        public void NullTimestampMeansNoLowerBound()
        {
            Assert.Null(TimestampFilters.ModifiedAfterOrNull(null));
        }

        [Fact]
        public void DateTimeMinValueMeansNoLowerBound()
        {
            var ts = Timestamp.FromDateTime(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
            Assert.Null(TimestampFilters.ModifiedAfterOrNull(ts));
        }

        /// <summary>A default (unset-fields) Timestamp is the Unix epoch, which SQL accepts, so it is a real bound.</summary>
        [Fact]
        public void DefaultTimestampIsTheUnixEpoch()
        {
            Assert.Equal(DateTime.UnixEpoch, TimestampFilters.ModifiedAfterOrNull(new Timestamp()));
        }

        [Fact]
        public void SqlMinimumItselfPassesThrough()
        {
            var result = TimestampFilters.ModifiedAfterOrNull(Timestamp.FromDateTime(SqlMinUtc));
            Assert.Equal(SqlMinUtc, result);
        }

        [Fact]
        public void OneTickBeforeSqlMinimumMeansNoLowerBound()
        {
            var ts = Timestamp.FromDateTime(SqlMinUtc.AddTicks(-1));
            Assert.Null(TimestampFilters.ModifiedAfterOrNull(ts));
        }

        /// <summary>Every UTC instant SQL DATETIME can hold comes back unchanged and still UTC.</summary>
        [Fact]
        public void ValuesInSqlRangePassThroughUnchanged()
        {
            long span = DateTime.MaxValue.Ticks - SqlMinUtc.Ticks;
            Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue).Select(i => (long)(span * (i / (double)int.MaxValue)))), offset =>
            {
                // Timestamp keeps 100 ns ticks exactly, so equality is exact rather than tolerance-based.
                var value = new DateTime(Math.Min(SqlMinUtc.Ticks + offset, DateTime.MaxValue.Ticks), DateTimeKind.Utc);
                var result = TimestampFilters.ModifiedAfterOrNull(Timestamp.FromDateTime(value));
                return result == value && result.Value.Kind == DateTimeKind.Utc;
            }).QuickCheckThrowOnFailure();
        }

        [Fact]
        public void ValuesBeforeSqlRangeMeanNoLowerBound()
        {
            long span = SqlMinUtc.Ticks;
            Prop.ForAll(Arb.From(Gen.Choose(0, int.MaxValue - 1).Select(i => (long)(span * (i / (double)int.MaxValue)))), ticks =>
            {
                var value = new DateTime(Math.Min(ticks, SqlMinUtc.Ticks - 1), DateTimeKind.Utc);
                return TimestampFilters.ModifiedAfterOrNull(Timestamp.FromDateTime(value)) == null;
            }).QuickCheckThrowOnFailure();
        }
    }
}
