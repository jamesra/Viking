using AnnotationVizLib;
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
    /// Annotation rows held in memory, answering the incremental publish's queries the way the SQL source does, so the
    /// planning and publishing code can be compared with a full build without a database.
    /// </summary>
    sealed class InMemoryMorphologySource : IMorphologySource
    {
        public InMemoryMorphologySource(List<SqlMorphologyLoader.CandidateRow> rows, List<SqlMorphologyLoader.LinkRow> links)
        {
            Rows = rows;
            Links = links;
        }

        public List<SqlMorphologyLoader.CandidateRow> Rows { get; }
        public List<SqlMorphologyLoader.LinkRow> Links { get; }

        /// <summary>Structures read through <see cref="LoadStructureRowsAsync"/>, to check an update reads few.</summary>
        public HashSet<long> StructuresRead { get; } = [];

        public static IScale Scale { get; } = new Scale(
            new AxisUnits(1, "nm"), new AxisUnits(1, "nm"), new AxisUnits(70, "nm"));

        public Task<Dictionary<long, SectionFingerprintDto>> LoadFingerprintsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SectionFingerprintBuilder.Compute(
                Rows.Select(r => (r.ID, r.ParentID, r.Z, r.LastModified)),
                Links.Select(l => (l.A, l.B, l.Created))));

        public Task<List<long>> LoadParentIdsInRangesAsync(IReadOnlyList<(long Lo, long Hi)> ranges, CancellationToken cancellationToken) =>
            Task.FromResult(Rows
                .Where(r => ranges.Any(g => r.Z >= g.Lo && r.Z <= g.Hi))
                .Select(r => r.ParentID)
                .Distinct()
                .ToList());

        public Task<(List<SqlMorphologyLoader.CandidateRow> Candidates, List<SqlMorphologyLoader.LinkRow> Links)> LoadStructureRowsAsync(
            IReadOnlyCollection<long> parentIds, CancellationToken cancellationToken)
        {
            HashSet<long> parents = [.. parentIds];
            StructuresRead.UnionWith(parents);
            List<SqlMorphologyLoader.CandidateRow> candidates = [.. Rows.Where(r => parents.Contains(r.ParentID))];
            HashSet<long> ids = [.. candidates.Select(c => c.ID)];
            List<SqlMorphologyLoader.LinkRow> links = [.. Links.Where(l => ids.Contains(l.A) && ids.Contains(l.B))];
            return Task.FromResult((candidates, links));
        }

        /// <summary>What a full rebuild produces from the current rows, structures smaller than <paramref name="minLocations"/> excluded.</summary>
        public Dictionary<long, SectionVectorField> FullBuild(int minLocations)
        {
            HashSet<long> admitted = [.. Rows.GroupBy(r => r.ParentID).Where(g => g.Count() >= minLocations).Select(g => g.Key)];
            SqlMorphologyLoader.LoadResult load = SqlMorphologyLoader.FromRows(
                [.. Rows.Where(r => admitted.Contains(r.ParentID))], Links, null, Scale, "None");
            NeighborResidualField field = NeighborResidualField.Build(
                NeighborResidualField.CollectResiduals(load.Cells, MorphologyGraph.ResidualWindowOptions.Default));
            return CorrectionPublisher.FromResidualField(
                "None", DateTime.UtcNow, field, MorphologyGraph.ResidualWindowOptions.Default, "", minLocations)
                .Sections.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public InMemoryMorphologySource Clone() =>
            new([.. Rows.Select(Copy)], [.. Links.Select(l => new SqlMorphologyLoader.LinkRow { A = l.A, B = l.B, Created = l.Created })]);

        public static SqlMorphologyLoader.CandidateRow Copy(SqlMorphologyLoader.CandidateRow r) => new()
        {
            ID = r.ID, ParentID = r.ParentID, Z = r.Z, X = r.X, Y = r.Y,
            Terminal = r.Terminal, OffEdge = r.OffEdge, TypeCode = r.TypeCode, LastModified = r.LastModified
        };

        /// <summary>
        /// Structures that wander through one region of the volume, each a chain with one location per section and the
        /// occasional skipped section. Positions drift smoothly with per-location jitter, so residuals are small but real,
        /// and enough structures overlap in every section for the lattice to reach its minimum votes.
        /// </summary>
        public static InMemoryMorphologySource Generate(int seed, int structures = 14, int length = 45)
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

            // Tiny structures sit at the build's minimum size, where one location more or less admits or drops the whole structure.
            for (int k = 0; k < 3; k++)
            {
                long parent = 5000 + k;
                long z = 20 + k * 3;
                long previous = 0;
                for (int i = 0; i < 2 + k; i++)
                {
                    long id = 800000L + k * 10 + i + 1;
                    rows.Add(new SqlMorphologyLoader.CandidateRow
                    {
                        ID = id, ParentID = parent, Z = z + i,
                        X = 3500 + k * 400 + (rng.NextDouble() - 0.5) * 300, Y = 3500 + (rng.NextDouble() - 0.5) * 300,
                        Terminal = i == 0, TypeCode = 1, LastModified = baseTime
                    });
                    if (previous != 0)
                        links.Add(new SqlMorphologyLoader.LinkRow { A = previous, B = id, Created = baseTime });
                    previous = id;
                }
            }

            return new InMemoryMorphologySource(rows, links);
        }
    }
}
