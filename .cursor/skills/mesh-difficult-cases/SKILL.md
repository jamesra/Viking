---
name: mesh-difficult-cases
description: Maintains the running list of difficult slices the Bajaj mesh generator once got wrong (MorphologyMeshTest/DifficultCases/difficult-cases.json) and re-checks every past case, by unit test and by baseline-image comparison, whenever a mesh case is fixed or geometry code changes. Use after fixing a BajajTest / BajajMultiTest failure, after editing MorphologyMesh, Geometry, SliceGraph, SliceTopology, contour simplification, or smoothing code, and when asked whether a mesh change regressed anything.
---

# Difficult mesh cases: keep the list, re-check every case

Fixes to the mesh generator have repeatedly broken slices that were fixed earlier. Every difficult slice that was
made to work is therefore kept in one list and re-checked, both as a unit test (does it still mesh to a complete
surface?) and as a picture (does it still look right?).

## The list

`MorphologyMeshTest/DifficultCases/difficult-cases.json` — `{ "cases": [ … ] }`, one object per case:

```json
{
  "volume": "RPC1",
  "locations": [365031, 365032],
  "problem": "Two 50 nm polylines 14 um apart: the chord passes paired the lines in opposite directions (bow-tie) and fold removal kept the fan from the nearest endpoint (holes:4).",
  "fix": "TryRebuildTwoPolylineRibbon: one polyline per section is rebuilt as a ruled strip.",
  "cameras": [ { "preset": "side" }, { "preset": "oblique" } ],
  "shots2D": [ { "stage": "Final mesh", "lookAt": [1234.5, -678.9], "downsample": 0.1 } ]
}
```

- `volume` is a `Viking.Common.Endpoint` name (RC1, RPC1, …) or a full OData URL; `locations` are the slice's
  LocationIDs.
- `problem` says what the mesh looked like **and which stage caused it**; `fix` names the code change. Write both
  the way you would explain them to the next person who sees the case regress.
- `cameras` are the 3D placements that show this defect (same fields as the testbed's `CaptureCameraRequest`:
  `preset` top/oblique/oblique-back/side/front/below, or `azimuth`/`elevation`/`distance`, optional `name`,
  `lookAt`, `position`). Pick the views where the original defect was visible: `side` for ribbon twists and
  folds, `below` for caps, `oblique-back` for stacked faces on a corresponding edge. A case with no `cameras`
  gets `oblique` + `side`.
- `shots2D` (optional) are extra 2D zooms for the case.
- `open` (optional, default false): when `true`, the case is **tracked but not regression-gated**.
  `DifficultCaseRegressionTests` and `Compare-DifficultCases.ps1` skip it. Use this for unfixed
  BajajMultiTest failures you want on the list before a fix lands.
- `failureKind` (optional): `SliceFailureKind` name from the failed-slices header
  (`Topology`, `FaceGenerationException`, `InvalidSurface`, `UntiledLinkedPair`). Set by
  `Import-FailedSlices.ps1`; ignored by tests.

**Append when you fix a case. Never delete a case because it started failing.** A synthetic unit test with the
copied coordinates is still worth writing (it runs offline and pins the exact geometry); the list entry is in
addition, so the live annotations keep being checked. `DifficultCaseList.Load()` parses the file for the tests;
`Compare-DifficultCases.ps1` reads the same file.

### Failure kinds (failed_slices `[Kind]` header)

| Kind | Meaning |
|---|---|
| `Topology` | SliceGraph could not build topology |
| `FaceGenerationException` | GenerateFaces threw |
| `InvalidSurface` | Mesh exists but manifold report rejects it (holes, non-manifold, winding) |
| `UntiledLinkedPair` | LocationLinked cross-band pair has no spanning face (even if manifold looks clean) |

When recording `problem`, keep the `[Kind]` from the failed_slices comment so imports stay filterable.

## Tracking failures (open) vs regressions (closed)

- `open: true` → tracked from BajajMultiTest; unit tests + Compare skip it; no baselines required yet.
- omit `open` / `open: false` → must mesh complete (raw + curvefit) and have baselines under `baselines/`.

### Promote failed_slices → difficult-cases (open)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .cursor\skills\mesh-difficult-cases\scripts\Import-FailedSlices.ps1 `
  -FailedSlicesPath C:\Temp\Perf\...\bajajmultitest_failed_slices.txt `
  -Volume RPC1 `
  -Kind UntiledLinkedPair   # optional filter
```

Dedupe is by case key (`VOLUME-id-id-…`). Do **not** run Compare/`-Accept` on open cases.

### Close an open case (after fix)

1. Confirm with BajajTest `--repro-locations …` (and optionally `--correction none` vs default).
2. Set `open` to false (or remove it); rewrite `problem` (stage + defect) and `fix` (code change).
3. Run DifficultCases unit filter + Compare; `-Accept` baselines for that Case.
4. Commit json + baselines + code together.

Baselines for each case live in `MorphologyMeshTest/DifficultCases/baselines/<VOLUME>-<ids>/`: `Final-mesh-2d.png`,
one `Final-mesh-3d-<camera>.png` per listed camera, and `manifold.txt` (the `MeshManifoldReport` of the accepted
mesh). They are captured **with** BajajMultiTest's process smoothing, at 1280 px width.

After a mesh-pipeline performance change, also run RC1 structure **410** in Release with `--timings` (see
[bajajtest-cli](../bajajtest-cli/SKILL.md) "Whole-cell performance benchmark") and confirm DifficultCases still pass
before declaring the change safe. Record the report's system RAM / CPU-busy / coresUsed lines with the numbers:
Server GC vs workstation GC, and any two runs on this machine, are not comparable without those. A new
`slow FaceGeneration` location list should be walked with BajajTest `--repro-locations` the same way a
failed-slice list is.

## When you must run this

- You fixed a slice (before adding its line, and again after, so the new baseline is captured).
- You changed anything that moves vertices or decides faces: `MorphologyMesh/**`, `Geometry/**`,
  `SliceGraph`, `SliceTopology`, contour simplification, `CurveFitProcesses` / `--correction`, correspondence insertion, caps.
- You are about to say a mesh change is done.

## Step 1 — unit tests (complete surface)

```powershell
dotnet test MorphologyMeshTest\MorphologyMeshTest.csproj -c Debug --filter "TestCategory=DifficultCases" --logger "console;verbosity=normal"
```

`DifficultCaseRegressionTests` meshes every line twice — raw annotations and smoothed — and requires no face-generation
exception, `GenerationHadErrors == false`, and `IsValidSliceSurface` (`nonManifold:0 inconsistent:0 holes:0`).
These need the OData endpoints, so they carry `TestCategory=LiveData` as well; the offline filter
`TestCategory!=LiveData` skips them, which is why they must be run explicitly here.

A red case is a regression until proven otherwise. Do not edit or remove the case; fix the code or bring the
conflict to the user.

## Step 2 — pictures (looks reasonable)

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .cursor\skills\mesh-difficult-cases\scripts\Compare-DifficultCases.ps1
```

The script writes a capture request from the list, runs `MonogameTestbed --mode BajajTest --screenshots` for all
cases in one process (Final mesh 2D plus one 3D shot per camera the case lists), diffs each PNG against its
baseline and prints:

```text
Case                         Verdict    MaxDiff%  New final
RPC1-368195-368197           unchanged         0  final: faces:12 manifold:11 nonManifold:0 ...
RPC1-368197-368198           changed       3.732  final: faces:12 manifold:11 nonManifold:0 ...
```

Verdicts: `unchanged`, `changed` (pixels or manifold line differ), `new` (no baseline yet), `missing` (capture
failed — read `error.txt` in the case folder). Exit code 2 when anything is not `unchanged`. Options: `-Case 368197`
to select cases, `-SkipCapture -OutputRoot <dir>` to re-diff an existing capture, `-NoCorrection` for raw annotations,
`-Threshold` (percent of changed pixels, default 0.02), `-Tolerance` (per-channel, default 24).

Rendering is deterministic: identical geometry gives 0.000 %. Any non-zero diff means the mesh changed.

As soon as the script finishes, post the `Review:` path it printed as a markdown link in chat
(`[review.html](C:/Temp/DifficultCases/20260910-164500/review.html)`) so a user who is watching can open the same
page you are about to judge from.

## Step 3 — judge a change; do not reject it out of hand

A changed picture is information, not a verdict. For each `changed` case:

1. Compare the two `final:` manifold lines. Fewer `holes`/`nonManifold`/`inconsistent`, or the same counts with
   fewer `contourSeam`/`ribbonEdge`, is evidence of improvement; more is evidence of regression.
2. Read the three new PNGs and the diff PNGs under `<OutputRoot>/diff/` (red = changed pixels over a faded
   baseline). A face that moved but still spans the same contours is usually equivalent; a new gap, fold, sliver, or
   a wall that no longer reaches its cap is a regression.
3. If it is not obvious, run the stage walkthrough on that slice
   (`.cursor/skills/bajaj-stage-walkthrough/SKILL.md`) and compare stages, not final renders.
4. Decide **improved / equivalent / regressed**. Improved or equivalent: accept (Step 4). Regressed: fix the code
   and re-run.

If you cannot decide, hand it to the user. The script already wrote a review page; open it for them:

```powershell
Invoke-Item <OutputRoot>\review.html
```

`review.html` (built from `scripts/review-template.html`) shows a summary table, then per case the description, both
manifold lines, and for each shot **baseline | new | diff** side by side (click an image to zoom) with radio buttons
for improved / regressed / equivalent. Tell the user which cases you are unsure about and what you see in each, and
ask for their judgement. Do not accept a baseline the user has not agreed to when you were unsure.

## Step 4 — accept new baselines

Only after the change is judged improved or equivalent (by you when clear, by the user when not):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .cursor\skills\mesh-difficult-cases\scripts\Compare-DifficultCases.ps1 -SkipCapture -OutputRoot <OutputRoot> -Accept -Case 368197,368198
```

`-Accept` copies the new PNGs (downscaled to `-BaselineWidth`, default 1280) and `manifold.txt` over the selected
baselines. Without `-Case` it accepts every case, so only omit it on a first-time capture. Baselines are committed
with the code change that made them true.

## Adding a case (checklist)

**Closed (fixed) case:**

```text
- [ ] Case fixed; synthetic unit test added where the geometry could be copied
- [ ] Step 1 and Step 2 run BEFORE the fix's line is added: all past cases unchanged or judged improved
- [ ] Case object appended to difficult-cases.json with volume, locations, problem, fix, and cameras (open omitted/false)
- [ ] Compare-DifficultCases.ps1 -Case <ids> -Accept  → baselines/<VOLUME>-<ids>/ created
- [ ] dotnet test --filter TestCategory=DifficultCases green
- [ ] difficult-cases.json, baselines/, and the code change go in the same commit
```

**Open (unfixed) case:** use `Import-FailedSlices.ps1` or append manually with `open: true`; do not accept baselines until closed.

## Files

| Path | Purpose |
|---|---|
| `MorphologyMeshTest/DifficultCases/difficult-cases.json` | The list |
| `MorphologyMeshTest/DifficultCases/DifficultCaseList.cs` | Parser shared by the test |
| `MorphologyMeshTest/DifficultCases/DifficultCaseRegressionTests.cs` | Raw + smoothed complete-surface test per line (skips `open`) |
| `MorphologyMeshTest/DifficultCases/baselines/<case>/` | Accepted PNGs and `manifold.txt` |
| `scripts/Compare-DifficultCases.ps1` | Capture, diff, `review.html`, `-Accept` (skips `open`) |
| `scripts/Import-FailedSlices.ps1` | Promote failed_slices → open cases |
| `scripts/ImageDiff.cs` | Pixel diff and downscale, compiled by the script via `Add-Type` |
| `scripts/review-template.html` | Static page the script fills with `{{SUMMARY}}` and `{{CASES}}` |

Capture requires a display (BajajTest renders with MonoGame) and the `Debug` build of MonogameTestbed; the script
builds it when the DLL is missing but not when it is stale, so rebuild after code changes before comparing.
