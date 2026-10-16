using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Viking.SectionCorrection
{
    /// <summary>
    /// What a published section was built from: a cheap per-Z summary of the annotation rows and the stos transform.
    /// Two builds that read the same fingerprint for a Z read the same rows there, so the published section can be kept.
    /// </summary>
    /// <remarks>
    /// Location rows are summarised by count, newest <c>LastModified</c>, <c>SUM(ID)</c> and <c>SUM(ParentID)</c>. Link rows
    /// are counted once under each end's Z and summarised by count, newest <c>Created</c> and the sum of
    /// <see cref="SectionFingerprintBuilder.LinkKey"/>. Count and sum change on insert and delete, which the old
    /// volume-wide watermark could not see.
    /// </remarks>
    public sealed class SectionFingerprintDto : IEquatable<SectionFingerprintDto>
    {
        [JsonPropertyName("rows")]
        public long Rows { get; set; }

        [JsonPropertyName("max_modified")]
        public long MaxModifiedTicks { get; set; }

        [JsonPropertyName("sum_id")]
        public long SumId { get; set; }

        [JsonPropertyName("sum_parent")]
        public long SumParent { get; set; }

        [JsonPropertyName("link_rows")]
        public long LinkRows { get; set; }

        [JsonPropertyName("link_max_created")]
        public long LinkMaxCreatedTicks { get; set; }

        [JsonPropertyName("link_sum")]
        public long LinkSum { get; set; }

        /// <summary>Identity of the stos transform that maps this section; empty for identity mapping.</summary>
        [JsonPropertyName("stos")]
        public string Stos { get; set; } = "";

        /// <summary>Newest modification or link creation in this section, or null when the section has no rows.</summary>
        public DateTime? Newest
        {
            get
            {
                long ticks = Math.Max(MaxModifiedTicks, LinkMaxCreatedTicks);
                return ticks > 0 ? new DateTime(ticks) : (DateTime?)null;
            }
        }

        public bool Equals(SectionFingerprintDto other) =>
            other != null
            && Rows == other.Rows
            && MaxModifiedTicks == other.MaxModifiedTicks
            && SumId == other.SumId
            && SumParent == other.SumParent
            && LinkRows == other.LinkRows
            && LinkMaxCreatedTicks == other.LinkMaxCreatedTicks
            && LinkSum == other.LinkSum
            && string.Equals(Stos ?? "", other.Stos ?? "", StringComparison.Ordinal);

        public override bool Equals(object obj) => Equals(obj as SectionFingerprintDto);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Rows.GetHashCode();
                hash = hash * 31 + MaxModifiedTicks.GetHashCode();
                hash = hash * 31 + SumId.GetHashCode();
                hash = hash * 31 + SumParent.GetHashCode();
                hash = hash * 31 + LinkRows.GetHashCode();
                return hash * 31 + LinkSum.GetHashCode();
            }
        }
    }

    /// <summary>Compares the fingerprints a set was published from with the current ones.</summary>
    public static class SectionFingerprintDiff
    {
        /// <summary>
        /// Sections whose fingerprint differs, including sections present on only one side (deleted or new).
        /// </summary>
        /// <param name="published">Fingerprints stored in the published manifest.</param>
        /// <param name="current">Fingerprints read from the annotation source now.</param>
        /// <returns>The dirty section numbers in ascending order.</returns>
        public static SortedSet<long> DirtySections(
            IReadOnlyDictionary<long, SectionFingerprintDto> published,
            IReadOnlyDictionary<long, SectionFingerprintDto> current)
        {
            SortedSet<long> dirty = [];
            foreach (KeyValuePair<long, SectionFingerprintDto> kv in current)
            {
                if (!published.TryGetValue(kv.Key, out SectionFingerprintDto old) || !old.Equals(kv.Value))
                    dirty.Add(kv.Key);
            }

            foreach (long z in published.Keys)
            {
                if (!current.ContainsKey(z))
                    dirty.Add(z);
            }

            return dirty;
        }

        /// <summary>Newest modification or link creation over all sections, or <see cref="DateTime.MinValue"/>.</summary>
        public static DateTime Newest(IEnumerable<SectionFingerprintDto> fingerprints)
        {
            DateTime newest = DateTime.MinValue;
            foreach (SectionFingerprintDto fp in fingerprints)
            {
                if (fp.Newest is DateTime t && t > newest)
                    newest = t;
            }

            return newest;
        }
    }

    /// <summary>Computes fingerprints from rows read in full, for sources that cannot aggregate server side.</summary>
    public static class SectionFingerprintBuilder
    {
        /// <summary>The value a link row contributes to <see cref="SectionFingerprintDto.LinkSum"/>.</summary>
        public static long LinkKey(long a, long b) => unchecked(a + 3 * b);

        /// <summary>
        /// Summarises location and link rows per Z. Matches the SQL aggregate in the SQL morphology source: a link
        /// counts under the Z of each end, and links whose end is unknown are ignored.
        /// </summary>
        /// <param name="locations">All annotation locations.</param>
        /// <param name="links">All location links.</param>
        public static Dictionary<long, SectionFingerprintDto> Compute(
            IEnumerable<(long Id, long ParentId, long Z, DateTime Modified)> locations,
            IEnumerable<(long A, long B, DateTime Created)> links)
        {
            Dictionary<long, SectionFingerprintDto> result = [];
            Dictionary<long, long> zById = [];

            SectionFingerprintDto For(long z)
            {
                if (!result.TryGetValue(z, out SectionFingerprintDto fp))
                {
                    fp = new SectionFingerprintDto();
                    result[z] = fp;
                }

                return fp;
            }

            foreach ((long id, long parent, long z, DateTime modified) in locations)
            {
                zById[id] = z;
                SectionFingerprintDto fp = For(z);
                fp.Rows++;
                fp.MaxModifiedTicks = Math.Max(fp.MaxModifiedTicks, modified.Ticks);
                unchecked
                {
                    fp.SumId += id;
                    fp.SumParent += parent;
                }
            }

            foreach ((long a, long b, DateTime created) in links)
            {
                long key = LinkKey(a, b);
                foreach (long end in new[] { a, b })
                {
                    if (!zById.TryGetValue(end, out long z))
                        continue;
                    SectionFingerprintDto fp = For(z);
                    fp.LinkRows++;
                    fp.LinkMaxCreatedTicks = Math.Max(fp.LinkMaxCreatedTicks, created.Ticks);
                    unchecked
                    {
                        fp.LinkSum += key;
                    }
                }
            }

            return result;
        }
    }
}
