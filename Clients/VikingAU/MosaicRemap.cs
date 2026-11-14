using CommandLine;
using Geometry;
using Geometry.Transforms;
using Microsoft.SqlServer.Types;
using Newtonsoft.Json;
using Path = System.IO.Path;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Viking.VolumeModel;
using WebAnnotationModel;
using WebAnnotationModel.Objects;

namespace Viking.AU
{
    /// <summary>Options every VikingAU verb shares: the volume and the credentials.</summary>
    class VolumeOptions
    {
        [Option('v', "VolumeURL", Required = true, HelpText = "URL of VolumeXML file")]
        public string VolumeURL { get; set; }

        [Option('u', "username", Required = false, HelpText = "Username for identity server authentication")]
        public string Username { get; set; } = "Anonymous";

        [Option('p', "password", Required = false, HelpText = "Password")]
        public string Password { get; set; } = "connectome";

        [Option("identity-server-url", Required = false, HelpText = "Identity server URL (default from config or https://identity.codepharm.net:5001/)")]
        public string IdentityServerUrl { get; set; } = null;
    }

    [Verb("remap-to-tiles", HelpText = "Step 1 of moving a section's annotations to a rebuilt mosaic: write every location's mosaic vertices as (tile, x, y) through the OLD .mosaic. Reads the server, changes nothing.")]
    class RemapToTilesOptions : VolumeOptions
    {
        [Option('s', "section", Required = true, HelpText = "Section number")]
        public int Section { get; set; }

        [Option("mosaic", Required = true, HelpText = "The OLD .mosaic file, the one the annotations were drawn on")]
        public string MosaicFile { get; set; }

        [Option('o', "output", Required = true, HelpText = "Tile-coordinate file to write (JSON)")]
        public string OutputFile { get; set; }
    }

    [Verb("remap-from-tiles", HelpText = "Step 2: map the tile coordinates written by remap-to-tiles through the NEW .mosaic and save each location's mosaic shape and volume cache.")]
    class RemapFromTilesOptions : VolumeOptions
    {
        [Option('s', "section", Required = true, HelpText = "Section number; must match the tile-coordinate file")]
        public int Section { get; set; }

        [Option("mosaic", Required = true, HelpText = "The NEW .mosaic file")]
        public string MosaicFile { get; set; }

        [Option('i', "input", Required = true, HelpText = "Tile-coordinate file written by remap-to-tiles")]
        public string InputFile { get; set; }

        [Option("report", Required = false, HelpText = "Report file (default: <input>.report.txt)")]
        public string ReportFile { get; set; }

        [Option("dry-run", Required = false, HelpText = "Map and report without saving")]
        public bool DryRun { get; set; }
    }

    /// <summary>The file remap-to-tiles writes and remap-from-tiles reads.</summary>
    sealed class TileRemapFile
    {
        public int Section { get; set; }
        public string OldMosaic { get; set; }
        public string OldMosaicSha256 { get; set; }
        public DateTime Created { get; set; }
        public List<TileRemapLocation> Locations { get; set; } = [];

        /// <summary>Locations step 1 could not express in tiles; step 2 leaves them unchanged.</summary>
        public List<SkippedLocation> Skipped { get; set; } = [];
    }

    sealed class TileRemapLocation
    {
        public long Id { get; set; }

        /// <summary>The location's LastModified when it was exported; step 2 skips a location changed since.</summary>
        public DateTime LastModified { get; set; }

        public TileShape Shape { get; set; }
    }

    sealed class SkippedLocation
    {
        public long Id { get; set; }
        public string Reason { get; set; }

        /// <summary>The mosaic vertices that caused the skip, as [x, y].</summary>
        public List<double[]> Vertices { get; set; } = [];
    }

    /// <summary>
    /// Moves a section's annotations from an old mosaic to a rebuilt one through the tiles both place (nornir's
    /// rescue-damaged-section step 7): <c>remap-to-tiles</c> writes each vertex as a tile pixel through the old mosaic,
    /// <c>remap-from-tiles</c> maps those pixels through the new mosaic and saves the location (mosaic shape, then the volume
    /// cache under the default transform, <see cref="LocationObjExtensions"/>). A location with any vertex that cannot be
    /// mapped is left unchanged and reported.
    /// </summary>
    static class MosaicRemap
    {
        // Bounds large enough to request every location on a section.
        private static readonly Geometry.Rectangle WholeVolumeBounds = new(-1e9, 1e9, -1e9, 1e9);

        /// <summary>Overlap disagreement above this many pixels is counted in the report.</summary>
        private const double DisagreementReportPixels = 5.0;

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.RoundtripKind,
        };

        private static Task<List<LocationObj>> LoadSectionAsync(int section, CancellationToken token) =>
            Store.LocationsByRegion.GetObjectsInRegionAsync(WholeVolumeBounds, 0, section, QueryTargets.Server, token, null);

        public static async Task<int> RemapToTilesAsync(RemapToTilesOptions options, CancellationToken token)
        {
            MosaicTileLocator oldMosaic = MosaicTileLocator.Load(options.MosaicFile);
            Console.WriteLine($"Old mosaic: {oldMosaic.TileCount} tiles");

            List<LocationObj> locations = await LoadSectionAsync(options.Section, token);
            TileRemapFile file = new()
            {
                Section = options.Section,
                OldMosaic = Path.GetFullPath(options.MosaicFile),
                OldMosaicSha256 = Sha256(options.MosaicFile),
                Created = DateTime.UtcNow,
            };

            int mapped = 0, extrapolated = 0;
            foreach (LocationObj loc in locations.OrderBy(l => l.ID))
            {
                if (loc.MosaicShape is null)
                {
                    file.Skipped.Add(new SkippedLocation { Id = loc.ID, Reason = "NoMosaicShape" });
                    continue;
                }

                ToTilesResult result;
                try
                {
                    result = TileRemap.ToTiles(loc.MosaicShape, oldMosaic);
                }
                catch (NotSupportedException e)
                {
                    file.Skipped.Add(new SkippedLocation { Id = loc.ID, Reason = "UnsupportedShape: " + e.Message });
                    continue;
                }

                if (result.Shape is null)
                {
                    file.Skipped.Add(new SkippedLocation
                    {
                        Id = loc.ID,
                        Reason = "NoTile",
                        Vertices = [.. result.Uncovered.Select(v => new[] { v.X, v.Y })],
                    });
                    continue;
                }

                mapped += result.MappedCount;
                extrapolated += result.ExtrapolatedCount;
                file.Locations.Add(new TileRemapLocation { Id = loc.ID, LastModified = loc.LastModified, Shape = result.Shape });
            }

            File.WriteAllText(options.OutputFile, JsonConvert.SerializeObject(file, JsonSettings));

            Console.WriteLine($"Section {options.Section}: {locations.Count} locations, {file.Locations.Count} expressed in tiles, {file.Skipped.Count} skipped.");
            Console.WriteLine($"Vertices: {mapped} mapped, {extrapolated} extrapolated (off the tile image at a warp edge).");
            foreach (SkippedLocation s in file.Skipped)
                Console.WriteLine($"  skipped {s.Id}: {s.Reason}");
            Console.WriteLine($"Wrote {options.OutputFile}");
            return 0;
        }

        public static async Task<int> RemapFromTilesAsync(RemapFromTilesOptions options, CancellationToken token)
        {
            TileRemapFile file = JsonConvert.DeserializeObject<TileRemapFile>(File.ReadAllText(options.InputFile), JsonSettings);
            if (file is null)
            {
                Console.Error.WriteLine($"{options.InputFile} holds no tile coordinates");
                return 1;
            }

            if (file.Section != options.Section)
            {
                Console.Error.WriteLine($"{options.InputFile} is for section {file.Section}, not {options.Section}");
                return 1;
            }

            MosaicTileLocator newMosaic = MosaicTileLocator.Load(options.MosaicFile);
            Console.WriteLine($"New mosaic: {newMosaic.TileCount} tiles");

            Viking.VolumeModel.Section section = State.Volume.Sections[options.Section];
            MappingBase mapper = State.MappingsManager.GetMapping(State.Volume.DefaultVolumeTransform, options.Section, section.DefaultChannel, section.DefaultPyramidTransform)
                                 ?? throw new InvalidOperationException($"No mapping found for section {options.Section}");
            await mapper.Initialize(token);

            Dictionary<long, LocationObj> current = (await LoadSectionAsync(options.Section, token)).ToDictionary(l => l.ID);

            List<(long Id, string Reason)> skipped = [.. file.Skipped.Select(s => (s.Id, "step 1: " + s.Reason))];
            List<double> shifts = [];
            List<double> disagreements = [];
            List<(LocationObj Location, SqlGeometry Before, SqlGeometry After)> changed = [];

            foreach (TileRemapLocation entry in file.Locations)
            {
                if (!current.TryGetValue(entry.Id, out LocationObj loc))
                {
                    skipped.Add((entry.Id, "Deleted since export"));
                    continue;
                }

                if (Math.Abs((loc.LastModified - entry.LastModified).TotalMilliseconds) >= 1)
                {
                    skipped.Add((entry.Id, $"Changed since export ({loc.LastModified:o})"));
                    continue;
                }

                FromTilesResult result = TileRemap.FromTiles(entry.Shape, newMosaic);
                if (!result.Succeeded)
                {
                    skipped.Add((entry.Id, result.Failure));
                    continue;
                }

                IShape2D oldShape = loc.MosaicShape;
                SqlGeometry before = LocationVolumeGeometry.ToSqlGeometry(oldShape);
                try
                {
                    if (!options.DryRun)
                    {
                        if (result.Shape is Circle circle)
                            loc.SetShapeFromCircleInSection(mapper, circle);
                        else
                            loc.SetShapeFromGeometryInSection(mapper, LocationVolumeGeometry.ToSqlGeometry(result.Shape));
                    }
                }
                catch (ArgumentException e)
                {
                    skipped.Add((entry.Id, "Not saved: " + e.Message));
                    continue;
                }

                shifts.AddRange(TileRemap.Vertices(oldShape).Zip(result.NewVertices, (a, b) => Vector2.Distance(a, b)));
                disagreements.AddRange(result.OverlapDisagreements);
                changed.Add((loc, before, LocationVolumeGeometry.ToSqlGeometry(result.Shape)));
            }

            List<string> overlaps = NewCellOverlaps(changed, current.Values);

            bool saved = true;
            if (!options.DryRun && changed.Count > 0)
                saved = await Store.Locations.Save();

            string report = BuildReport(options, file, newMosaic, changed.Count, skipped, shifts, disagreements, overlaps, saved);
            string reportPath = options.ReportFile ?? options.InputFile + ".report.txt";
            File.WriteAllText(reportPath, report);
            Console.WriteLine(report);
            Console.WriteLine($"Wrote {reportPath}");
            return saved ? 0 : 1;
        }

        /// <summary>
        /// Pairs of different cells' areas (polygons and circles of different parents) that overlap after the remap but did
        /// not before (<c>.cursor/rules/cell-boundaries-never-overlap.mdc</c>). Reported for review; the remap does not clip,
        /// since it moves stored outlines rather than drawing new ones.
        /// </summary>
        private static List<string> NewCellOverlaps(List<(LocationObj Location, SqlGeometry Before, SqlGeometry After)> changed, IEnumerable<LocationObj> section)
        {
            const double MinOverlapArea = 1.0;
            Dictionary<long, (SqlGeometry Before, SqlGeometry After)> moved = changed.ToDictionary(c => c.Location.ID, c => (c.Before, c.After));

            var areas = section.Where(l => l.ParentID.HasValue && l.MosaicShape is not null && IsArea(l.MosaicShape))
                               .Select(l =>
                               {
                                   SqlGeometry before = moved.TryGetValue(l.ID, out var m) ? m.Before : LocationVolumeGeometry.ToSqlGeometry(l.MosaicShape);
                                   SqlGeometry after = moved.TryGetValue(l.ID, out m) ? m.After : before;
                                   return (Loc: l, Before: before, After: after, Box: Envelope(after));
                               })
                               .ToList();

            RTree.RTree<int> index = new();
            for (int i = 0; i < areas.Count; i++)
                index.Add(areas[i].Box.ToRTreeRect(0), i);

            List<string> found = [];
            for (int i = 0; i < areas.Count; i++)
            {
                foreach (int j in index.Intersects(areas[i].Box.ToRTreeRect(0)))
                {
                    if (j <= i || areas[i].Loc.ParentID == areas[j].Loc.ParentID)
                        continue;
                    if (!moved.ContainsKey(areas[i].Loc.ID) && !moved.ContainsKey(areas[j].Loc.ID))
                        continue;

                    double now = OverlapArea(areas[i].After, areas[j].After);
                    if (now < MinOverlapArea)
                        continue;

                    double was = OverlapArea(areas[i].Before, areas[j].Before);
                    if (now > was + MinOverlapArea)
                        found.Add($"{areas[i].Loc.ID} (structure {areas[i].Loc.ParentID}) and {areas[j].Loc.ID} (structure {areas[j].Loc.ParentID}): {was:F0} -> {now:F0} px^2");
                }
            }

            return found;
        }

        private static bool IsArea(IShape2D shape) => shape is Circle || shape is Polygon;

        private static double OverlapArea(SqlGeometry a, SqlGeometry b)
        {
            SqlGeometry overlap = a.STIntersection(b);
            return overlap.IsNull || overlap.STArea().IsNull ? 0 : overlap.STArea().Value;
        }

        private static Geometry.Rectangle Envelope(SqlGeometry g)
        {
            SqlGeometry env = g.STEnvelope();
            double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
            for (int i = 1; i <= env.STNumPoints().Value; i++)
            {
                SqlGeometry p = env.STPointN(i);
                minX = Math.Min(minX, p.STX.Value); maxX = Math.Max(maxX, p.STX.Value);
                minY = Math.Min(minY, p.STY.Value); maxY = Math.Max(maxY, p.STY.Value);
            }

            return new Geometry.Rectangle(minX, maxX, minY, maxY);
        }

        private static string BuildReport(RemapFromTilesOptions options, TileRemapFile file, MosaicTileLocator newMosaic, int changedCount,
                                          List<(long Id, string Reason)> skipped, List<double> shifts, List<double> disagreements,
                                          List<string> overlaps, bool saved)
        {
            static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
            static double Percentile(List<double> sorted, double p) => sorted.Count == 0 ? double.NaN : sorted[(int)Math.Min(sorted.Count - 1, Math.Round(p * (sorted.Count - 1)))];

            List<double> sortedShifts = [.. shifts.OrderBy(s => s)];
            StringBuilder r = new();
            r.AppendLine($"VikingAU remap-from-tiles{(options.DryRun ? " (DRY RUN: nothing saved)" : string.Empty)}");
            r.AppendLine($"Volume: {options.VolumeURL}");
            r.AppendLine($"Section: {file.Section}");
            r.AppendLine($"Old mosaic: {file.OldMosaic} (sha256 {file.OldMosaicSha256})");
            r.AppendLine($"New mosaic: {Path.GetFullPath(options.MosaicFile)} (sha256 {Sha256(options.MosaicFile)}), {newMosaic.TileCount} tiles");
            r.AppendLine($"Tile coordinates: {Path.GetFullPath(options.InputFile)}, written {file.Created:o}");
            r.AppendLine($"Run: {DateTime.UtcNow:o}");
            r.AppendLine();
            r.AppendLine($"Locations remapped: {changedCount}{(options.DryRun ? " (would be saved)" : saved ? " (saved)" : " (SAVE FAILED)")}");
            r.AppendLine($"Locations skipped (unchanged): {skipped.Count}");
            r.AppendLine($"Vertex shift px: median {F(Percentile(sortedShifts, 0.5))}, p95 {F(Percentile(sortedShifts, 0.95))}, max {F(sortedShifts.Count == 0 ? double.NaN : sortedShifts[sortedShifts.Count - 1])} over {sortedShifts.Count} vertices");
            r.AppendLine($"Vertices in a tile overlap: {disagreements.Count}; overlapping tiles disagree by > {DisagreementReportPixels} px at {disagreements.Count(d => d > DisagreementReportPixels)}");
            r.AppendLine($"New overlaps between different cells: {overlaps.Count}");

            if (skipped.Count > 0)
            {
                r.AppendLine();
                r.AppendLine("Skipped locations (look at these by hand):");
                foreach (var (id, reason) in skipped.OrderBy(s => s.Id))
                    r.AppendLine($"  {id}: {reason}");
            }

            if (overlaps.Count > 0)
            {
                r.AppendLine();
                r.AppendLine("Different cells that now overlap (not clipped; review):");
                foreach (string o in overlaps)
                    r.AppendLine("  " + o);
            }

            return r.ToString();
        }

        private static string Sha256(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).Substring(0, 16).ToLowerInvariant();
        }
    }
}
