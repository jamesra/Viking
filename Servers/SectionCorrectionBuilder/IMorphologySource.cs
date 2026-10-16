using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.SectionCorrection;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// The annotation queries an incremental publish needs, implemented over SQL and over OData so the planning and
    /// publishing code is shared. The full rebuild keeps its own loaders.
    /// </summary>
    internal interface IMorphologySource
    {
        /// <summary>One fingerprint per Z that has location rows. Stos identity is not set here.</summary>
        Task<Dictionary<long, SectionFingerprintDto>> LoadFingerprintsAsync(CancellationToken cancellationToken);

        /// <summary>Distinct structure IDs that own a location in any of the inclusive Z ranges.</summary>
        Task<List<long>> LoadParentIdsInRangesAsync(IReadOnlyList<(long Lo, long Hi)> ranges, CancellationToken cancellationToken);

        /// <summary>
        /// Every location of the structures, regardless of structure size, and the links among them. Rows are
        /// returned as stored; positions are mapped through the stos group by the caller.
        /// </summary>
        Task<(List<SqlMorphologyLoader.CandidateRow> Candidates, List<SqlMorphologyLoader.LinkRow> Links)> LoadStructureRowsAsync(
            IReadOnlyCollection<long> parentIds, CancellationToken cancellationToken);
    }

    /// <summary>Helpers for turning section sets into the inclusive ranges the sources filter on.</summary>
    internal static class SectionRanges
    {
        /// <summary>Collapses section numbers into ascending inclusive runs.</summary>
        public static List<(long Lo, long Hi)> ToRanges(IEnumerable<long> sections) => Widen(sections, 0);

        /// <summary>Each section widened by <paramref name="padding"/> on both sides, merged into ascending runs.</summary>
        public static List<(long Lo, long Hi)> Widen(IEnumerable<long> sections, int padding)
        {
            List<(long Lo, long Hi)> ranges = [];
            foreach (long z in sections.Distinct().OrderBy(z => z))
            {
                long lo = z - padding;
                long hi = z + padding;
                if (ranges.Count > 0 && ranges[^1].Hi + 1 >= lo)
                    ranges[^1] = (ranges[^1].Lo, System.Math.Max(ranges[^1].Hi, hi));
                else
                    ranges.Add((lo, hi));
            }

            return ranges;
        }

        /// <summary>Splits ranges into batches so one query never carries an unbounded predicate.</summary>
        public static IEnumerable<List<(long Lo, long Hi)>> Batches(IReadOnlyList<(long Lo, long Hi)> ranges, int size)
        {
            for (int i = 0; i < ranges.Count; i += size)
                yield return [.. ranges.Skip(i).Take(size)];
        }

        /// <summary>Splits values into batches of at most <paramref name="size"/>.</summary>
        public static IEnumerable<List<long>> Batches(IReadOnlyCollection<long> values, int size)
        {
            List<long> batch = new(size);
            foreach (long v in values)
            {
                batch.Add(v);
                if (batch.Count == size)
                {
                    yield return batch;
                    batch = new List<long>(size);
                }
            }

            if (batch.Count > 0)
                yield return batch;
        }
    }
}
