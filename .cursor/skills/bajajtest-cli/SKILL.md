---
name: bajajtest-cli
description: Dump and review MonogameTestbed BAJAJTEST screenshots via CLI to debug Bajaj slice-mesh generation. Use when investigating mesh tiling errors, ReproSet cases, --screenshots, --repro, --capture-request, BajajTest, or visual OTV/region/chord failures.
---

# BAJAJTEST CLI — debug slice mesh generation

`BajajAssignmentTest` (`TestMode.BAJAJTEST`) meshes **one slice pair** from `ReproSet` in `Clients/MonogameTestbed/BajajTest.cs`. It is a visual debugger, not an automated pass/fail test. Use it to inspect Delaunay, OTV chords, incomplete vertices, and per-stage meshes.

For algorithm/paper context see [contours-to-mesh](../contours-to-mesh/SKILL.md). For legend colors, gamepad, and stage names see [reference.md](reference.md).

**Not this skill:** whole-cell export is `BAJAJMULTITEST` (`-s` structure IDs, `-e` endpoint, `-o` DAE folder). Child structures are included by default; pass `--xc` / `--exclude-children` to mesh only the IDs on `-s`.

### Whole-cell performance benchmark (BAJAJMULTITEST)

Prefer a **Release** x64 build for timing (`bin/x64/Release/net9.0-windows`). Structure **410** on RC1 (GAC Aii, ~3000 locations with polygons) is the standard benchmark; 476 is mostly circles and is a poor mesh-load stand-in. The Release `MonogameTestbed` project uses **Server GC** (`ServerGarbageCollection=true`); that setting is part of the measured configuration, not an optional extra.

```text
dotnet build Clients\MonogameTestbed\MonogameTestbed.csproj -c Release -p:Platform=x64
cd Clients\MonogameTestbed\bin\x64\Release\net9.0-windows
dotnet exec .\MonogameTestbed.dll --mode BajajMultiTest -s 410 -e http://websvc.codepharm.net/RC1/OData -o C:\Temp\Perf\RC1_410 -q -l --invert-z --timings
```

`--timings` prints:

- Per-phase seconds, call counts, us/call, us/item, **max(s)** (longest single call — a long-pole slice), and **x-wall** (phase-seconds ÷ run wall clock). `x-wall` keeps counting through GC pauses; for core utilization trust the 5-second process sampler, not x-wall.
- Face-generation sub-phases: `Delaunay`, `RegionGraphBuild`, `RegionClosing`, `ChordGeneration`, `FaceClosing`, `SecondPassRegionDetection`, plus `FaceSlotWait` (time parked for a generation permit) and `peakSlicesQueuedForSlot`.
- Process peaks: threads, working set, GC gen0/1/2 counts, GC pause.
- **System RAM**: total, free at start, min free during the run. Two runs of identical code are not comparable if one was memory-starved.
- **System CPU busy %** at `pre-OData`, `post-OData`, `mesh complete`, and `end` (whole machine, to catch a Nornir build or similar competing for cores).
- **Process core usage** sampled every 5s from OData-complete through mesh-complete (`coresUsed`, system free MB, process working set). The last ~20s collapsing to 1–3 cores is the long-pole slice, not a missing parallelism budget.
- **slow FaceGeneration** lines (`>10s`) with slice key, location IDs, vertex/face counts, and chord-pass stats. Replay those IDs with `--repro-locations`.
  The slices that own the run's serial tail are large contours (RC1 410: `200625,200626,201467,1420185`, ~2500 verts / ~50s; RPC1 2628: `98942,98943,101061,101147,101150,101220,101221`, ~4700 verts / ~5 min), not degenerate Delaunay retries. `chordPassCalls` in the teens–thirties is normal; a pass count in the hundreds would mean the while-loop is stuck adding one chord at a time. The next algorithmic lever is incremental OTV (`FindOptimalTilingForVertexByDistance` / `FindNearestPoints`), not more `faceSlots`.

Do not use Debug + `-v` for wall-clock comparisons: Debug asserts and console Trace listeners dominate. Do not raise `MeshParallelism.DegreeOfParallelism` while `peakSlicesQueuedForSlot` already exceeds `faceSlots`; that means work is queued, not that the machine is idle.

## Constraints

- DesktopGL needs a real GPU window (not headless).
- Repro cases fetch live OData (`Endpoint.TEST` or `RPC1`). Failures write `error.txt` under the case folder.
- Stop a running testbed debug session before rebuilding; output DLLs lock.

## CLI

Binary: `Clients/MonogameTestbed/bin/x64/Debug/net9.0-windows/MonogameTestbed.dll`  
cwd: that same folder.

| Flag | Purpose |
|------|---------|
| `--mode BajajTest` | Start in BAJAJTEST (enum parse is case-insensitive) |
| `--screenshots` | Fullscreen at native monitor resolution, then dump PNGs + `manifest.json` |
| `--repro N` | `ReproSet` index; also `0-3`, `1,5,7`, or `all` |
| `--repro-locations A[,B,C…]` | Mesh the slice spanning these **LocationIDs** without touching `ReproSet`. Needs `-e`. Appended after the repro set and auto-selected when `--repro` is absent. A single ID is an isolated annotation: its slice holds that one contour and only the cap stage runs (shown as the upward cap) |
| `--repro-locations-file path` | One slice per line (`A,B[,C…]` or space separated); every line becomes an ad-hoc case. This is how a BAJAJMULTITEST failure list is replayed in batch |
| `--repro-tolerance T` | Simplification tolerance for `--repro-locations` (default 1.0) |
| `--3d` | Also capture the shaded 3D mesh renders. Off by default: they are the slowest captures to learn with and carry no chord information. Add it only for final verification of a fixed slice. A capture request that names a `"view": "3d"` shot explicitly is always honoured |
| `--capture-request path.json` | Replace the default shot list and/or override `--repro` |
| `--cameras a,b,…` | 3D camera presets (`top`, `oblique`, `oblique-back`, `side`, `front`, `below`) applied to every 3D shot; one PNG per camera. See "3D cameras" |
| `--display N` \| `primary` | Monitor to capture on. Defaults to a secondary monitor when one is attached, so a capture does not take over the operator's screen |
| `--list-displays` | Print the attached monitors with their indices and exit |
| `-o dir` | Output root; screenshots go to `{dir}/BajajTest/` |
| `-q` | Exit after the last PNG |
| `-v` | Trace + ILogger to console |
| `-l` | Trace to a log file under `{dir}/Logs` (or cwd) |

Without `--screenshots`, `--repro N` still selects the interactive case (default index **5**). `-v` and `-l` can be combined.

The process opts into per-monitor DPI awareness in `Program.Main`. Without it Windows virtualizes every size it reports, so a 3840x2160 display at 150% scaling claims to be 2560x1440 and captures came out at that reduced size. Because Windows then stops stretching fixed pixel sizes, the interactive window multiplies `desired_screen_width`/`_height` by `MonoTestbed.ScaleForDisplayDpi` to keep its former apparent size.

Capture goes borderless fullscreen, which takes over a whole monitor, so it picks a non-primary one when the machine has more than one display. Only `--screenshots` relocates the window; an interactive session stays where it was opened. With `-v` the run logs `Capturing on the display at (x,y)`.

**Run via `dotnet exec MonogameTestbed.dll`, not the `.exe`.** The `.exe` is a windowed binary whose stdout is not attached to the console, so redirecting its output yields an empty file and you lose every trace message.

### Inspect an arbitrary slice found by a diagnostic

A diagnostic that reports a bad slice prints its LocationIDs; feed them straight in. No code edit, no rebuild.

BAJAJMULTITEST writes its failures into the `-o` folder as `bajajmultitest_failed_slices_<MM.dd.yyyy_HH.mm.ss>.txt` (same stamp as the `Logs/*.log` for that run), appending each slice as it fails so a session stopped mid-run still leaves a list. On completion it also writes a sorted `bajajmultitest_failed_slices.txt`; diff two of those to see whether a change added or removed failures. Either file goes straight to `--repro-locations-file`.

```text
dotnet exec MonogameTestbed.dll --mode BajajTest --screenshots --repro-locations 8614,8616 -e RC1 -o C:\Temp\BajajTestScreenshots -q -v
```

The case appears as `case-NN-ad-hoc-8614-8616`. The same slices can be listed in a capture request under `reproLocations`, which also accepts a per-case `endpoint`, `description`, and `tolerance`.

Ad-hoc repros apply the same `--correction` pipeline BAJAJMULTITEST does (and load three hops so the Catmull-Rom window matches the full cell), so a slice that fails in the multi test fails the same way here. Pass `--correction none` to see the raw annotations; a slice that only fails with correction on is a registration artefact, not an annotation problem.

Polyline-only slices (gap junctions, adherens, rafts) run the `PolylineRibbonMeshGenerator` stages instead of the polygon region path: `FirstPassDelaunay`, `Remove Invalid Edges`, `CompleteCorrespondingVertexFaces`, `FirstPassSliceChordGeneration`, `FirstPassFaceGeneration`, `CleanRibbonFaces`, then caps and `Final mesh`. When the slice is exactly one polyline per section, `CleanRibbonFaces` throws the Delaunay/chord faces away and rebuilds the strip by marching both lines in arc-length order (`TryRebuildTwoPolylineRibbon`), so for those slices the earlier stage views are not where the final faces came from. Forks and mixed slices keep the fold-removal + sliver-removal heuristics. A single location ID is an isolated annotation and only the cap stage runs.

**Debug one slice per BajajTest run.** `--repro-locations-file` exists to replay a whole failure list for a count, not for diagnosis: a batch shares one process, one output folder, and one set of stage views per case, and a fault in one case can abort the rest. When walking a failure list, take the next line, run it alone with `--repro-locations A,B`, read its stage views, resolve it, then take the next line.

**Do not narrow the shot list while diagnosing.** A capture request such as `{ "shots": [ { "stage": "Final mesh", "view": "3d" } ] }` produces no 2D line views, so the edge-type labels (chord classification) are never captured and the walkthrough cannot be done. Omit `--capture-request` (or use `shots: []`) to get the default set, and read the `-2d` line views first. 3D views should only be used as a final verification, if at all.  They may also be used to check normals.  When 3D is needed, give the shot a camera list (`cameras3D` / `--cameras`, see "3D cameras" below) rather than reading the default straight-down render.

### Verdicts: `manifold.txt`

Every case folder also gets a `manifold.txt` with `locations:`, `final:` (the `MeshManifoldReport` counts: `nonManifold`, `inconsistent`, `contourSeam`, `holes`, `isolated`, `forkGap`, `ribbonEdge`, `singleTriPolyline`), `firstInvalidStage:`, one line per stage, a `defects:` list (each bad edge with its vertices as `v12[L:0 iVert:3 of 18]` and the faces on it) and a `notes:` list. `IsValidSliceSurface` is only `nonManifold:0 inconsistent:0 holes:0`; seams, isolated edges and ribbon edges are informational. A `notes:` line `same-section shapes a and b intersect` means two annotations on the same section overlap, which is an annotation error the mesher cannot tile; report it rather than chase it.

After a fix, a batch replay of the whole list (`--repro-locations-file`, `-o` to a fresh folder) gives the new count; summarise it without opening PNGs:

```powershell
Get-ChildItem <out>\BajajTest\case-*\manifold.txt | ForEach-Object {
  $t = Get-Content $_.FullName
  ($t | Select-String '^locations').Line + ' | ' + ($t | Select-String '^final').Line }
```

Open PNGs only for the cases that are still invalid. When the multi test is mid-run, read its in-progress failure file with `[IO.File]::Open($path,'Open','Read','ReadWrite')`; `Get-Content` is refused while the writer holds it.

Launch config: **MonogameTestbed (BAJAJTEST screenshots)** in `.vscode/launch.json`.

### Dump one case

```text
dotnet exec MonogameTestbed.dll --mode BajajTest --screenshots --repro 5 -o C:\Temp\BajajTestScreenshots -q -v -l
```

Output:

```
C:\Temp\BajajTestScreenshots\BajajTest\
  manifest.json
  case-05-Region-with-no-perimeter\
    00-overview-2d-2d.png
    … mesh/line/region/OTV shots …
    error.txt          (only if load or mesh gen failed)
```

`manifest.json` lists case index, description, location IDs, endpoint, per-shot `stage` / `view` / `relativePath` / 2D camera (`lookAtX/Y`, `downsample`) / 3D camera (`camera`, `cameraPosition`, `cameraLookAt`), and `error` if generation faulted.

Default shots (no request file): overview 2D, OTV chords (glow = accepted, ladder = rejected), each mesh stage in 2D (plus 3D only with `--3d`), each line-view pass, the vertex-index view, each region pass. Legend HUD is burned into every PNG.

The PNG numbering runs through two view families. `01`…`NN-Final-mesh-3d` are the **mesh-stage renders**; the `-2d` one of each pair overlays that stage's classified edges and labels on the shaded faces (`ApplyShotUnlocked` pairs a mesh shot with the line view of the same stage name), the `-3d` one is shaded faces only. The files after that, starting again at the first stage (`15-FirstPassDelaunay-2d.png` for a ribbon case), are the **line-pass views**: the same stages drawn as classified edges with `SURFACE`/`INVALID`/`CONTOUR`/`CORRESPONDING` labels and vertex numbers. Interactively these are two separate modes (`PageUp`/`PageDown` steps the rendered mesh stage; the gamepad `B` button cycles the line pass). Diagnosis uses the line-pass views almost exclusively.

After the last line-pass view comes `NN-vertex-indices-2d.png`: the final edge set drawn **without** edge labels so every mesh vertex index is legible. Use it to map the `v12[L:0 iVert:3 of 18]` entries of a `manifold.txt` defect onto the picture; edge labels and vertex labels share the same pixels along a contour, so on the line-pass views one of them usually loses. It accepts `lookAt`/`downsample` in a capture request as `"stage": "vertex-indices"`.

## Interactive keys

| Key | Effect |
|---|---|
| `PageDown` / `PageUp` | Next/previous stage view. **2D only** — gated on `!Draw3D` |
| `V` | Toggle 2D and 3D. Also the gamepad left shoulder |
| `K` | Toggle cull mode. Also the gamepad left stick |

The current mode is shown in the HUD as `View: 2D` or `View: 3D mesh`. Many other toggles are gamepad-only.

## Annotation sizes

Line widths, vertex radii, and `LabelView.FontSize` are all **world units**, so their on-screen size is `value / Downsample`. The camera fits the slice, so a large slice drove the old hardcoded defaults (line 1.0, vertex 1.25–2, label 2.0) far below one pixel and they vanished. `BajajOTVAssignmentView.ScaleAnnotationsToScene` now derives them from `Camera.Downsample` each time the zoom or viewport changes, targeting fixed pixel sizes that also grow with capture resolution. Adjust `VertexRadiusPixels`, `LineWidthPixels`, `RegionEdgePixels`, and `LabelHeightPixels` to retune.

Under `--screenshots` the pixel targets are scaled against `CaptureReferenceViewportHeight` (576) instead of 1200, because a capture at full monitor resolution is normally rescaled to about a thousand pixels wide before anyone (or any image-reading tool) looks at it. The HUD in `MonogameTestbed.DrawLegendHUD` uses the same pair of reference heights.

Edge labels are drawn rotated along the edge they name. Two things had to be fixed for that to land where it belongs. `LabelView.Draw` anchored every label off the corner of its **axis-aligned** `BoundingRect`, which is only correct for upright text, so a rotated label was thrown off its anchor by roughly half its own width — worst for long text on steep edges, which is what put contour labels outside the cell entirely. Rotated labels now pivot about the row center over `Position`, and multi-row labels step along their own down axis. Separately, which endpoint a segment calls A is arbitrary, so `LabelView.ReadableRotation` reverses any leftward direction; without it half the labels rendered upside down. Each label is also offset across its edge into the adjacent face (`PolyBranchAssignmentView.InwardOffsetDirection` supplies the direction, `LineSetView.LineLabelOffsetDirections` carries it) so the text sits on the surface rather than straddling the line.

A slice carries about a thousand vertices and several thousand edges, so one shared label size either overlaps everywhere or is illegible everywhere. Each dense label is therefore fitted to the room it actually has — an edge label to its own segment length, a vertex label to the distance to its nearest neighbouring vertex — and only labels that end up below `MinLabelHeightPixels` are skipped at draw time. Markers and edges still draw underneath, and the HUD reports the count, e.g. `B: FirstPassDelaunay (58 labels too small, zoom in)`. Zooming in brings them back, so a crowded region is worth a `lookAt`/`downsample` capture request rather than a code change.

Two gotchas when adding a view: sizes must be re-applied whenever views are published (call `InvalidateAnnotationScale`, since the meshing task adds views while the camera sits still), and `LabelView` silently hides text smaller than 1/200th of the viewport height, which is why the skip floor stays above that.

## A frame that looks empty

If the mesh stage is blank while lines and labels still draw, suspect the depth clear. `GraphicsDevice.Clear` requires a depth value in 0..1; passing `float.MaxValue` is undefined and landed as 0, so the mesh pass (the only one that depth-tests, with `LessEqual`) had every fragment rejected at ndc z ≈ 0.03 while the non-depth-tested overlays survived. Clear depth to `1.0f`.

Check `downsample` and `lookAtX/Y` in `manifest.json` before concluding a stage drew nothing. Automatic framing fits both axes, so a blank frame with faint arcs clipping only the left and right edges means the camera was overridden, not that the geometry is missing. Stage views draw only that stage's overlay: an empty chord stage means zero chords were generated, which is itself the finding.

## Agent assessment loop

Follow [bajaj-stage-walkthrough](../bajaj-stage-walkthrough/SKILL.md): step through **every** stage view in pipeline order, 2D line views (edge-type labels) first, and blame the first stage where the defect appears. Do not diagnose from `Final mesh` alone.

1. Run capture (launch config or CLI). Wait until `-q` exits or `manifest.json` is written.
2. **Post a link to the output as soon as it exists**, before reading anything: the case folder (or `manifest.json` /
   `review.html`) as a markdown link on its absolute path, e.g.
   `[case-42-368197-368198](C:/Temp/BajajTestScreenshots/BajajTest/case-42-RPC1-368197-368198/)`. The user may be
   watching and should be able to open the same PNGs you are reading. Do this for **every** capture in the session,
   not only the first; when you re-run after a fix, link the new folder and name the one it replaces.
3. Read `manifest.json`, then **all** the PNGs (Read tool), in stage order.
4. Judge each stage **OK / wrong / unsure** using [reference.md](reference.md) (C1–C3, incomplete verts, edge colors).
5. **Show the user only unsure (or wrong) images.** Do not dump every PNG into chat; the folder link from step 2 covers the rest.
6. If labels are hidden (`N labels too small, zoom in`), write a capture-request JSON with `lookAt`/`downsample` and re-run with `--capture-request`.
7. After a code fix, re-run the **same** request and compare stage by stage.
8. Then re-check every previously fixed slice and record the new one: [mesh-difficult-cases](../mesh-difficult-cases/SKILL.md).

Use a fresh `-o` folder (or a fresh case description) per run rather than overwriting, so an earlier link the user
opened still shows what you were looking at when you posted it.

### Capture-request JSON

Omit `shots` (or use `[]`) for the full default set. `repro` in the file overrides `--repro`.

```json
{
  "repro": [5],
  "reproLocations": [
    { "locations": [8614, 8616], "endpoint": "RC1", "description": "no cross-band faces" }
  ],
  "shots": [
    { "stage": "FirstPassFaceGeneration", "view": "3d" },
    { "stage": "overview-2d", "lookAt": [15400, 16500], "downsample": 0.15 }
  ]
}
```

`stage` matches a mesh/line view name, `overview-2d`, `otv-chords`, or `region-N` (spaces/punctuation ignored). `view` is `2d` or `3d`. `lookAt` is `[x, y]` in volume XY; `downsample` is camera zoom (smaller = closer). Both apply to 2D shots only.

### 3D cameras

A 3D shot with no camera is taken straight down and looks like the 2D view with shading. To see the Z structure (walls, caps, folds, the twist at a polyline crossing) give the shot a **list of cameras**; each camera becomes its own PNG, named `NN-<stage>-3d-<camera>.png`.

```json
{
  "reproLocations": [{ "locations": [368197, 368198], "endpoint": "RPC1" }],
  "cameras3D": [
    { "preset": "oblique" },
    { "preset": "side" },
    { "name": "crossing", "azimuth": 200, "elevation": 25, "distance": 0.35, "lookAt": [-232, 260, 0] }
  ],
  "shots": [
    { "stage": "Final mesh", "view": "3d" },
    { "stage": "FirstPassFaceGeneration", "view": "3d", "cameras": [ { "preset": "below" } ] }
  ]
}
```

- `cameras3D` (top level) applies to every 3D shot without its own `cameras`, including the default shot list when `shots` is omitted. `--cameras oblique,side` on the command line sets the same thing without a request file.
- Presets: `top` (90°), `oblique` (az 240, el 35), `oblique-back` (az 60, el 35), `side` (az 0, el 8), `front` (az 270, el 8), `below` (az 240, el −35).
- `azimuth` is degrees around Z from +X toward +Y, measured from the look-at point to the camera; `elevation` is degrees above the XY plane. Either overrides the preset.
- `distance` multiplies the distance that fits the whole slice in frame (1 = fit, 0.3 = close-up). `lookAt` is `[x, y, z]` in the slice XY frame; Z is relative to the slice centre, so `0` is mid-slice. `position` `[x, y, z]` replaces the orbit entirely.
- `cull` defaults to `false` for placed cameras: a slice mesh is an open sheet whose winding faces an arbitrary side, and with viewer-style back-face culling half the orbit positions render nothing. Set `"cull": true` to reproduce what the viewer would show from that angle (a blank frame then means the normals face away).
- `manifest.json` records `camera`, `cameraPosition`, and `cameraLookAt` per 3D shot.

A ribbon is a slanted wall between two sections, so `side`/`front` show it edge-on or face-on depending on its heading; if one of them is a sliver, use the other, or an `oblique`.

```text
dotnet exec MonogameTestbed.dll --mode BajajTest --screenshots --capture-request C:\Temp\capture-request.json -o C:\Temp\BajajTestScreenshots -q -v -l
```

## Choosing a repro

`ReproSet` entries are historical crashers; the description is the old exception, not a pass criterion. Index **5** is the launch-config default (`Region with no perimeter`, locations 145431 / 145428, RPC1).

Pick an index whose description matches the failure (intersecting edges, medial-axis, incomplete perimeter, Delaunay after corresponding points, …). Do not add new IDs to `ReproSet` until the case is reproduced.

## After visual diagnosis

Fix the **stage** the PNGs implicate (correspondence / invalid edges / OTV / untiled close / caps) before changing later passes. Prefer a GeometryTests or FSCheck case for the degenerate polygon; keep `ReproSet` as the visual gate.
