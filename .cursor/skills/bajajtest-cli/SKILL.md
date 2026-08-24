---
name: bajajtest-cli
description: Dump and review MonogameTestbed BAJAJTEST screenshots via CLI to debug Bajaj slice-mesh generation. Use when investigating mesh tiling errors, ReproSet cases, --screenshots, --repro, --capture-request, BajajTest, or visual OTV/region/chord failures.
---

# BAJAJTEST CLI — debug slice mesh generation

`BajajAssignmentTest` (`TestMode.BAJAJTEST`) meshes **one slice pair** from `ReproSet` in `Clients/MonogameTestbed/BajajTest.cs`. It is a visual debugger, not an automated pass/fail test. Use it to inspect Delaunay, OTV chords, incomplete vertices, and per-stage meshes.

For algorithm/paper context see [contours-to-mesh](../contours-to-mesh/SKILL.md). For legend colors, gamepad, and stage names see [reference.md](reference.md).

**Not this skill:** whole-cell export is `BAJAJMULTITEST` (`-s` structure IDs, `-e` endpoint, `-o` DAE folder).

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
| `--screenshots` | After mesh gen, dump PNGs + `manifest.json` |
| `--repro N` | `ReproSet` index; also `0-3`, `1,5,7`, or `all` |
| `--capture-request path.json` | Replace the default shot list and/or override `--repro` |
| `-o dir` | Output root; screenshots go to `{dir}/BajajTest/` |
| `-q` | Exit after the last PNG |
| `-v` | Trace + ILogger to console |
| `-l` | Trace to a log file under `{dir}/Logs` (or cwd) |

Without `--screenshots`, `--repro N` still selects the interactive case (default index **5**). `-v` and `-l` can be combined.

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
