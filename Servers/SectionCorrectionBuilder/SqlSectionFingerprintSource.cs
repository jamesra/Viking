using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Viking.DataModel.Annotation;
using Viking.SectionCorrection;

namespace Viking.SectionCorrectionBuilder
{
    /// <summary>
    /// <see cref="ISectionFingerprintSource"/> over the annotation SQL database. Fingerprints are aggregated on the server
    /// from the indexed Z column only, so a check needs no spatial calls and reads no coordinates.
    /// </summary>
    internal sealed class SqlSectionFingerprintSource(AnnotationContext db) : ISectionFingerprintSource
    {
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
    }
}
