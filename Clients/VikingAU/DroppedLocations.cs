using Geometry;
using Geometry.Transforms;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;
using Path = System.IO.Path;

namespace Viking.AU
{
    /// <summary>One location the remap deletes, with everything needed to recreate it by hand.</summary>
    sealed class DroppedLocationRecord
    {
        public long Id { get; set; }
        public long? StructureId { get; set; }
        public long? StructureTypeId { get; set; }
        public long Section { get; set; }
        public string TypeCode { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double VolumeX { get; set; }
        public double VolumeY { get; set; }
        public double Radius { get; set; }
        public double? Width { get; set; }
        public string MosaicShapeWkt { get; set; }
        public string VolumeShapeWkt { get; set; }
        public bool Terminal { get; set; }
        public bool OffEdge { get; set; }
        public bool Untraceable { get; set; }
        public bool VericosityCap { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = [];
        public string Username { get; set; }
        public DateTime LastModified { get; set; }

        /// <summary>Tiles of the old mosaic the location lay on, none of which the new mosaic places.</summary>
        public List<int> DroppedTiles { get; set; } = [];

        /// <summary>Its links before the remap: the linked location, its section and structure.</summary>
        public List<DroppedLink> LinksBefore { get; set; } = [];

        /// <summary>Index into <see cref="DroppedLocationLog.Groups"/>.</summary>
        public int Group { get; set; }

        /// <summary>planned (dry run), deleted, not deleted (with why).</summary>
        public string Outcome { get; set; }
    }

    sealed class DroppedLink
    {
        public long LocationId { get; set; }
        public long? Section { get; set; }
        public long? StructureId { get; set; }

        /// <summary>True when the linked location could not be read from the server.</summary>
        public bool NotFound { get; set; }
    }

    /// <summary>A connected set of dropped locations and the links that replace it.</summary>
    sealed class DroppedGroup
    {
        public int Index { get; set; }
        public List<long> Removed { get; set; } = [];
        public List<long> Neighbours { get; set; } = [];

        /// <summary>Links between the neighbours, as [A, B], added in place of the removed locations.</summary>
        public List<long[]> LinksAdded { get; set; } = [];

        public List<long[]> AlreadyLinked { get; set; } = [];

        /// <summary>Neighbour pairs not linked because they belong to different structures or one could not be read.</summary>
        public List<long[]> NotLinked { get; set; } = [];

        /// <summary>Links whose creation failed, with the error; the group's locations are then kept.</summary>
        public List<string> LinkFailures { get; set; } = [];
    }

    /// <summary>A structure whose every location is dropped. The remap does not delete structures.</summary>
    sealed class EmptiedStructure
    {
        public long StructureId { get; set; }
        public long? StructureTypeId { get; set; }
        public string Label { get; set; }
        public List<long> Locations { get; set; } = [];
    }

    /// <summary>The machine-readable record remap-from-tiles writes next to its report.</summary>
    sealed class DroppedLocationLog
    {
        public string Volume { get; set; }
        public int Section { get; set; }
        public string OldMosaic { get; set; }
        public string NewMosaic { get; set; }
        public string TileCoordinates { get; set; }
        public DateTime Run { get; set; }
        public bool DryRun { get; set; }
        public List<DroppedLocationRecord> Locations { get; set; } = [];
        public List<DroppedGroup> Groups { get; set; } = [];
        public List<EmptiedStructure> StructuresLeftWithoutLocations { get; set; } = [];
    }

    /// <summary>
    /// Locations that lie wholly on tiles the new mosaic dropped (James Anderson, 2026-11-20: "annotations that are not on
    /// tiles should be removed and any links connected to each other ... It is important we account for all dropped
    /// annotation, logging them so they can be recreated if needed."). Each connected set of dropped locations is replaced by
    /// links between the locations it joined (<see cref="LinkBridging"/>), then deleted; every one is logged first. A
    /// location only partly on dropped tiles is not deleted: remap-from-tiles skips and reports it. Structures left with no
    /// location are reported, not deleted.
    /// </summary>
    static class DroppedLocations
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
        };

        /// <summary>Builds the log: the records, the link groups and the structures the deletes would empty. Reads only.</summary>
        public static async Task<DroppedLocationLog> PlanAsync(DroppedLocationLog log, IReadOnlyList<(LocationObj Location, IEnumerable<int> Tiles)> dropped,
                                                               IReadOnlyDictionary<long, LocationObj> section, CancellationToken token)
        {
            Dictionary<long, LocationObj> known = new(section.ToDictionary(kv => kv.Key, kv => kv.Value));
            HashSet<long> droppedIds = [.. dropped.Select(d => d.Location.ID)];

            List<long> neighbourIds = [.. dropped.SelectMany(d => d.Location.LinksCopy).Where(id => !known.ContainsKey(id)).Distinct()];
            if (neighbourIds.Count > 0)
            {
                GetByIDResult<long, LocationObj> fetched = await Store.Locations.GetObjectsByIDs(neighbourIds, token);
                foreach (LocationObj loc in fetched.Found)
                    known[loc.ID] = loc;
            }

            List<long> structureIds = [.. dropped.Select(d => d.Location.ParentID).Where(p => p.HasValue).Select(p => p.Value).Distinct()];
            Dictionary<long, StructureObj> structures = [];
            if (structureIds.Count > 0)
            {
                GetByIDResult<long, StructureObj> fetched = await Store.Structures.GetObjectsByIDs(structureIds, token);
                foreach (StructureObj s in fetched.Found)
                    structures[s.ID] = s;
            }

            IReadOnlyList<BridgedGroup> groups = LinkBridging.Bridge(
                droppedIds,
                id => known.TryGetValue(id, out LocationObj l) ? l.LinksCopy : [],
                id => known.TryGetValue(id, out LocationObj l) ? l.ParentID : null);

            Dictionary<long, int> groupOf = [];
            for (int i = 0; i < groups.Count; i++)
            {
                BridgedGroup g = groups[i];
                log.Groups.Add(new DroppedGroup
                {
                    Index = i,
                    Removed = [.. g.Removed],
                    Neighbours = [.. g.Neighbours],
                    LinksAdded = [.. g.Added.Select(p => new[] { p.A, p.B })],
                    AlreadyLinked = [.. g.AlreadyLinked.Select(p => new[] { p.A, p.B })],
                    NotLinked = [.. g.NotLinkedDifferentStructure.Select(p => new[] { p.A, p.B })],
                });
                foreach (long id in g.Removed)
                    groupOf[id] = i;
            }

            foreach ((LocationObj loc, IEnumerable<int> tiles) in dropped.OrderBy(d => d.Location.ID))
            {
                long? typeId = loc.ParentID.HasValue && structures.TryGetValue(loc.ParentID.Value, out StructureObj s) ? s.TypeID : null;
                log.Locations.Add(new DroppedLocationRecord
                {
                    Id = loc.ID,
                    StructureId = loc.ParentID,
                    StructureTypeId = typeId,
                    Section = loc.Section,
                    TypeCode = loc.TypeCode.ToString(),
                    X = loc.Position.X,
                    Y = loc.Position.Y,
                    VolumeX = loc.VolumePosition.X,
                    VolumeY = loc.VolumePosition.Y,
                    Radius = loc.Radius,
                    Width = loc.Width,
                    MosaicShapeWkt = Wkt(loc.MosaicShape),
                    VolumeShapeWkt = Wkt(loc.VolumeShape),
                    Terminal = loc.Terminal,
                    OffEdge = loc.OffEdge,
                    Untraceable = loc.Untraceable,
                    VericosityCap = loc.VericosityCap,
                    Attributes = loc.Attributes.GroupBy(a => a.Name).ToDictionary(a => a.Key, a => a.Last().Value),
                    Username = loc.Username,
                    LastModified = loc.LastModified,
                    DroppedTiles = [.. tiles.Distinct().OrderBy(t => t)],
                    LinksBefore = [.. loc.LinksCopy.OrderBy(id => id).Select(id => known.TryGetValue(id, out LocationObj peer)
                        ? new DroppedLink { LocationId = id, Section = peer.Section, StructureId = peer.ParentID }
                        : new DroppedLink { LocationId = id, NotFound = true })],
                    Group = groupOf[loc.ID],
                    Outcome = log.DryRun ? "planned" : "pending",
                });
            }

            foreach (long structureId in structureIds.OrderBy(id => id))
            {
                ICollection<LocationObj> all = await Store.Locations.GetStructureLocations(structureId, QueryTargets.Server);
                List<long> remaining = [.. all.Select(l => l.ID).Where(id => !droppedIds.Contains(id))];
                if (remaining.Count > 0)
                    continue;

                structures.TryGetValue(structureId, out StructureObj structure);
                log.StructuresLeftWithoutLocations.Add(new EmptiedStructure
                {
                    StructureId = structureId,
                    StructureTypeId = structure?.TypeID,
                    Label = structure?.Label,
                    Locations = [.. droppedIds.Where(id => all.Any(l => l.ID == id)).OrderBy(id => id)],
                });
            }

            return log;
        }

        /// <summary>
        /// Adds each group's links, then deletes its locations; a group whose link failed keeps its locations. The log is
        /// written before anything changes and again with the outcome. Returns false when a link or the save failed.
        /// </summary>
        public static async Task<bool> ApplyAsync(DroppedLocationLog log, IReadOnlyDictionary<long, LocationObj> section, string logPath)
        {
            Write(log, logPath);

            bool ok = true;
            List<long> toDelete = [];
            foreach (DroppedGroup group in log.Groups)
            {
                foreach (long[] pair in group.LinksAdded)
                {
                    try
                    {
                        await Store.LocationLinks.CreateLink(pair[0], pair[1]);
                    }
                    catch (Exception e)
                    {
                        group.LinkFailures.Add($"{pair[0]}-{pair[1]}: {e.Message}");
                    }
                }

                if (group.LinkFailures.Count > 0)
                {
                    ok = false;
                    foreach (DroppedLocationRecord r in log.Locations.Where(r => r.Group == group.Index))
                        r.Outcome = "not deleted: a replacement link failed";
                    continue;
                }

                toDelete.AddRange(group.Removed);
            }

            foreach (long id in toDelete)
            {
                if (section.TryGetValue(id, out LocationObj loc))
                    await Store.Locations.Remove(loc);
            }

            bool saved = toDelete.Count == 0 || await Store.Locations.Save();
            foreach (DroppedLocationRecord r in log.Locations.Where(r => toDelete.Contains(r.Id)))
                r.Outcome = saved ? "deleted" : "not deleted: SAVE FAILED";

            Write(log, logPath);
            return ok && saved;
        }

        public static void Write(DroppedLocationLog log, string path) =>
            File.WriteAllText(path, JsonConvert.SerializeObject(log, JsonSettings));

        /// <summary>Report lines: the dropped locations, the links added, and the structures left with no location.</summary>
        public static IEnumerable<string> ReportLines(DroppedLocationLog log, string logPath)
        {
            if (log.Locations.Count == 0)
                yield break;

            yield return string.Empty;
            yield return $"Dropped locations (wholly on tiles the new mosaic does not place; {(log.DryRun ? "would be deleted" : "deleted unless noted")}); full record: {Path.GetFullPath(logPath)}";
            foreach (DroppedLocationRecord r in log.Locations)
            {
                string links = r.LinksBefore.Count == 0 ? "no links" : "links " + string.Join(", ", r.LinksBefore.Select(l => l.NotFound ? $"{l.LocationId} (not found)" : $"{l.LocationId} (z {l.Section})"));
                yield return string.Format(CultureInfo.InvariantCulture, "  {0}: structure {1}, {2}, ({3:F1}, {4:F1}), tiles {5}; {6}; group {7}: {8}",
                    r.Id, r.StructureId?.ToString() ?? "none", r.TypeCode, r.X, r.Y, string.Join(",", r.DroppedTiles), links, r.Group, r.Outcome);
            }

            yield return string.Empty;
            yield return $"Links added in their place: {log.Groups.Sum(g => g.LinksAdded.Count)}";
            foreach (DroppedGroup g in log.Groups)
            {
                string removed = string.Join(", ", g.Removed);
                if (g.LinksAdded.Count > 0)
                    yield return $"  group {g.Index} (removes {removed}): {string.Join(", ", g.LinksAdded.Select(p => $"{p[0]}-{p[1]}"))}";
                if (g.NotLinked.Count > 0)
                    yield return $"  group {g.Index}: NOT linked (different structures or unreadable): {string.Join(", ", g.NotLinked.Select(p => $"{p[0]}-{p[1]}"))}";
                foreach (string f in g.LinkFailures)
                    yield return $"  group {g.Index}: LINK FAILED {f}";
            }

            yield return string.Empty;
            yield return $"Structures left with no location: {log.StructuresLeftWithoutLocations.Count} (not deleted)";
            foreach (EmptiedStructure s in log.StructuresLeftWithoutLocations)
                yield return $"  {s.StructureId} (type {s.StructureTypeId}{(string.IsNullOrEmpty(s.Label) ? string.Empty : ", " + s.Label)}): its locations {string.Join(", ", s.Locations)}";
        }

        private static string Wkt(IShape2D shape) =>
            shape is null ? null : LocationVolumeGeometry.ToSqlGeometry(shape).STAsText().ToSqlString().Value;
    }
}
