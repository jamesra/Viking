using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.SectionCorrection;
using Viking.SectionCorrectionBuilder;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>
    /// Annotation rows held in memory, answering the fingerprint query the way the SQL source does, so the publisher's
    /// change check can be exercised without a database.
    /// </summary>
    sealed class InMemoryFingerprintSource : ISectionFingerprintSource
    {
        public InMemoryFingerprintSource(List<SqlMorphologyLoader.CandidateRow> rows, List<SqlMorphologyLoader.LinkRow> links)
        {
            Rows = rows;
            Links = links;
        }

        public List<SqlMorphologyLoader.CandidateRow> Rows { get; }
        public List<SqlMorphologyLoader.LinkRow> Links { get; }

        public static IScale Scale { get; } = new Scale(
            new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(70, "nm"));

        public Task<Dictionary<long, SectionFingerprintDto>> LoadFingerprintsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SectionFingerprintBuilder.Compute(
                Rows.Select(r => (r.ID, r.ParentID, r.Z, r.LastModified)),
                Links.Select(l => (l.A, l.B, l.Created))));

        /// <summary>
        /// Structures that wander through one region of the volume, each a chain with one location per section and the
        /// occasional skipped section. Positions drift smoothly with per-location jitter, so residuals are small but real,
        /// and enough structures overlap in every section for the lattice to reach its minimum votes.
        /// </summary>
        public static InMemoryFingerprintSource Generate(int seed, int structures = 14, int length = 45)
        {
            Random rng = new(seed);
            List<SqlMorphologyLoader.CandidateRow> rows = [];
            List<SqlMorphologyLoader.LinkRow> links = [];
            DateTime baseTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int s = 0; s < structures; s++)
            {
                long parent = 1000 + s;
                long z = 10 + rng.Next(0, 12);
                double x = 2000 + rng.NextDouble() * 3000;
                double y = 2000 + rng.NextDouble() * 3000;
                double vx = (rng.NextDouble() - 0.5) * 40;
                double vy = (rng.NextDouble() - 0.5) * 40;
                int count = length + rng.Next(-10, 10);
                long previous = 0;
                for (int i = 0; i < count; i++)
                {
                    long id = s * 10000L + i + 1;
                    rows.Add(new SqlMorphologyLoader.CandidateRow
                    {
                        ID = id,
                        ParentID = parent,
                        Z = z,
                        X = x + (rng.NextDouble() - 0.5) * 300,
                        Y = y + (rng.NextDouble() - 0.5) * 300,
                        Terminal = i == 0 || i == count - 1,
                        TypeCode = 1,
                        LastModified = baseTime
                    });
                    if (previous != 0)
                        links.Add(new SqlMorphologyLoader.LinkRow { A = previous, B = id, Created = baseTime });
                    previous = id;
                    z += rng.NextDouble() < 0.15 ? 2 + rng.Next(0, 2) : 1;
                    x += vx;
                    y += vy;
                }
            }

            return new InMemoryFingerprintSource(rows, links);
        }
    }
}
