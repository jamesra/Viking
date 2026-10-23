using AnnotationVizLib;
using Geometry;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnitsAndScale;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.DataModel.Annotation;
using Viking.SectionCorrection;
using Viking.VolumeModel;

namespace Viking.SectionCorrectionBuilder
{
    public static class SqlMorphologyLoader
    {
        public sealed class CandidateRow
        {
            public long ID { get; set; }
            public long ParentID { get; set; }
            public long Z { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public bool Terminal { get; set; }
            public bool OffEdge { get; set; }
            public short TypeCode { get; set; }
            public DateTime LastModified { get; set; }
        }

        public sealed class LinkRow
        {
            public long A { get; set; }
            public long B { get; set; }
            public DateTime Created { get; set; }
        }

        public sealed class LoadResult
        {
            public string StosGroup { get; init; }
            public List<CandidateRow> Candidates { get; init; }
            public List<LinkRow> Links { get; init; }
            public List<MorphologyGraph> Cells { get; init; }
        }

        public static async Task<Scale> LoadScaleAsync(AnnotationContext db)
        {
            double xy = await ScalarAsync<double>(db, "SELECT dbo.XYScale()").ConfigureAwait(false);
            double z = await ScalarAsync<double>(db, "SELECT dbo.ZScale()").ConfigureAwait(false);
            string xyUnits = await ScalarAsync<string>(db, "SELECT dbo.XYScaleUnits()").ConfigureAwait(false);
            string zUnits = await ScalarAsync<string>(db, "SELECT dbo.ZScaleUnits()").ConfigureAwait(false);
            return new Scale(
                new AxisUnits(xy, string.IsNullOrWhiteSpace(xyUnits) ? "nm" : xyUnits),
                new AxisUnits(xy, string.IsNullOrWhiteSpace(xyUnits) ? "nm" : xyUnits),
                new AxisUnits(z, string.IsNullOrWhiteSpace(zUnits) ? "nm" : zUnits));
        }

        /// <summary>
        /// Loads residual-field candidate locations and links for a rebuild.
        /// Called by <see cref="CorrectionPublisher"/>. SELECT must project every
        /// <see cref="CandidateRow"/> property; the stored VolumeX/VolumeY columns are deliberately not read, because
        /// positions come from mapping mosaic X/Y through the stos group (see <c>BuildCells</c>);
        /// EF Core <c>SqlQuery</c> fails the whole rebuild if a required column is missing.
        /// </summary>
        public static async Task<LoadResult> LoadAsync(
            AnnotationContext db,
            Volume volume,
            IScale scale,
            string stosGroup,
            int minLocations)
        {
            int min = minLocations < 1 ? 3 : minLocations;
            // Inline equivalent of dbo.ResidualFieldCandidateLocations so DBs without schema v85 still work.
            List<CandidateRow> candidates = await db.Database
                .SqlQuery<CandidateRow>($@"
SELECT L.ID, L.ParentID, L.Z, L.X, L.Y,
       L.Terminal, L.OffEdge, L.TypeCode, L.LastModified
FROM dbo.Location AS L
INNER JOIN (
    SELECT ParentID
    FROM dbo.Location
    GROUP BY ParentID
    HAVING COUNT(*) >= {min}
) AS C ON C.ParentID = L.ParentID")
                .ToListAsync()
                .ConfigureAwait(false);

            List<LinkRow> links = await db.Database
                .SqlQuery<LinkRow>($@"
SELECT LK.A, LK.B, LK.Created
FROM dbo.LocationLink AS LK
WHERE LK.A IN (
    SELECT L.ID
    FROM dbo.Location AS L
    INNER JOIN (
        SELECT ParentID
        FROM dbo.Location
        GROUP BY ParentID
        HAVING COUNT(*) >= {min}
    ) AS C ON C.ParentID = L.ParentID
)
AND LK.B IN (
    SELECT L.ID
    FROM dbo.Location AS L
    INNER JOIN (
        SELECT ParentID
        FROM dbo.Location
        GROUP BY ParentID
        HAVING COUNT(*) >= {min}
    ) AS C ON C.ParentID = L.ParentID
)")
                .ToListAsync()
                .ConfigureAwait(false);

            List<MorphologyGraph> cells = BuildCells(candidates, links, volume, scale, stosGroup);
            return new LoadResult
            {
                StosGroup = stosGroup,
                Candidates = candidates,
                Links = links,
                Cells = cells
            };
        }

        /// <summary>
        /// Builds the result from rows fetched elsewhere (the OData loader), mapping them into volume space.
        /// </summary>
        /// <param name="candidates">Candidate locations.</param>
        /// <param name="links">Links among the candidates.</param>
        /// <param name="volume">Volume whose stos transforms map mosaic positions.</param>
        /// <param name="scale">Annotation database scale.</param>
        /// <param name="stosGroup">Transform group.</param>
        public static LoadResult FromRows(
            List<CandidateRow> candidates,
            List<LinkRow> links,
            Volume volume,
            IScale scale,
            string stosGroup) =>
            new()
            {
                StosGroup = stosGroup,
                Candidates = candidates,
                Links = links,
                Cells = BuildCells(candidates, links, volume, scale, stosGroup)
            };

        public static Task<LoadResult> RemapAsync(LoadResult source, Volume volume, IScale scale, string stosGroup)
        {
            List<MorphologyGraph> cells = BuildCells(source.Candidates, source.Links, volume, scale, stosGroup);
            return Task.FromResult(new LoadResult
            {
                StosGroup = stosGroup,
                Candidates = source.Candidates,
                Links = source.Links,
                Cells = cells
            });
        }

        static List<MorphologyGraph> BuildCells(
            List<CandidateRow> candidates,
            List<LinkRow> links,
            Volume volume,
            IScale scale,
            string stosGroup)
        {
            VolumeTransformProvider provider = new(volume, stosGroup);
            Dictionary<long, IVolumeToSectionTransform> maps = [];

            IVolumeToSectionTransform MapFor(long z)
            {
                if (!maps.TryGetValue(z, out IVolumeToSectionTransform map))
                {
                    map = provider.GetSectionToVolumeTransform((int)z);
                    maps[z] = map;
                }

                return map;
            }

            Dictionary<long, MorphologyGraph> cells = [];
            HashSet<ulong> accepted = [];
            Dictionary<long, long> parentById = [];
            int unmapped = 0;
            foreach (IGrouping<long, CandidateRow> group in candidates.GroupBy(c => c.ParentID))
            {
                MorphologyGraph graph = new((ulong)group.Key, scale);
                foreach (CandidateRow row in group)
                {
                    IVolumeToSectionTransform map = MapFor(row.Z);
                    if (!map.TrySectionToVolume(new Vector2(row.X, row.Y), out Vector2 volumeDb))
                    {
                        unmapped++;
                        continue;
                    }

                    double xNm = volumeDb.X * scale.X.Value;
                    double yNm = volumeDb.Y * scale.Y.Value;
                    graph.AddNode(new MorphologyNode((ulong)row.ID, new VolumePointLocation(row, xNm, yNm, scale), graph));
                    accepted.Add((ulong)row.ID);
                    parentById[row.ID] = row.ParentID;
                }

                if (graph.Nodes.Count == 0)
                    continue;
                cells[group.Key] = graph;
            }

            LogMappingGaps(volume, stosGroup, maps.Keys, unmapped);

            foreach (LinkRow link in links)
            {
                if (!accepted.Contains((ulong)link.A) || !accepted.Contains((ulong)link.B))
                    continue;
                if (!parentById.TryGetValue(link.A, out long parent) || !cells.TryGetValue(parent, out MorphologyGraph graph))
                    continue;
                if (!graph.Nodes.ContainsKey((ulong)link.A) || !graph.Nodes.ContainsKey((ulong)link.B))
                    continue;
                graph.AddEdge(new MorphologyEdge(graph, (ulong)link.A, (ulong)link.B));
            }

            return [.. cells.Values];
        }

        /// <summary>
        /// Reports rows the stos could not map and sections absent from the stos group, which
        /// <see cref="VolumeTransformProvider"/> maps through identity without complaint.
        /// </summary>
        static void LogMappingGaps(Volume volume, string stosGroup, IEnumerable<long> sections, int unmapped)
        {
            if (unmapped > 0)
                Console.WriteLine($"{stosGroup}: {unmapped} locations skipped (stos could not map them).");

            if (string.IsNullOrEmpty(stosGroup) || stosGroup == "None" ||
                !volume.Transforms.TryGetValue(stosGroup, out SortedList<int, ITransform> perSection))
                return;

            long[] missing = [.. sections.Where(z => !perSection.ContainsKey((int)z)).OrderBy(z => z)];
            if (missing.Length > 0)
                Console.WriteLine($"{stosGroup}: no stos for section(s) {string.Join(",", missing)}; their annotations map through identity.");
        }

        public sealed class StructuresLoadResult
        {
            public List<MorphologyGraph> Cells { get; init; }

            /// <summary>
            /// The structures' fingerprint, read before their rows (see <see cref="LoadStructuresAsync"/>), so it never
            /// describes newer data than <see cref="Cells"/>.
            /// </summary>
            public StructureFingerprint Fingerprint { get; init; }
        }

        sealed class LocationAggregateRow
        {
            public long Rows { get; set; }
            public DateTime? MaxModified { get; set; }
            public long SumId { get; set; }
        }

        sealed class LinkAggregateRow
        {
            public long Rows { get; set; }
            public DateTime? MaxCreated { get; set; }
            public long SumKey { get; set; }
        }

        /// <summary>
        /// The requested structures, plus their direct children when <paramref name="includeChildren"/> is set.  Shared by
        /// <see cref="LoadStructuresAsync"/> and <see cref="LoadStructureFingerprintAsync(string, IReadOnlyList{long}, bool, CancellationToken)"/>
        /// so a fingerprint always covers the same structures as the corrected positions it is compared with.
        /// </summary>
        static async Task<HashSet<long>> ResolveStructureIdsAsync(
            AnnotationContext db, IReadOnlyList<long> structureIds, bool includeChildren, CancellationToken cancellationToken)
        {
            HashSet<long> ids = [.. structureIds];
            if (includeChildren)
            {
                List<long> children = await db.Structures.AsNoTracking()
                    .Where(s => s.ParentId != null && ids.Contains(s.ParentId.Value))
                    .Select(s => s.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                foreach (long child in children)
                    ids.Add(child);
            }

            return ids;
        }

        static AnnotationContext OpenAnnotationContext(string annotationConnection)
        {
            // Keep NTS for the Location geometry columns on the model; never SELECT those
            // columns into entities here — STCentroid runs in SQL and returns floats only.
            var dbOptions = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(annotationConnection, sql => sql.UseNetTopologySuite())
                .Options;
            return new AnnotationContext(dbOptions);
        }

        /// <summary>
        /// The current <see cref="StructureFingerprint"/> of the requested structures (plus children when asked), aggregated
        /// in SQL: an index seek on <c>Location.ParentID</c> and on <c>LocationLink.A</c>/<c>B</c>, no coordinates, no
        /// volume or stos load.  Called by gRPC <c>GetStructureFingerprint</c>.
        /// </summary>
        public static async Task<StructureFingerprint> LoadStructureFingerprintAsync(
            string annotationConnection,
            IReadOnlyList<long> structureIds,
            bool includeChildren,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(annotationConnection);
            if (structureIds is null || structureIds.Count == 0)
                return StructureFingerprint.FromRows([], []);

            await using AnnotationContext db = OpenAnnotationContext(annotationConnection);
            HashSet<long> ids = await ResolveStructureIdsAsync(db, structureIds, includeChildren, cancellationToken).ConfigureAwait(false);
            return await LoadStructureFingerprintAsync(db, ids, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// The SQL form of <see cref="StructureFingerprint.FromRows"/>.  The link aggregate takes links whose two ends both
        /// belong to the structures, the same links <see cref="LoadStructuresAsync"/> loads.
        /// </summary>
        static async Task<StructureFingerprint> LoadStructureFingerprintAsync(
            AnnotationContext db, HashSet<long> ids, CancellationToken cancellationToken)
        {
            // Structure IDs are longs from the gRPC request only (not user SQL text).
            string idList = string.Join(",", ids);
#pragma warning disable EF1002 // idList is comma-joined Int64s; SqlQuery cannot bind a dynamic IN list
            LocationAggregateRow locations = (await db.Database
                .SqlQueryRaw<LocationAggregateRow>($@"
SELECT COUNT_BIG(*) AS [Rows], MAX(L.LastModified) AS MaxModified, ISNULL(SUM(CAST(L.ID AS bigint)), 0) AS SumId
FROM dbo.Location AS L
WHERE L.ParentID IN ({idList})")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))[0];

            // Same key as SectionFingerprintBuilder.LinkKey (A + 3 * B).
            LinkAggregateRow links = (await db.Database
                .SqlQueryRaw<LinkAggregateRow>($@"
SELECT COUNT_BIG(*) AS [Rows], MAX(LK.Created) AS MaxCreated,
       ISNULL(SUM(CAST(LK.A AS bigint) + 3 * CAST(LK.B AS bigint)), 0) AS SumKey
FROM dbo.LocationLink AS LK
INNER JOIN dbo.Location AS LA ON LA.ID = LK.A
INNER JOIN dbo.Location AS LB ON LB.ID = LK.B
WHERE LA.ParentID IN ({idList}) AND LB.ParentID IN ({idList})")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))[0];
#pragma warning restore EF1002

            return new StructureFingerprint
            {
                LocationRows = locations.Rows,
                MaxModified = locations.MaxModified,
                SumId = locations.SumId,
                LinkRows = links.Rows,
                MaxCreated = links.MaxCreated,
                LinkSum = links.SumKey
            };
        }

        /// <summary>
        /// Locations for the requested structures, plus child structures when <paramref name="includeChildren"/> is set.
        /// Called by gRPC <c>CorrectStructures</c>. Positions are the mosaic XY mapped through the stos group
        /// (the stored VolumeX/VolumeY are ignored: they were saved under older registrations). Open curves often leave X/Y at 0 — centroid
        /// is taken with SQL <c>STCentroid</c> so CurvePolygon never materializes through NetTopologySuite
        /// (NTS throws <c>Unsupported type: CurvePolygon</c> on outline-traced cells such as RC1 c410).
        /// The fingerprint is read first: an edit that lands between it and the rows leaves the response with an older
        /// fingerprint, so the client's next check re-fetches rather than keeping stale offsets.
        /// </summary>
        public static async Task<StructuresLoadResult> LoadStructuresAsync(
            string annotationConnection,
            string volumeUrl,
            string stosGroup,
            IReadOnlyList<long> structureIds,
            bool includeChildren,
            string cachePath,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(annotationConnection);
            ArgumentException.ThrowIfNullOrWhiteSpace(volumeUrl);
            if (structureIds is null || structureIds.Count == 0)
                return new StructuresLoadResult { Cells = [], Fingerprint = StructureFingerprint.FromRows([], []) };

            await using AnnotationContext db = OpenAnnotationContext(annotationConnection);
            HashSet<long> ids = await ResolveStructureIdsAsync(db, structureIds, includeChildren, cancellationToken).ConfigureAwait(false);
            StructureFingerprint fingerprint = await LoadStructureFingerprintAsync(db, ids, cancellationToken).ConfigureAwait(false);

            // Structure IDs are longs from the authenticated gRPC request only (not user SQL text).
            string idList = string.Join(",", ids);
#pragma warning disable EF1002 // idList is comma-joined Int64s; SqlQuery cannot bind a dynamic IN list
            List<CandidateRow> candidates = await db.Database
                .SqlQueryRaw<CandidateRow>($@"
SELECT L.ID, L.ParentID, L.Z,
       CASE WHEN L.X = 0 AND L.Y = 0 AND L.MosaicShape IS NOT NULL
            THEN ISNULL(L.MosaicShape.STCentroid().STX, L.X) ELSE L.X END AS X,
       CASE WHEN L.X = 0 AND L.Y = 0 AND L.MosaicShape IS NOT NULL
            THEN ISNULL(L.MosaicShape.STCentroid().STY, L.Y) ELSE L.Y END AS Y,
       L.Terminal, L.OffEdge, L.TypeCode, L.LastModified
FROM dbo.Location AS L
WHERE L.ParentID IN ({idList})")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
#pragma warning restore EF1002

            List<long> locationIds = [.. candidates.Select(c => c.ID)];
            List<LinkRow> links = await db.LocationLinks.AsNoTracking()
                .Where(l => locationIds.Contains(l.A) && locationIds.Contains(l.B))
                .Select(l => new LinkRow { A = l.A, B = l.B, Created = l.Created })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            UnitsAndScale.Scale scale = await LoadScaleAsync(db).ConfigureAwait(false);
            string cache = cachePath ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SectionCorrectionBuilder");
            Directory.CreateDirectory(cache);
            Volume volume = await Volume.CreateAsync(volumeUrl, cache, null, cancellationToken).ConfigureAwait(false);
            await volume.Initialize(cancellationToken).ConfigureAwait(false);
            return new StructuresLoadResult
            {
                Cells = BuildCells(candidates, links, volume, scale, stosGroup),
                Fingerprint = fingerprint
            };
        }

        static async Task<T> ScalarAsync<T>(AnnotationContext db, string sql)
        {
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = sql;
            await db.Database.OpenConnectionAsync().ConfigureAwait(false);
            object value = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
            if (value is null || value is DBNull)
                return default;
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }

        sealed class VolumePointLocation : ILocationReadOnly
        {
            readonly CandidateRow _row;
            readonly double _xNm;
            readonly double _yNm;
            readonly IScale _scale;

            public VolumePointLocation(CandidateRow row, double xNm, double yNm, IScale scale)
            {
                _row = row;
                _xNm = xNm;
                _yNm = yNm;
                _scale = scale;
            }

            public ulong ID => (ulong)_row.ID;
            public ulong ParentID => (ulong)_row.ParentID;
            public bool Terminal => _row.Terminal;
            public bool OffEdge => _row.OffEdge;
            public bool IsVericosityCap => false;
            public bool IsUntraceable => false;
            public IReadOnlyDictionary<string, string> Attributes { get; } = new Dictionary<string, string>();
            public long UnscaledZ => _row.Z;
            public LocationType TypeCode => LocationType.POINT;
            public double Z => _row.Z * _scale.Z.Value;
            public double? Width => null;
            public string MosaicGeometryWKT => null;
            public string VolumeGeometryWKT => string.Format(
                CultureInfo.InvariantCulture,
                "POINT({0} {1})",
                _xNm,
                _yNm);

            public bool Equals(ILocationReadOnly other) => other != null && ID == other.ID;
        }
    }
}
