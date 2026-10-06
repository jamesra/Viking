# VikingVolumeBench

Headless end-to-end benchmark for Viking's transform and geometry code. It loads a real volume (RC2 by default) through `Viking.VolumeModel` the way the viewer and VikingAU do, sets up sections, requests visible tiles for synthetic scenes, and replays the VikingAU annotation mapping path. It never downloads tile images, never opens a window, and never writes to the annotation service.

`GeometryBenchmarks` (next to this folder) holds BenchmarkDotNet micro-benchmarks that load the same RC2 files.

## Quick start

```powershell
dotnet build Benchmarks\VikingVolumeBench\VikingVolumeBench.csproj -c Release
$bench = "Benchmarks\VikingVolumeBench\bin\Release\net48\VikingVolumeBench.exe"

# Once: mirror the transforms and save a read-only annotation snapshot (needs a login, see below)
$env:VIKINGBENCH_USER = "..."; $env:VIKINGBENCH_PASSWORD = "..."
& $bench prime

# Record a baseline, change code, then compare
& $bench run --label baseline --out "$env:LOCALAPPDATA\VikingVolumeBench\results\baseline.json"
& $bench run --label my-change --baseline "$env:LOCALAPPDATA\VikingVolumeBench\results\baseline.json"

# Compare two existing results files
& $bench compare --baseline a.json --current b.json
```

`--quick` runs a small configuration (3 sections, 2 levels, 1 repetition) for smoke tests. `run` exits with 1 when the fingerprint differs from `--baseline`.

Run in Release, on AC power, with nothing else busy. A full RC2 run takes about 4 minutes before the transform optimizations, most of it in section warping.

## How it avoids the network

`prime` downloads the VikingXML, both stos zips and the `.mosaic` files for the benchmark sections into `<root>\mirror`, setting each file's time to the server's `Last-Modified`. `run` serves that mirror from `http://localhost:<port>` and rewrites the VikingXML's `path` and `host` attributes to point at it, so Viking's own loading code (zip download, mosaic GET, cache validity checks) runs unchanged. Paths the mirror lacks are listed as `MirrorMisses` in the results; rerun `prime` with those sections.

Viking's cache for the run lives in `<root>\viking-cache`, never the user's cache. By default (`--cold`) each volume load first deletes the caches Viking computes (`*.cache` and `Stos\*` in the volume folder) and keeps the extracted stos zips, which Viking also keeps between sessions. `--warm` keeps everything.

## Annotations

`prime` reads every location on the benchmark sections with `LocationStore.GetObjectsForSection` and saves them to `<root>\annotations\<section>.json` (IDs, type, width, mosaic and volume shapes as WKB). It only reads. The login comes from `VIKINGBENCH_USER` and `VIKINGBENCH_PASSWORD` and is never written anywhere; without them it tries the anonymous login, which RC2 refuses. Without a snapshot, phase C and the annotation steps of D and E record zero.

## Phases and measurements

Every measurement records wall time, allocated bytes (process-wide, `AppDomain.MonitoringTotalAllocatedMemorySize`) and GC counts. Samples are grouped by instance (a section, a scene, a stos group); the reported value is the sum over instances of each instance's median, so totals add up. Steps marked "stage" are timed inside Viking by `Viking.VolumeModel.LoadStageTimings` and have no allocation figure. Each phase also prints `(unattributed)`: its total minus its steps.

| Phase | ID | What it times |
|---|---|---|
| A, volume load (3 repetitions) | A1.CreateAsync | Fetch and parse the VikingXML |
| | A2.StosZipFetch | Download and extract each stos zip (stage, per group) |
| | A3.StosQueue | Start the stos loads; parsing up to each load's first await (stage, per group) |
| | A4.SectionQueue | Parse section elements; runs on the loading thread (stage) |
| | A5.SectionWait | Wait for and register sections (stage) |
| | A6.StosParseWait | Wait for the remaining stos parses (stage) |
| | A7.CreateVolumeTransforms | Registration tree and slice-to-volume composition (stage) |
| B, section setup (per section) | B1.GetMapping | `MappingManager.GetMapping` for the warped mosaic and the tileset |
| | B2.MosaicLoad | Load the mosaic tile transforms (stage; timed directly for the reference section) |
| | B3.WarpCacheRead / B3.Warp | Read warped tiles from cache, or warp them (`TriangulationTransform.Transform`) (stage) |
| | B4.WarpCacheWrite | Write the warped-tile cache (stage) |
| | B5.TilesetInit | Initialize the tileset mapping |
| C, annotation mapping (per section) | C1.Parse | Read the snapshot, parse WKB |
| | C2.Map | `TryMapShapeSectionToVolume`; `C2.Map[TYPE]` breaks it down by location type |
| | C3.Smooth | `GetSmoothedShape`; `C3.Smooth[TYPE]` by type |
| | C4.Check | `STIsValid` and the VikingAU difference test against the stored volume shape |
| D, cold scenes (per scene, 5 repetitions) | D1.MosaicTiles / D1.TilesetTiles | `VisibleTiles` until every tile build it started finishes; `@ds###` by downsample |
| | D2.SelectAnnotations | Locations whose stored volume box meets the view and whose radius is at least one screen pixel |
| | D3.MapAnnotations | Map, smooth and check those locations |
| E, warm scenes | E1.MosaicTiles / E1.TilesetTiles | `VisibleTiles` with every tile cached (the per-frame cost) |
| | E2.SelectAnnotations | Annotation selection |
| | E3.Pan / E3.PanFrame | 120-frame pan at downsample 4: total per section, and per-frame median and 95th percentile |
| F, revisit | F1.GetMapping, F2-F4 | Sections evicted from the mapping cache are set up again (from the warped-tile cache) |
| | F5.Scene | One cold scene at downsample 4 |

Scenes: for each section and each downsample (1 to 128), 8 seeded positions inside the section's volume bounds, with a 1920 by 1080 viewport.

Phase F evicts every section except the 6 most recently visited before revisiting. The viewer's `SectionTransformsCache` never evicts on its own: `TimeQueueCache` only removes entries that missed a `Checkpoint()`, and nothing calls `Checkpoint()` on it.

## Fingerprint

Next to each results file, `<name>.fingerprint.json` records what the run computed:

- every scene's visible tile set (count and hash; must match exactly)
- a vertex summary of every tile (count, sums and bounds; may move up to 1 pixel)
- 81 probe points per section mapped section to volume and back (may move up to 1 pixel)
- every snapshot location's outcome and shape summary (status must match; shape may move up to 1 pixel)

A performance change must leave the fingerprint matching. A deliberate behaviour change (for example the switch to double-precision RBF) is reviewed once and becomes the new baseline.

## Default sections

645 (the reference section, no warp), 1, 100, 300, 500, 643, 646, 800, 1000, 1200, 1454. That is more than the 6 sections the mapping cache keeps. Section 1 lists `TEM.Leveled.Pyramid` without levels, so like the viewer it uses its default pyramid (counted as `sections.mosaicChannelFallback`).
