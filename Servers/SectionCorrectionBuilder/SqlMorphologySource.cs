using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.SectionCorrection;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// <see cref="IMorphologySource"/> over the annotation SQL database. Fingerprints are aggregated on the server from
    /// the indexed Z column only, so a check needs no spatial calls and reads no coordinates.
    /// </summary>
    /// <remarks>
    /// Every interpolated value in the raw SQL below is an Int64 produced by this code (section numbers, structure IDs),
    /// never text from a caller, because SqlQueryRaw cannot bind a dynamic list.
    /// </remarks>
    internal sealed class SqlMorphologySource(AnnotationContext db) : IMorphologySource
    {
        const int RangesPerQuery = 200;
        const int IdsPerQuery = 500;

        sealed class LocationFingerprintRow
        {
            public long Z { get; set; }
            public long Rows { get; set; }
            public DateTime MaxModified { get; set; }
            public long SumId { get; set; }
            public long SumParent { get; set; }
        }

        sealed class LinkFingerprintRow
        {
            public long Z { get; set; }
            public long Rows { get; set; }
            public DateTime MaxCreated { get; set; }
            public long SumKey { get; set; }
        }

        public async Task<Dictionary<long, SectionFingerprintDto>> LoadFingerprintsAsync(CancellationToken cancellationToken)
        {
            List<LocationFingerprintRow> locations = await db.Database
                .SqlQuery<LocationFingerprintRow>($@"
SELECT CAST(Z AS bigint) AS Z, COUNT_BIG(*) AS [Rows], MAX(LastModified) AS MaxModified,
       SUM(CAST(ID AS bigint)) AS SumId, SUM(CAST(ParentID AS bigint)) AS SumParent
FROM dbo.Location
GROUP BY Z")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // A link counts once under each end's Z, with the same key as SectionFingerprintBuilder.LinkKey (A + 3 * B).
            List<LinkFingerprintRow> links = await db.Database
                .SqlQuery<LinkFingerprintRow>($@"
SELECT T.Z AS Z, COUNT_BIG(*) AS [Rows], MAX(T.Created) AS MaxCreated, SUM(T.K) AS SumKey
FROM (
    SELECT CAST(LA.Z AS bigint) AS Z, LK.Created AS Created, CAST(LK.A AS bigint) + 3 * CAST(LK.B AS bigint) AS K
    FROM dbo.LocationLink AS LK
    INNER JOIN dbo.Location AS LA ON LA.ID = LK.A
    UNION ALL
    SELECT CAST(LB.Z AS bigint) AS Z, LK.Created AS Created, CAST(LK.A AS bigint) + 3 * CAST(LK.B AS bigint) AS K
    FROM dbo.LocationLink AS LK
    INNER JOIN dbo.Location AS LB ON LB.ID = LK.B
) AS T
GROUP BY T.Z")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Dictionary<long, SectionFingerprintDto> result = [];
            foreach (LocationFingerprintRow row in locations)
            {
                result[row.Z] = new SectionFingerprintDto
                {
                    Rows = row.Rows,
                    MaxModifiedTicks = row.MaxModified.Ticks,
                    SumId = row.SumId,
                    SumParent = row.SumParent
                };
            }

            foreach (LinkFingerprintRow row in links)
            {
                if (!result.TryGetValue(row.Z, out SectionFingerprintDto fp))
                {
                    fp = new SectionFingerprintDto();
                    result[row.Z] = fp;
                }

                fp.LinkRows = row.Rows;
                fp.LinkMaxCreatedTicks = row.MaxCreated.Ticks;
                fp.LinkSum = row.SumKey;
            }

            return result;
        }

        public async Task<List<long>> LoadParentIdsInRangesAsync(
            IReadOnlyList<(long Lo, long Hi)> ranges,
            CancellationToken cancellationToken)
        {
            HashSet<long> ids = [];
            foreach (List<(long Lo, long Hi)> batch in SectionRanges.Batches(ranges, RangesPerQuery))
            {
                string predicate = string.Join(" OR ", batch.Select(r =>
                    string.Create(CultureInfo.InvariantCulture, $"(L.Z BETWEEN {r.Lo} AND {r.Hi})")));
#pragma warning disable EF1002 // predicate is built from Int64 section numbers only
                List<long> parents = await db.Database
                    .SqlQueryRaw<long>($"SELECT DISTINCT L.ParentID AS Value FROM dbo.Location AS L WHERE {predicate}")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
#pragma warning restore EF1002
                ids.UnionWith(parents);
            }

            return [.. ids];
        }

        public async Task<(List<SqlMorphologyLoader.CandidateRow> Candidates, List<SqlMorphologyLoader.LinkRow> Links)> LoadStructureRowsAsync(
            IReadOnlyCollection<long> parentIds,
            CancellationToken cancellationToken)
        {
            List<SqlMorphologyLoader.CandidateRow> candidates = [];
            List<SqlMorphologyLoader.LinkRow> links = [];
            foreach (List<long> batch in SectionRanges.Batches(parentIds, IdsPerQuery))
            {
                string idList = string.Join(",", batch);
#pragma warning disable EF1002 // idList is comma-joined Int64 structure IDs
                // Same projection as SqlMorphologyLoader.LoadAsync so an incremental build sees the rows a full build does.
                candidates.AddRange(await db.Database
                    .SqlQueryRaw<SqlMorphologyLoader.CandidateRow>($@"
SELECT L.ID, L.ParentID, L.Z, L.X, L.Y,
       L.Terminal, L.OffEdge, L.TypeCode, L.LastModified
FROM dbo.Location AS L
WHERE L.ParentID IN ({idList})")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false));

                links.AddRange(await db.Database
                    .SqlQueryRaw<SqlMorphologyLoader.LinkRow>($@"
SELECT LK.A, LK.B, LK.Created
FROM dbo.LocationLink AS LK
WHERE LK.A IN (SELECT ID FROM dbo.Location WHERE ParentID IN ({idList}))
  AND LK.B IN (SELECT ID FROM dbo.Location WHERE ParentID IN ({idList}))")
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false));
#pragma warning restore EF1002
            }

            return (candidates, links);
        }
    }
}
