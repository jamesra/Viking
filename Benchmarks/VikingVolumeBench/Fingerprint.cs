using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Viking.VolumeModel;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// What the run computed, as opposed to how long it took. Two runs of the same code produce the same fingerprint; a
    /// performance change must not change it beyond the tolerances in <see cref="Compare"/>.
    /// </summary>
    internal sealed class Fingerprint
    {
        /// <summary>Key: scene ID plus mapping name. Visible tiles once every tile build has finished.</summary>
        public SortedDictionary<string, SceneTiles> Scenes { get; set; } = [];

        /// <summary>Key: <see cref="TileUniqueKey"/> text. Vertex summary for every tile any scene returned.</summary>
        public SortedDictionary<string, TileStat> Tiles { get; set; } = [];

        /// <summary>Key: section number. Probe points mapped section to volume and back.</summary>
        public SortedDictionary<string, List<Probe>> Probes { get; set; } = [];

        /// <summary>Key: section number and location ID. Result of the VikingAU mapping path for each snapshot location.</summary>
        public SortedDictionary<string, AnnotationOutcome> Annotations { get; set; } = [];

        public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, ResultsFile.JsonOptions));

        public static Fingerprint Load(string path) => JsonSerializer.Deserialize<Fingerprint>(File.ReadAllText(path), ResultsFile.JsonOptions);

        public void AddScene(string sceneKey, TilePyramid pyramid)
        {
            List<string> keys = [];
            foreach (int level in pyramid.AvailableLevels)
            {
                foreach (var pair in pyramid.GetTilesForLevel(level))
                {
                    string key = pair.Key.ToString();
                    keys.Add(key);
                    if (pair.Value is null || Tiles.ContainsKey(key))
                        continue;

                    Tiles[key] = TileStat.From(pair.Value);
                }
            }

            keys.Sort(StringComparer.Ordinal);
            Scenes[sceneKey] = new SceneTiles { Count = keys.Count, Hash = Hash(string.Join("\n", keys)) };
        }

        private static string Hash(string text)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").Substring(0, 16);
        }

        /// <summary>
        /// Compares two fingerprints. Visible tile sets and annotation outcomes must match exactly; tile vertex summaries,
        /// probe points and annotation shapes may move up to <paramref name="tolerancePixels"/>.
        /// Returns human-readable differences; empty means the fingerprints match.
        /// </summary>
        public static List<string> Compare(Fingerprint baseline, Fingerprint current, double tolerancePixels = 1.0, int maxReported = 25)
        {
            List<string> diffs = [];
            void Report(string message)
            {
                if (diffs.Count < maxReported)
                    diffs.Add(message);
                else if (diffs.Count == maxReported)
                    diffs.Add("... more differences not shown");
            }

            int sceneDiffs = 0;
            foreach (var pair in baseline.Scenes)
            {
                if (!current.Scenes.TryGetValue(pair.Key, out var c))
                    Report($"Scene {pair.Key}: missing from current run");
                else if (c.Count != pair.Value.Count || c.Hash != pair.Value.Hash)
                {
                    sceneDiffs++;
                    Report($"Scene {pair.Key}: visible tiles {pair.Value.Count} -> {c.Count} (set {(c.Hash == pair.Value.Hash ? "same" : "different")})");
                }
            }

            foreach (var pair in baseline.Tiles)
            {
                if (!current.Tiles.TryGetValue(pair.Key, out var c))
                    continue;
                string why = pair.Value.Difference(c, tolerancePixels);
                if (why != null)
                    Report($"Tile {pair.Key}: {why}");
            }

            foreach (var pair in baseline.Probes)
            {
                if (!current.Probes.TryGetValue(pair.Key, out var c) || c.Count != pair.Value.Count)
                {
                    Report($"Probes for section {pair.Key}: missing or different count");
                    continue;
                }

                for (int i = 0; i < c.Count; i++)
                {
                    string why = pair.Value[i].Difference(c[i], tolerancePixels);
                    if (why != null)
                        Report($"Probe {pair.Key}#{i}: {why}");
                }
            }

            foreach (var pair in baseline.Annotations)
            {
                if (!current.Annotations.TryGetValue(pair.Key, out var c))
                {
                    Report($"Annotation {pair.Key}: missing from current run");
                    continue;
                }
                string why = pair.Value.Difference(c, tolerancePixels);
                if (why != null)
                    Report($"Annotation {pair.Key}: {why}");
            }

            return diffs;
        }
    }

    internal sealed class SceneTiles
    {
        public int Count { get; set; }
        public string Hash { get; set; }
    }

    /// <summary>Summary of a tile's vertices. Sums catch any vertex moving; bounds catch the size of the move.</summary>
    internal sealed class TileStat
    {
        public int Vertices { get; set; }
        public int Indices { get; set; }
        public double SumX { get; set; }
        public double SumY { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public static TileStat From(TileViewModel tile)
        {
#if VIKINGBENCH_TILE_VERTICIES
            var vertices = tile.Verticies;
#else
            var vertices = tile.Vertices;
#endif
            TileStat s = new()
            {
                Vertices = vertices.Length,
                Indices = tile.TriangleIndicies?.Length ?? 0,
                MinX = double.MaxValue,
                MinY = double.MaxValue,
                MaxX = double.MinValue,
                MaxY = double.MinValue,
            };
            foreach (var v in vertices)
            {
                double x = v.Position.X, y = v.Position.Y;
                s.SumX += x;
                s.SumY += y;
                s.MinX = Math.Min(s.MinX, x);
                s.MinY = Math.Min(s.MinY, y);
                s.MaxX = Math.Max(s.MaxX, x);
                s.MaxY = Math.Max(s.MaxY, y);
            }
            return s;
        }

        public string Difference(TileStat other, double tolerance)
        {
            if (Vertices != other.Vertices || Indices != other.Indices)
                return $"vertices {Vertices}/{Indices} -> {other.Vertices}/{other.Indices}";

            double sumTolerance = tolerance * Math.Max(1, Vertices);
            if (Math.Abs(SumX - other.SumX) > sumTolerance || Math.Abs(SumY - other.SumY) > sumTolerance)
                return $"vertex sums moved by {Math.Abs(SumX - other.SumX):F3}, {Math.Abs(SumY - other.SumY):F3}";

            if (Math.Abs(MinX - other.MinX) > tolerance || Math.Abs(MinY - other.MinY) > tolerance ||
                Math.Abs(MaxX - other.MaxX) > tolerance || Math.Abs(MaxY - other.MaxY) > tolerance)
                return "vertex bounds moved more than the tolerance";

            return null;
        }
    }

    internal sealed class Probe
    {
        public double SectionX { get; set; }
        public double SectionY { get; set; }
        public bool Mapped { get; set; }
        public double VolumeX { get; set; }
        public double VolumeY { get; set; }
        public bool MappedBack { get; set; }
        public double BackX { get; set; }
        public double BackY { get; set; }

        public string Difference(Probe other, double tolerance)
        {
            if (Mapped != other.Mapped || MappedBack != other.MappedBack)
                return $"mapped {Mapped}/{MappedBack} -> {other.Mapped}/{other.MappedBack}";
            if (Mapped && (Math.Abs(VolumeX - other.VolumeX) > tolerance || Math.Abs(VolumeY - other.VolumeY) > tolerance))
                return $"volume point moved {Math.Abs(VolumeX - other.VolumeX):F3}, {Math.Abs(VolumeY - other.VolumeY):F3}";
            if (MappedBack && (Math.Abs(BackX - other.BackX) > tolerance || Math.Abs(BackY - other.BackY) > tolerance))
                return $"round-trip point moved {Math.Abs(BackX - other.BackX):F3}, {Math.Abs(BackY - other.BackY):F3}";
            return null;
        }
    }

    internal sealed class AnnotationOutcome
    {
        /// <summary>A <see cref="ReplayStatus"/> name.</summary>
        public string Status { get; set; }
        public bool DiffersFromStored { get; set; }
        public int Points { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public string Difference(AnnotationOutcome other, double tolerance)
        {
            if (Status != other.Status)
                return $"status {Status} -> {other.Status}";
            if (DiffersFromStored != other.DiffersFromStored)
                return $"differs-from-stored {DiffersFromStored} -> {other.DiffersFromStored}";
            if (Points != other.Points)
                return $"points {Points} -> {other.Points}";
            double[] a = [CenterX, CenterY, MinX, MinY, MaxX, MaxY];
            double[] b = [other.CenterX, other.CenterY, other.MinX, other.MinY, other.MaxX, other.MaxY];
            double worst = a.Zip(b, (x, y) => Math.Abs(x - y)).Max();
            return worst > tolerance ? $"shape moved {worst:F3} px" : null;
        }
    }
}
