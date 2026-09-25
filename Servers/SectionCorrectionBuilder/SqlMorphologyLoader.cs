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
            public double VolumeX { get; set; }
            public double VolumeY { get; set; }
            public bool HasVolumePosition { get; set; }
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

        public static async Task<DateTime> LoadWatermarkAsync(AnnotationContext db)
        {
            DateTime loc = await db.Locations.AsNoTracking().MaxAsync(l => (DateTime?)l.LastModified).ConfigureAwait(false)
                ?? DateTime.MinValue;
            DateTime link = await db.LocationLinks.AsNoTracking().MaxAsync(l => (DateTime?)l.Created).ConfigureAwait(false)
                ?? DateTime.MinValue;
            return loc > link ? loc : link;
        }

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
SELECT L.ID, L.ParentID, L.Z, L.X, L.Y, L.Terminal, L.OffEdge, L.TypeCode, L.LastModified
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
            foreach (IGrouping<long, CandidateRow> group in candidates.GroupBy(c => c.ParentID))
            {
                MorphologyGraph graph = new((ulong)group.Key, scale);
                foreach (CandidateRow row in group)
                {
                    double xNm;
                    double yNm;
                    if (row.HasVolumePosition)
                    {
                        xNm = row.VolumeX * scale.X.Value;
                        yNm = row.VolumeY * scale.Y.Value;
                    }
                    else
                    {
                        IVolumeToSectionTransform map = MapFor(row.Z);
                        if (!map.TrySectionToVolume(new Vector2(row.X, row.Y), out Vector2 volumeDb))
                            continue;

                        xNm = volumeDb.X * scale.X.Value;
                        yNm = volumeDb.Y * scale.Y.Value;
                    }
                    graph.AddNode(new MorphologyNode((ulong)row.ID, new VolumePointLocation(row, xNm, yNm, scale), graph));
                    accepted.Add((ulong)row.ID);
                    parentById[row.ID] = row.ParentID;
                }

                if (graph.Nodes.Count == 0)
                    continue;
                cells[group.Key] = graph;
            }

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
        /// Locations for the requested structures, plus child structures when <paramref name="includeChildren"/> is set.
        /// Positions are the stored volume centroid (VolumeX/VolumeY times scale), the same point Bajaj reads from VolumeShape.
        /// A location with no volume centroid falls back to mosaic XY mapped through the stos group.
        /// </summary>
        public static async Task<List<MorphologyGraph>> LoadStructuresAsync(
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
                return [];

            var dbOptions = new DbContextOptionsBuilder<AnnotationContext>()
                .UseSqlServer(annotationConnection, sql => sql.UseNetTopologySuite())
                .Options;
            await using AnnotationContext db = new(dbOptions);

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

            // Open curves often leave Location.X/Y at 0; MosaicShape still has the real section geometry.
            var raw = await db.Locations.AsNoTracking()
                .Where(l => ids.Contains(l.ParentId))
                .Select(l => new
                {
                    l.Id,
                    l.ParentId,
                    l.Z,
                    l.X,
                    l.Y,
                    l.VolumeX,
                    l.VolumeY,
                    l.MosaicShape,
                    l.Terminal,
                    l.OffEdge,
                    l.TypeCode,
                    l.LastModified
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            List<CandidateRow> candidates = [];
            foreach (var l in raw)
            {
                double x = l.X;
                double y = l.Y;
                bool hasVolume = l.VolumeX != 0 || l.VolumeY != 0;
                if (!hasVolume && x == 0 && y == 0 && l.MosaicShape is not null && !l.MosaicShape.IsEmpty)
                {
                    var c = l.MosaicShape.Centroid;
                    if (c is not null && !c.IsEmpty)
                    {
                        x = c.X;
                        y = c.Y;
                    }
                }

                candidates.Add(new CandidateRow
                {
                    ID = l.Id,
                    ParentID = l.ParentId,
                    Z = l.Z,
                    X = x,
                    Y = y,
                    VolumeX = l.VolumeX,
                    VolumeY = l.VolumeY,
                    HasVolumePosition = hasVolume,
                    Terminal = l.Terminal,
                    OffEdge = l.OffEdge,
                    TypeCode = l.TypeCode,
                    LastModified = l.LastModified
                });
            }

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
            return BuildCells(candidates, links, volume, scale, stosGroup);
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
