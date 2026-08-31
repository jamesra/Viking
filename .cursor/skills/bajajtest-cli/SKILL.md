---
name: bajajtest-cli
description: Dump and review MonogameTestbed BAJAJTEST screenshots via CLI to debug Bajaj slice-mesh generation. Use when investigating mesh tiling errors, ReproSet cases, --screenshots, --repro, --capture-request, BajajTest, or visual OTV/region/chord failures.
---

# BAJAJTEST CLI — debug slice mesh generation

`BajajAssignmentTest` (`TestMode.BAJAJTEST`) meshes **one slice pair** from `ReproSet` in `Clients/MonogameTestbed/BajajTest.cs`. It is a visual debugger, not an automated pass/fail test. Use it to inspect Delaunay, OTV chords, incomplete vertices, and per-stage meshes.

For algorithm/paper context see [contours-to-mesh](../contours-to-mesh/SKILL.md). For legend colors, gamepad, and stage names see [reference.md](reference.md).

**Not this skill:** whole-cell export is `BAJAJMULTITEST` (`-s` structure IDs, `-e` endpoint, `-o` DAE folder). Child structures are included by default; pass `--xc` / `--exclude-children` to mesh only the IDs on `-s`.

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
| `--repro-locations A,B[,C…]` | Mesh the slice spanning these **LocationIDs** without touching `ReproSet`. Needs `-e`. Appended after the repro set and auto-selected when `--repro` is absent |
| `--repro-tolerance T` | Simplification tolerance for `--repro-locations` (default 1.0) |
| `--capture-request path.json` | Replace the default shot list and/or override `--repro` |
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

A diagnostic that reports a bad slice prints its LocationIDs; feed them straight in. No code edit, no rebuild:

```text
dotnet exec MonogameTestbed.dll --mode BajajTest --screenshots --repro-locations 8614,8616 -e RC1 -o C:\Temp\BajajTestScreenshots -q -v
```

The case appears as `case-NN-ad-hoc-8614-8616`. The same slices can be listed in a capture request under `reproLocations`, which also accepts a per-case `endpoint`, `description`, and `tolerance`.

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

`manifest.json` lists case index, description, location IDs, endpoint, per-shot `stage` / `view` / `relativePath` / camera, and `error` if generation faulted.

Default shots (no request file): overview 2D, OTV chords (glow = accepted, ladder = rejected), each mesh stage in 2D and 3D, each line-view pass, each region pass. Legend HUD is burned into every PNG.

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

1. Run capture (launch config or CLI). Wait until `-q` exits or `manifest.json` is written.
2. Read `manifest.json`, then the PNGs (Read tool).
3. Judge each stage **OK / wrong / unsure** using [reference.md](reference.md) (C1–C3, incomplete verts, edge colors).
4. **Show the user only unsure (or wrong) images.** Do not dump every PNG into chat.
5. If a closer look is needed, write a capture-request JSON and re-run with `--capture-request`.
6. After a code fix, re-run the **same** request and compare.

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

`stage` matches a mesh/line view name, `overview-2d`, `otv-chords`, or `region-N` (spaces/punctuation ignored). `view` is `2d` or `3d`. `lookAt` is `[x, y]` in volume XY; `downsample` is camera zoom (smaller = closer).

```text
dotnet exec MonogameTestbed.dll --mode BajajTest --screenshots --capture-request C:\Temp\capture-request.json -o C:\Temp\BajajTestScreenshots -q -v -l
```

## Choosing a repro

`ReproSet` entries are historical crashers; the description is the old exception, not a pass criterion. Index **5** is the launch-config default (`Region with no perimeter`, locations 145431 / 145428, RPC1).

Pick an index whose description matches the failure (intersecting edges, medial-axis, incomplete perimeter, Delaunay after corresponding points, …). Do not add new IDs to `ReproSet` until the case is reproduced.

## After visual diagnosis

Fix the **stage** the PNGs implicate (correspondence / invalid edges / OTV / untiled close / caps) before changing later passes. Prefer a GeometryTests or FSCheck case for the degenerate polygon; keep `ReproSet` as the visual gate.
