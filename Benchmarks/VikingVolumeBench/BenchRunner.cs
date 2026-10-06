using Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Viking.AnnotationServiceTypes.Interfaces;
using Viking.Common;
using Viking.VolumeModel;
using Path = System.IO.Path;

namespace Viking.Benchmarks.VolumeBench
{
    /// <summary>
    /// Runs phases A to F (see README.md) against the local mirror and records measurements and the fingerprint.
    /// </summary>
    /// <remarks>
    /// Work is timed one thing at a time on purpose: allocation counts are process-wide, so concurrent timed work would
    /// blur them. Background work Viking starts on its own (tile builds, cache writes) still runs concurrently, as it does
    /// in the viewer; cold scene timings wait for tile builds through <see cref="MappingBase.WhenPendingTilesComplete"/>.
    /// </remarks>
    internal sealed class BenchRunner(BenchOptions options, MirrorServer server, Recorder recorder, Fingerprint fingerprint)
    {
        private const string PhaseA = "A", PhaseB = "B", PhaseC = "C", PhaseD = "D", PhaseE = "E", PhaseF = "F";

        public static readonly IReadOnlyDictionary<string, string> PhaseNames = new Dictionary<string, string>
        {
            [PhaseA] = "Volume load",
            [PhaseB] = "Section setup",
            [PhaseC] = "Annotation mapping (VikingAU path, no save)",
            [PhaseD] = "Scenes, cold",
            [PhaseE] = "Scenes, warm",
            [PhaseF] = "Revisit after cache eviction",
        };

        private readonly BenchOptions _o = options;
        private readonly MirrorServer _server = server;
        private readonly Recorder _r = recorder;
        private readonly Fingerprint _fp = fingerprint;
        private readonly CancellationToken _token = CancellationToken.None;

        public SortedDictionary<string, long> Counts { get; } = [];

        private Volume _volume;
        private readonly HashSet<int> _mosaicChannelFallbacks = [];

        /// <summary>
        /// The pyramid channel for the warped mosaic mapping. Some sections list the configured pyramid without levels,
        /// which Viking rejects; those use the section's default pyramid, as the viewer would.
        /// </summary>
        private string MosaicChannelFor(int number)
        {
            Section section = _volume.Sections[number];
            if (section.ImagePyramids.ContainsKey(_o.MosaicChannel))
                return _o.MosaicChannel;

            if (_mosaicChannelFallbacks.Add(number))
                Count("sections.mosaicChannelFallback");
            return section.DefaultPyramid;
        }

        /// <summary>
        /// GetMapping for the warped mosaic. Returns null when the manager falls back to a tileset, so a tileset is never
        /// timed as if it were the mosaic.
        /// </summary>
        private MappingBase GetMosaicMapping(MappingManager mappings, int number)
        {
            string channel = MosaicChannelFor(number);
            if (string.IsNullOrEmpty(channel))
                return null;
            return mappings.GetMapping(_o.VolumeTransform, number, channel, _o.MosaicTransform) as FixedTileCountMapping;
        }

        /// <summary>
        /// Where <see cref="LoadStageTimings"/> events go right now: stage name to (measurement ID, description), plus the
        /// phase and instance to file them under. Set by the runner around each timed call; the runner is sequential.
        /// </summary>
        private volatile StageRoute _route;

        private sealed class StageRoute(string phase, string instance, Dictionary<string, (string Id, string Description)> map)
        {
            public readonly string Phase = phase;
            public readonly string Instance = instance;
            public readonly Dictionary<string, (string Id, string Description)> Map = map;
        }

        private static readonly Dictionary<string, (string, string)> VolumeStages = new()
        {
            [LoadStageTimings.StosZipFetch] = ("A2.StosZipFetch", "Download and extract the stos zips (per group)"),
            [LoadStageTimings.StosQueue] = ("A3.StosQueue", "Start stos loads; parsing up to the first await (per group)"),
            [LoadStageTimings.SectionQueue] = ("A4.SectionQueue", "Parse section elements (runs on the loading thread)"),
            [LoadStageTimings.SectionParse] = ("A5.SectionWait", "Wait for and register the parsed sections"),
            [LoadStageTimings.StosParseWait] = ("A6.StosParseWait", "Wait for the remaining stos parses"),
            [LoadStageTimings.CreateVolumeTransforms] = ("A7.CreateVolumeTransforms", "Build the registration tree and compose transforms"),
        };

        private static Dictionary<string, (string, string)> MappingStages(string p) => new()
        {
            [LoadStageTimings.MosaicLoad] = ($"{p}2.MosaicLoad", "Load the mosaic tile transforms"),
            [LoadStageTimings.WarpCacheRead] = ($"{p}3.WarpCacheRead", "Read warped tiles from the cache file"),
            [LoadStageTimings.Warp] = ($"{p}3.Warp", "Warp tiles into volume space"),
            [LoadStageTimings.WarpCacheWrite] = ($"{p}4.WarpCacheWrite", "Write the warped-tile cache file"),
        };

        public void OnStage(string stage, string detail, TimeSpan elapsed)
        {
            StageRoute route = _route;
            if (route is null || !route.Map.TryGetValue(stage, out var target))
                return;

            string instance = stage is LoadStageTimings.StosZipFetch or LoadStageTimings.StosQueue ? detail : route.Instance;
            _r.AddExternal(target.Id, route.Phase, target.Description, instance, elapsed);
        }

        private sealed class NullProgress : IProgress<ProgressInfo>
        {
            public void Report(ProgressInfo value) { }
        }

        /// <summary>Everything the per-section phases need for one section.</summary>
        private sealed class SectionContext
        {
            public int Number;
            public string Instance;
            public MappingBase Mosaic;
            public MappingBase Tileset;
            public Rectangle VolumeBounds;
            public List<Scene> Scenes = [];
            public List<ReplayLocation> Locations;
        }

        private readonly struct Scene(string id, int downsample, Rectangle bounds)
        {
            public readonly string Id = id;
            public readonly int Downsample = downsample;
            public readonly Rectangle Bounds = bounds;
        }

        public async Task<double> RunAsync()
        {
            Stopwatch wall = Stopwatch.StartNew();

            Volume volume = await PhaseAVolumeLoad().ConfigureAwait(false);
            _volume = volume;
            MappingManager mappings = new(volume);
            List<SectionContext> visited = [];

            foreach (int number in _o.Sections)
            {
                if (!volume.Sections.ContainsKey(number))
                {
                    Console.WriteLine($"Section {number} is not in the volume; skipped.");
                    Count("sections.missing");
                    continue;
                }

                SectionContext s = await PhaseBSectionSetup(mappings, number).ConfigureAwait(false);
                if (s is null)
                    continue;
                visited.Add(s);

                Console.WriteLine($"  Section {number}: phases C to E");
                PhaseCAnnotations(s);
                await PhaseDColdScenes(s).ConfigureAwait(false);
                await PhaseEWarmScenes(s).ConfigureAwait(false);
                RecordProbes(s);
            }

            await EvictLikeAnLru(mappings, visited).ConfigureAwait(false);
            await PhaseFRevisit(mappings).ConfigureAwait(false);

            wall.Stop();
            return wall.Elapsed.TotalMilliseconds;
        }

        private void Count(string key, long by = 1)
        {
            Counts.TryGetValue(key, out long v);
            Counts[key] = v + by;
        }

        #region Phase A

        private async Task<Volume> PhaseAVolumeLoad()
        {
            Volume volume = null;
            for (int rep = 0; rep < _o.VolumeRepetitions; rep++)
            {
                Console.WriteLine($"Phase A: volume load {rep + 1} of {_o.VolumeRepetitions}");
                volume = null;
                Mappings_Reset();
                if (!_o.Warm)
                    CacheCleaner.DeleteComputedCaches(_o.VikingCacheRoot);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                using (_r.Measure("A.Total", PhaseA, "Volume load total", "volume", MeasurementKind.Total))
                {
                    using (_r.Measure("A1.CreateAsync", PhaseA, "Fetch and parse the VikingXML (Volume.CreateAsync)", "volume"))
                        volume = await Volume.CreateAsync(_server.VolumeUrl, _o.VikingCacheRoot, new NullProgress(), _token).ConfigureAwait(false);

                    _route = new StageRoute(PhaseA, "volume", VolumeStages);
                    try
                    {
                        await volume.Initialize(_token, new NullProgress()).ConfigureAwait(false);
                    }
                    finally
                    {
                        _route = null;
                    }
                }
            }

            Counts["volume.sections"] = volume.Sections.Count;
            Counts["volume.transformGroups"] = volume.Transforms.Count;
            if (volume.Transforms.TryGetValue(_o.VolumeTransform, out var stos))
                Counts["volume.stos." + _o.VolumeTransform] = stos.Count;
            else
                throw new InvalidOperationException($"Volume has no stos group named {_o.VolumeTransform}. Groups: {string.Join(", ", volume.Transforms.Keys)}");

            return volume;
        }

        /// <summary>Tiles are cached process-wide; a new volume must not see tiles built for the previous one.</summary>
        private static void Mappings_Reset() => Viking.VolumeModel.Global.TileCache = new TileCache();

        #endregion

        #region Phase B and F

        private async Task<SectionContext> PhaseBSectionSetup(MappingManager mappings, int number)
        {
            string instance = number.ToString("D4");
            Console.WriteLine($"  Section {number}: phase B");
            SectionContext s = new() { Number = number, Instance = instance };
            long retainedBefore = GC.GetTotalMemory(forceFullCollection: true);

            using (_r.Measure("B.Total", PhaseB, "Section setup total", instance, MeasurementKind.Total))
            {
                using (_r.Measure("B1.GetMapping", PhaseB, "MappingManager.GetMapping for the mosaic and tileset mappings", instance))
                {
                    s.Mosaic = GetMosaicMapping(mappings, number);
                    s.Tileset = mappings.GetMapping(_o.VolumeTransform, number, _o.TilesetChannel, null);
                }

                await InitializeMosaic(s.Mosaic, PhaseB, "B", instance).ConfigureAwait(false);

                using (_r.Measure("B5.TilesetInit", PhaseB, "Initialize the tileset mapping", instance))
                {
                    if (s.Tileset != null)
                        await s.Tileset.Initialize(_token).ConfigureAwait(false);
                }
            }

            if (s.Mosaic is null)
                Count("sections.noMosaicMapping");
            if (s.Tileset is null)
                Count("sections.noTilesetMapping");
            if (s.Mosaic is null && s.Tileset is null)
                return null;

            Rectangle? bounds = s.Mosaic?.VolumeBounds;
            if (!bounds.HasValue && s.Tileset is TileGridToVolumeMapping tg && tg.VolumeTransform is ITransformControlPoints cp)
                bounds = cp.ControlBounds;
            bounds ??= s.Tileset?.VolumeBounds;
            if (!bounds.HasValue)
            {
                Count("sections.noBounds");
                return null;
            }

            s.VolumeBounds = bounds.Value;
            BuildScenes(s);
            await RecordRetainedMemory(s, retainedBefore).ConfigureAwait(false);
            Counts[$"section.{instance}.mosaicTiles"] = (s.Mosaic as FixedTileCountMapping)?.GetLoadedTransformsOrNull()?.Length ?? 0;
            return s;
        }

        /// <summary>
        /// Records the managed memory a set-up section keeps alive: its warped tile transforms, mosaic tiles and the volume
        /// transform's RTrees (the background prewarm is awaited first). Tiles in <c>Global.TileCache</c> are not included;
        /// that cache has its own size limit. Measured outside every timed scope because it forces full collections.
        /// </summary>
        private async Task RecordRetainedMemory(SectionContext s, long retainedBefore)
        {
            foreach (MappingBase mapping in new[] { s.Mosaic, s.Tileset })
            {
                ITransform volumeTransform = mapping switch
                {
                    SectionToVolumeMapping m => m.VolumeTransform,
                    TileGridToVolumeMapping m => m.VolumeTransform,
                    _ => null,
                };
                if (volumeTransform is ISpatialIndexPrewarm prewarm)
                    await prewarm.PrewarmSpatialIndexAsync().ConfigureAwait(false);
            }

            long retained = GC.GetTotalMemory(forceFullCollection: true) - retainedBefore;
            Counts[$"section.{s.Instance}.retainedKB"] = retained / 1024;
        }

        /// <summary>
        /// Initializes the warped mosaic mapping. For a warped section the steps are timed by stage events; the reference
        /// section has no volume transform, so its whole initialization is the mosaic load.
        /// </summary>
        private async Task InitializeMosaic(MappingBase mosaic, string phase, string prefix, string instance)
        {
            if (mosaic is null)
                return;

            if (mosaic is SectionToVolumeMapping)
            {
                _route = new StageRoute(phase, instance, MappingStages(prefix));
                try
                {
                    await mosaic.Initialize(_token).ConfigureAwait(false);
                }
                finally
                {
                    _route = null;
                }
            }
            else
            {
                using (_r.Measure($"{prefix}2.MosaicLoad", phase, "Load the mosaic tile transforms", instance))
                    await mosaic.Initialize(_token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Evicts every visited section except the most recent <c>NumSectionsToKeepInMemory</c>, the way the section
        /// mapping cache is meant to.
        /// </summary>
        /// <remarks>
        /// In the viewer the cache never actually evicts: <c>TimeQueueCache</c> only removes entries that missed a
        /// <c>Checkpoint()</c>, nothing calls <c>Checkpoint()</c> on <c>SectionTransformsCache</c>, and every
        /// <c>GetMapping</c> marks its section used. Without this step phase F would only measure cache hits. Eviction here
        /// mirrors <c>SectionMappingsCacheEntry.Dispose</c>: drop the entry and free each mapping's memory.
        /// </remarks>
        private async Task EvictLikeAnLru(MappingManager mappings, List<SectionContext> visited)
        {
            int keep = (int)mappings.SectionMappingCache.NumSectionsToKeepInMemory;
            int evicted = 0;
            foreach (SectionContext s in visited.Take(Math.Max(0, visited.Count - keep)))
            {
                mappings.SectionMappingCache.Remove(s.Number);
                if (s.Mosaic != null)
                    await s.Mosaic.FreeMemory().ConfigureAwait(false);
                if (s.Tileset != null)
                    await s.Tileset.FreeMemory().ConfigureAwait(false);
                evicted++;
            }

            Counts["F.sectionsEvicted"] = evicted;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private async Task PhaseFRevisit(MappingManager mappings)
        {
            Console.WriteLine("Phase F: revisit");
            int rebuilt = 0;
            foreach (int number in _o.Sections)
            {
                string instance = number.ToString("D4");
                using (_r.Measure("F.Total", PhaseF, "Revisit total", instance, MeasurementKind.Total))
                {
                    MappingBase mosaic, tileset;
                    using (_r.Measure("F1.GetMapping", PhaseF, "MappingManager.GetMapping for the mosaic and tileset mappings", instance))
                    {
                        mosaic = GetMosaicMapping(mappings, number);
                        tileset = mappings.GetMapping(_o.VolumeTransform, number, _o.TilesetChannel, null);
                    }

                    if (mosaic is null && tileset is null)
                        continue;

                    if (mosaic != null && !mosaic.Initialized)
                        rebuilt++;

                    await InitializeMosaic(mosaic, PhaseF, "F", instance).ConfigureAwait(false);

                    Rectangle? bounds = mosaic?.VolumeBounds ?? tileset?.VolumeBounds;
                    if (!bounds.HasValue)
                        continue;

                    Rectangle view = ViewAt(bounds.Value.Center, 4);
                    Viking.VolumeModel.Global.TileCache = new TileCache();
                    using (_r.Measure("F5.Scene", PhaseF, "One cold scene at downsample 4", instance))
                    {
                        await VisibleTilesCold(mosaic, view, 4).ConfigureAwait(false);
                        await VisibleTilesCold(tileset, view, 4).ConfigureAwait(false);
                    }
                }
            }

            Counts["F.sectionsRebuilt"] = rebuilt;
        }

        #endregion

        #region Phase C

        private void PhaseCAnnotations(SectionContext s)
        {
            SectionSnapshot snapshot = SectionSnapshot.TryLoad(_o, s.Number);
            if (snapshot is null)
            {
                Count("annotations.sectionsWithoutSnapshot");
                return;
            }

            MappingBase mapper = s.Tileset ?? s.Mosaic;
            using (_r.Measure("C.Total", PhaseC, "Annotation mapping total", s.Instance, MeasurementKind.Total))
            {
                using (_r.Measure("C1.Parse", PhaseC, "Read the snapshot and parse WKB into SqlGeometry", s.Instance))
                {
                    snapshot = SectionSnapshot.TryLoad(_o, s.Number);
                    s.Locations = [.. snapshot.Locations.Select(AnnotationReplay.Parse)];
                }

                ReplayResult[] results = [.. s.Locations.Select(_ => new ReplayResult())];
                RunReplay(s.Locations, results, mapper, PhaseC, "C", s.Instance, byType: true);

                foreach (var group in results.GroupBy(r => r.Status))
                    Count($"annotations.{group.Key}", group.Count());
                Count("annotations.locations", results.Length);
                Count("annotations.differsFromStored", results.Count(r => r.Status == ReplayStatus.Ok && r.DiffersFromStored));

                for (int i = 0; i < results.Length; i++)
                    _fp.Annotations[$"{s.Instance}:{s.Locations[i].Source.Id}"] = AnnotationReplay.ToOutcome(results[i]);
            }
        }

        /// <summary>
        /// Runs the map, smooth and check passes over <paramref name="locations"/>, timing each pass as one step. With
        /// <paramref name="byType"/>, also accumulates map and smooth time per location type as breakdowns.
        /// </summary>
        private void RunReplay(IReadOnlyList<ReplayLocation> locations, ReplayResult[] results, MappingBase mapper,
            string phase, string prefix, string instance, bool byType)
        {
            Dictionary<LocationType, long> mapTicks = [], smoothTicks = [];

            using (_r.Measure($"{prefix}2.Map", phase, "TryMapShapeSectionToVolume", instance))
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    AnnotationReplay.Map(mapper, locations[i], results[i]);
                    if (byType)
                        Accumulate(mapTicks, locations[i].Type, Stopwatch.GetTimestamp() - start);
                }
            }

            using (_r.Measure($"{prefix}3.Smooth", phase, "GetSmoothedShape", instance))
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    long start = Stopwatch.GetTimestamp();
                    AnnotationReplay.Smooth(locations[i], results[i]);
                    if (byType)
                        Accumulate(smoothTicks, locations[i].Type, Stopwatch.GetTimestamp() - start);
                }
            }

            using (_r.Measure($"{prefix}4.Check", phase, "STIsValid and compare with the stored volume shape", instance))
            {
                for (int i = 0; i < locations.Count; i++)
                    AnnotationReplay.Check(locations[i], results[i]);
            }

            if (!byType)
                return;

            foreach (var pair in mapTicks)
                _r.AddAccumulated($"{prefix}2.Map[{pair.Key}]", phase, $"Map, {pair.Key} only", instance, pair.Value, MeasurementKind.Breakdown);
            foreach (var pair in smoothTicks)
                _r.AddAccumulated($"{prefix}3.Smooth[{pair.Key}]", phase, $"Smooth, {pair.Key} only", instance, pair.Value, MeasurementKind.Breakdown);
        }

        private static void Accumulate(Dictionary<LocationType, long> ticks, LocationType type, long elapsed)
        {
            ticks.TryGetValue(type, out long v);
            ticks[type] = v + elapsed;
        }

        #endregion

        #region Phases D and E

        private void BuildScenes(SectionContext s)
        {
            Rectangle b = s.VolumeBounds;
            foreach (int ds in _o.Downsamples)
            {
                for (int p = 0; p < _o.PositionsPerLevel; p++)
                {
                    Random random = new(unchecked((_o.Seed * 31) + (s.Number * 1009) + (ds * 9176) + p));
                    Vector2 center = new(b.Left + (random.NextDouble() * b.Width), b.Bottom + (random.NextDouble() * b.Height));
                    s.Scenes.Add(new Scene($"{s.Instance}:ds{ds:D3}:p{p}", ds, ViewAt(center, ds)));
                }
            }
        }

        private Rectangle ViewAt(Vector2 center, double downsample)
        {
            double halfWidth = _o.ViewportWidth * downsample / 2.0;
            double halfHeight = _o.ViewportHeight * downsample / 2.0;
            return new Rectangle(center.X - halfWidth, center.X + halfWidth, center.Y - halfHeight, center.Y + halfHeight);
        }

        /// <summary>One cold <c>VisibleTiles</c> call: request the tiles and wait until every tile build it started has finished.</summary>
        private static async Task VisibleTilesCold(MappingBase mapping, Rectangle view, double downsample)
        {
            if (mapping is null)
                return;
            mapping.VisibleTiles(view, downsample);
            await mapping.WhenPendingTilesComplete().ConfigureAwait(false);
        }

        private List<int> SelectAnnotations(SectionContext s, Scene scene)
        {
            List<int> selected = [];
            if (s.Locations is null)
                return selected;

            for (int i = 0; i < s.Locations.Count; i++)
            {
                ReplayLocation loc = s.Locations[i];
                if (loc.StoredVolumeBounds.HasValue && loc.Radius >= scene.Downsample && scene.Bounds.Intersects(loc.StoredVolumeBounds.Value))
                    selected.Add(i);
            }

            return selected;
        }

        private async Task PhaseDColdScenes(SectionContext s)
        {
            for (int rep = 0; rep < _o.SceneRepetitions; rep++)
            {
                foreach (Scene scene in s.Scenes)
                {
                    Viking.VolumeModel.Global.TileCache = new TileCache();
                    string level = $"@ds{scene.Downsample:D3}";

                    using (_r.Measure("D.Total", PhaseD, "Cold scenes total", scene.Id, MeasurementKind.Total))
                    {
                        using (_r.Measure("D1.MosaicTiles", PhaseD, "Warped mosaic: VisibleTiles until tile builds finish", scene.Id))
                        using (_r.Measure("D1.MosaicTiles" + level, PhaseD, $"Warped mosaic tiles at downsample {scene.Downsample}", scene.Id, MeasurementKind.Breakdown))
                            await VisibleTilesCold(s.Mosaic, scene.Bounds, scene.Downsample).ConfigureAwait(false);

                        using (_r.Measure("D1.TilesetTiles", PhaseD, "Tileset: VisibleTiles until tile builds finish", scene.Id))
                        using (_r.Measure("D1.TilesetTiles" + level, PhaseD, $"Tileset tiles at downsample {scene.Downsample}", scene.Id, MeasurementKind.Breakdown))
                            await VisibleTilesCold(s.Tileset, scene.Bounds, scene.Downsample).ConfigureAwait(false);

                        List<int> selected;
                        using (_r.Measure("D2.SelectAnnotations", PhaseD, "Pick the scene's annotations", scene.Id))
                            selected = SelectAnnotations(s, scene);

                        if (s.Locations != null)
                        {
                            using (_r.Measure("D3.MapAnnotations", PhaseD, "Map, smooth and check the scene's annotations", scene.Id))
                            {
                                ReplayLocation[] locs = [.. selected.Select(i => s.Locations[i])];
                                ReplayResult[] results = [.. locs.Select(_ => new ReplayResult())];
                                MappingBase mapper = s.Tileset ?? s.Mosaic;
                                for (int i = 0; i < locs.Length; i++)
                                {
                                    AnnotationReplay.Map(mapper, locs[i], results[i]);
                                    AnnotationReplay.Smooth(locs[i], results[i]);
                                    AnnotationReplay.Check(locs[i], results[i]);
                                }
                            }
                        }
                    }

                    if (rep == 0)
                        RecordSceneFingerprint(s, scene);
                }
            }
        }

        private void RecordSceneFingerprint(SectionContext s, Scene scene)
        {
            if (s.Mosaic != null)
                _fp.AddScene(scene.Id + ":mosaic", s.Mosaic.VisibleTiles(scene.Bounds, scene.Downsample));
            if (s.Tileset != null)
                _fp.AddScene(scene.Id + ":tileset", s.Tileset.VisibleTiles(scene.Bounds, scene.Downsample));

            s.Mosaic?.WhenPendingTilesComplete().GetAwaiter().GetResult();
            s.Tileset?.WhenPendingTilesComplete().GetAwaiter().GetResult();
        }

        private async Task PhaseEWarmScenes(SectionContext s)
        {
            Viking.VolumeModel.Global.TileCache = new TileCache();
            foreach (Scene scene in s.Scenes)
            {
                await VisibleTilesCold(s.Mosaic, scene.Bounds, scene.Downsample).ConfigureAwait(false);
                await VisibleTilesCold(s.Tileset, scene.Bounds, scene.Downsample).ConfigureAwait(false);
            }

            for (int rep = 0; rep < _o.SceneRepetitions; rep++)
            {
                foreach (Scene scene in s.Scenes)
                {
                    using (_r.Measure("E.Total", PhaseE, "Warm scenes and pan total", scene.Id, MeasurementKind.Total))
                    {
                        using (_r.Measure("E1.MosaicTiles", PhaseE, "Warped mosaic: VisibleTiles, every tile cached", scene.Id))
                            s.Mosaic?.VisibleTiles(scene.Bounds, scene.Downsample);

                        using (_r.Measure("E1.TilesetTiles", PhaseE, "Tileset: VisibleTiles, every tile cached", scene.Id))
                            s.Tileset?.VisibleTiles(scene.Bounds, scene.Downsample);

                        using (_r.Measure("E2.SelectAnnotations", PhaseE, "Pick the scene's annotations", scene.Id))
                            SelectAnnotations(s, scene);
                    }
                }
            }

            Rectangle[] frames = PanFrames(s.VolumeBounds, 4);
            foreach (Rectangle frame in frames)
            {
                await VisibleTilesCold(s.Mosaic, frame, 4).ConfigureAwait(false);
                await VisibleTilesCold(s.Tileset, frame, 4).ConfigureAwait(false);
            }

            Measurement panTotal = _r.Get("E.Total", PhaseE, "Warm scenes and pan total", MeasurementKind.Total);
            Measurement pan = _r.Get("E3.Pan", PhaseE, $"Pan sequence, {frames.Length} frames at downsample 4");
            Measurement frameStats = _r.Get("E3.PanFrame", PhaseE, "One pan frame (median and 95th percentile over every frame of every section)", MeasurementKind.Breakdown);

            long panStart = Stopwatch.GetTimestamp();
            long allocStart = Recorder.AllocatedBytesNow();
            int g0 = GC.CollectionCount(0), g1 = GC.CollectionCount(1), g2 = GC.CollectionCount(2);
            foreach (Rectangle frame in frames)
            {
                long frameStart = Stopwatch.GetTimestamp();
                s.Mosaic?.VisibleTiles(frame, 4);
                s.Tileset?.VisibleTiles(frame, 4);
                frameStats.Add("all", new Sample((Stopwatch.GetTimestamp() - frameStart) * 1000.0 / Stopwatch.Frequency, -1, 0, 0, 0));
            }

            Sample panSample = new((Stopwatch.GetTimestamp() - panStart) * 1000.0 / Stopwatch.Frequency,
                Recorder.AllocatedBytesNow() - allocStart,
                GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
            pan.Add(s.Instance, panSample);
            panTotal.Add("pan:" + s.Instance, panSample);
        }

        /// <summary>Frames that move a tenth of the viewport per frame, bouncing between the section's left and right edges.</summary>
        private Rectangle[] PanFrames(Rectangle bounds, double downsample)
        {
            double viewWidth = _o.ViewportWidth * downsample;
            double step = viewWidth * 0.1;
            double travel = Math.Max(step, bounds.Width - viewWidth);
            Rectangle[] frames = new Rectangle[_o.PanFrames];
            for (int i = 0; i < frames.Length; i++)
            {
                double distance = (i * step) % (2 * travel);
                double offset = distance <= travel ? distance : (2 * travel) - distance;
                Vector2 center = new(bounds.Left + (viewWidth / 2.0) + offset, bounds.Center.Y);
                frames[i] = ViewAt(center, downsample);
            }
            return frames;
        }

        #endregion

        /// <summary>
        /// Maps a 9 by 9 grid of section points to volume space and back with the mapping VikingAU uses. Runs after the
        /// timed phases for the section, so the inverse lookups it triggers do not warm anything a later measurement uses.
        /// </summary>
        private void RecordProbes(SectionContext s)
        {
            MappingBase mapper = s.Tileset ?? s.Mosaic;
            Rectangle? sectionBounds = s.Mosaic?.SectionBounds ?? s.Tileset?.SectionBounds;
            if (mapper is null || !sectionBounds.HasValue)
                return;

            Rectangle b = sectionBounds.Value;
            const int n = 9;
            Vector2[] points = new Vector2[n * n];
            for (int iy = 0; iy < n; iy++)
                for (int ix = 0; ix < n; ix++)
                    points[(iy * n) + ix] = new Vector2(b.Left + (b.Width * (0.05 + (0.9 * ix / (n - 1)))), b.Bottom + (b.Height * (0.05 + (0.9 * iy / (n - 1)))));

            bool[] mapped = mapper.TrySectionToVolume(points, out Vector2[] volume);
            bool[] back = mapper.TryVolumeToSection(volume, out Vector2[] roundTrip);

            List<Probe> probes = new(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                probes.Add(new Probe
                {
                    SectionX = points[i].X,
                    SectionY = points[i].Y,
                    Mapped = mapped[i],
                    VolumeX = mapped[i] ? volume[i].X : 0,
                    VolumeY = mapped[i] ? volume[i].Y : 0,
                    MappedBack = mapped[i] && back[i],
                    BackX = mapped[i] && back[i] ? roundTrip[i].X : 0,
                    BackY = mapped[i] && back[i] ? roundTrip[i].Y : 0,
                });
            }

            _fp.Probes[s.Instance] = probes;
        }
    }

    /// <summary>
    /// Deletes the caches Viking computes from downloaded files, so a cold run does the computation again. Downloaded
    /// files (extracted stos zips) are kept: Viking keeps them between sessions too.
    /// </summary>
    internal static class CacheCleaner
    {
        public static void DeleteComputedCaches(string vikingCacheRoot)
        {
            if (!Directory.Exists(vikingCacheRoot))
                return;

            foreach (string volumeDir in Directory.GetDirectories(vikingCacheRoot))
            {
                foreach (string file in Directory.GetFiles(volumeDir, "*.cache"))
                    TryDelete(file);

                string stosDir = Path.Combine(volumeDir, "Stos");
                if (Directory.Exists(stosDir))
                {
                    foreach (string file in Directory.GetFiles(stosDir))
                        TryDelete(file);
                }
            }
        }

        /// <summary>Viking writes some caches from fire-and-forget tasks, so a file may still be open briefly.</summary>
        private static void TryDelete(string file)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    File.Delete(file);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(50);
                }
            }
            throw new IOException($"Could not delete cache file {file}");
        }
    }
}
