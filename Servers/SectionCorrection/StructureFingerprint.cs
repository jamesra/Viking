using System;
using System.Collections.Generic;
using System.Globalization;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// What a set of structures' annotations looked like when they were read: the per-structure counterpart of
    /// <see cref="SectionFingerprintDto"/>, with the same summaries.  CorrectStructures returns its <see cref="Token"/>
    /// and GetAnswerStamp returns the current one, so a client can tell whether cached offsets are stale
    /// without re-running the correction.
    /// </summary>
    /// <remarks>
    /// Locations are summarised by count, newest <c>LastModified</c> and <c>SUM(ID)</c>; links (both ends among the
    /// structures' locations) by count, newest <c>Created</c> and the sum of <see cref="SectionFingerprintBuilder.LinkKey"/>.
    /// An insert or edit moves a newest time; a delete lowers a count.  The newest time alone (<see cref="Newest"/>, the
    /// annotation_watermark) cannot see a delete, because removing a row leaves the newest remaining time unchanged.
    /// Assumes <c>LastModified</c> changes on every location edit.
    /// </remarks>
    public sealed class StructureFingerprint : IEquatable<StructureFingerprint>
    {
        /// <summary>Bump when the definition changes, so tokens from an older definition never compare equal.</summary>
        const string TokenVersion = "1";

        public long LocationRows { get; init; }
        public DateTime? MaxModified { get; init; }
        public long SumId { get; init; }
        public long LinkRows { get; init; }
        public DateTime? MaxCreated { get; init; }
        public long LinkSum { get; init; }

        /// <summary>Newest location modification or link creation, or null when there are no rows.</summary>
        public DateTime? Newest =>
            MaxModified is null ? MaxCreated
            : MaxCreated is null ? MaxModified
            : MaxModified > MaxCreated ? MaxModified : MaxCreated;

        /// <summary>Opaque comparison string.  Equal tokens mean the structures read the same rows.</summary>
        public string Token => string.Join(":",
            TokenVersion,
            LocationRows.ToString(CultureInfo.InvariantCulture),
            (MaxModified?.Ticks ?? 0).ToString(CultureInfo.InvariantCulture),
            SumId.ToString(CultureInfo.InvariantCulture),
            LinkRows.ToString(CultureInfo.InvariantCulture),
            (MaxCreated?.Ticks ?? 0).ToString(CultureInfo.InvariantCulture),
            LinkSum.ToString(CultureInfo.InvariantCulture));

        /// <summary>
        /// The reference definition over rows read in full; the SQL aggregate in the correction service's loader computes
        /// the same six values server side.
        /// </summary>
        public static StructureFingerprint FromRows(
            IEnumerable<(long Id, DateTime Modified)> locations,
            IEnumerable<(long A, long B, DateTime Created)> links)
        {
            long rows = 0, sumId = 0, linkRows = 0, linkSum = 0;
            DateTime? maxModified = null, maxCreated = null;
            foreach ((long id, DateTime modified) in locations)
            {
                rows++;
                unchecked { sumId += id; }
                if (maxModified is null || modified > maxModified)
                    maxModified = modified;
            }

            foreach ((long a, long b, DateTime created) in links)
            {
                linkRows++;
                unchecked { linkSum += SectionFingerprintBuilder.LinkKey(a, b); }
                if (maxCreated is null || created > maxCreated)
                    maxCreated = created;
            }

            return new StructureFingerprint
            {
                LocationRows = rows,
                MaxModified = maxModified,
                SumId = sumId,
                LinkRows = linkRows,
                MaxCreated = maxCreated,
                LinkSum = linkSum
            };
        }

        public bool Equals(StructureFingerprint other) => other is not null && Token == other.Token;

        public override bool Equals(object obj) => Equals(obj as StructureFingerprint);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Token);

        public override string ToString() => Token;
    }
}
