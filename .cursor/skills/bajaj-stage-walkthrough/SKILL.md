---
name: bajaj-stage-walkthrough
description: Diagnose a bad Bajaj slice mesh by stepping through every BajajTest stage view in order, 2D line views with edge-type labels first, and attributing the defect to the first stage where it appears. Use whenever debugging BajajTest / BajajMultiTest output, a failed slice, a hole, fold, sliver, missing faces, or wrong chords in MorphologyMesh; do not judge from the Final mesh view alone.
---

# Bajaj stage walkthrough

The Final mesh view shows *that* a slice is wrong. It almost never shows *which stage* made it wrong, and the fix
belongs in that stage. Delaunay, invalid-edge removal, corresponding-vertex faces, chord generation, face
generation, region closing, cleanup, and caps each leave a distinct fingerprint that is only visible in that
stage's own view. Judging from the final mesh leads to patching later passes (caps, normals, validators) to hide a
defect that an earlier pass introduced.

## Rules

1. Read **every** stage view for the case, in pipeline order. Do not stop at `Final mesh`. Capture with the default
   shot list (no `--capture-request`, or `shots: []`); a request that lists only `Final mesh` never writes the
   line views, so there is nothing to walk through. One slice per BajajTest run.
2. Read the **2D line views first**. They carry the edge-type labels (`SURFACE 4-5`, `CONTOUR 2-3`,
   `CORRESPONDING`, `INVALID`, `MEDIALAXIS`, ...) with vertex numbers; the shaded mesh views do not. The label
   tells you what the classifier believed about each chord at that stage.
3. Diff consecutive stages: which edges and faces appeared, which disappeared. The defect belongs to the
   **first stage where it is present**. If a bad chord is already in `FirstPassDelaunay` and survives
   `Remove Invalid Edges`, the problem is edge classification, not face generation.
4. If the HUD says `N labels too small, zoom in`, zoom. Write a capture request with `lookAt` and `downsample`
   around the suspect vertices and re-run. Never conclude from an unlabelled picture.
5. Pair the pictures with numbers. Dump the faces and edges of the slice (vertex index, shape index, edge type,
   face count) from a MorphologyMeshTest diagnostic and confirm what the picture suggests before changing code.
6. After a fix, re-run the **same** capture request and step through the stages again. A fix that only changes
   `Final mesh` and not the stage you blamed is treating the symptom.

## Stage order and what to look for

Polygon slices (`RunPolygonStages`) and polyline-only slices (`RunRibbonStages`) publish different views; both
start with the same three.

| Stage | Line view shows | Wrong when |
|-------|-----------------|------------|
| `FirstPassDelaunay` | Every Delaunay edge, classified | Hull-spanning `SURFACE` chords joining one shape's end to the other's start; `SURFACE` chords that cross a corresponding (gold) vertex's twist; `INVALID` labels on chords that should be surface |
| `Remove Invalid Edges` | Survivors | An edge you flagged above is still there → classifier bug, fix `GetEdgeType`; faces vanished that should have stayed → over-eager INVALID |
| `CompleteCorrespondingVertexFaces` | Faces around gold vertices | Faces pairing `previous` of one shape with `next` of the other on a same-direction crossing; quads split through the wrong diagonal; a vertical sliver on the corresponding edge with nothing beside it |
| `MergeAndCloseRegionsPass` (polygon) | Region polygons, medial-axis edges | Regions closed with medial axis where the contours actually overlap; regions left open |
| `FirstPassSliceChordGeneration` | Glow = accepted chord, ladder = rejected | Accepted chords that cross; obvious pairings rejected; a vertex with no accepted chord that has a clear partner |
| `FirstPassFaceGeneration` | Remaining incomplete vertices (orange/red) | Interior contour vertices still incomplete; a contour segment with two faces |
| `CleanRibbonFaces` (polyline) | Two-polyline slice: the rebuilt ruled strip. Fork/mixed slice: what fold/sliver removal took out | Strip paired the lines in the wrong direction (endpoint-distance test); a removed face that was the only correct one (heuristic picked wrong); a fold left in place |
| `Second MergeAndCloseRegionsPass` (polygon) | Same as first | Same as first |

Region closing is guarded: a 3/4-vertex region is closed by `TryClosingSmallRegion` (one or two faces, shorter
3D diagonal), a larger region is refused when any perimeter edge already carries its full face count
(`TryFindEdgeAtFaceCapacity`, CONTOUR edges hold one face, every other edge two), and a region whose faces are not
edge-connected is split before closing (`SplitIntoEdgeConnectedGroups`). A refused close is traced and left as a
hole on purpose: a hole is a visible, local defect, a 3-face edge is a non-manifold surface that breaks everything
downstream. If the region views show a hole that the perimeter suggests could be closed, look for the refusal
trace before blaming the closer.
| `Cap upper / lower polygons` | Cap faces | Cap on the wrong band; cap normals disagreeing with the wall |
| `Final mesh` | Everything | Only useful to confirm the stage-level fix removed the symptom |

Edge and vertex colours are in [../bajajtest-cli/reference.md](../bajajtest-cli/reference.md).

## Procedure

```text
Progress:
- [ ] Capture with the default shot list (all stages, 2D and 3D)
- [ ] Post a link to the new case folder in chat (absolute path) so the user can follow along; repeat for every re-capture
- [ ] Read line views in order, note first stage with a suspect edge/face
- [ ] Zoom capture on the suspect vertices if labels were hidden
- [ ] Dump faces/edges numerically for that slice, confirm
- [ ] Fix the implicated stage
- [ ] Re-run the same capture, step through again
```

Capture (CLI details in [../bajajtest-cli/SKILL.md](../bajajtest-cli/SKILL.md)):

```text
dotnet exec MonogameTestbed.dll --mode BajajTest --screenshots --repro-locations A,B -e RPC1 -o C:\Temp\BajajTestScreenshots -q -v
```

Captures are 2D only by default: the 3D renders dominate capture time and show nothing about chords. Add `--3d`
once, at the end, to verify the fixed surface in 3D.

The default shot list writes **two families** per case. Nearly all of the work is done in the second one:

- **Line-pass views** — the files numbered after the last `Final-mesh-2d` render (with `--3d`, after
  `Final-mesh-3d`). One per stage, same order, drawn with the
  edge-type labels (`SURFACE 4-5`, `INVALID 3-9`, `CONTOUR`, `CORRESPONDING`, `MEDIALAXIS`), vertex numbers, chord
  glow/ladder, and incomplete-vertex colours. **Diagnose from these, stage by stage.**
- **Mesh-stage renders** — the first numbered `-2d` files (and `-3d` pairs when `--3d` is given). The `-2d` render
  of a stage overlays the line-pass view of the same stage (HUD shows both `A: <stage>` and `B: <stage>`), so it
  carries the chord labels on top of the shaded faces; the `-3d` render is shaded faces only and is for confirming
  a symptom is gone, not for diagnosis.

- **Vertex index view** — `NN-vertex-indices-2d.png`, after the line-pass views: final edges with no edge labels,
  so the vertex numbers are the only text. Open it alongside a line view whenever a defect names vertices.

If a line view reports `N labels too small, zoom in`, do not guess: write a capture request with `lookAt` and
`downsample` on the suspect vertices and re-run.

When a 3D check is warranted (a fold, a twist, whether a wall reaches its cap), do not read the default 3D render —
it is taken straight down and shows the same thing as 2D. Give the shot a camera list (`cameras3D` in the request or
`--cameras oblique,side` on the command line; one PNG per camera, `distance` below 1 for a close-up around a
`lookAt`). See "3D cameras" in [bajajtest-cli](../bajajtest-cli/SKILL.md).

Reading only the mesh renders (or only `Final mesh`) is the mistake this skill exists to prevent: the render
tells you a face is missing, the line view for the same stage tells you which chord was classified wrong or
which vertex was left incomplete, and that is where the fix goes.

Zoom request (coordinates come from the vertex labels or the numeric dump; `lookAt` is in the slice's XY frame):

```json
{
  "reproLocations": [{ "locations": [368197, 368198], "endpoint": "RPC1" }],
  "shots": [
    { "stage": "FirstPassDelaunay", "view": "2d", "lookAt": [-232, 260], "downsample": 0.12 },
    { "stage": "Remove Invalid Edges", "view": "2d", "lookAt": [-232, 260], "downsample": 0.12 },
    { "stage": "CompleteCorrespondingVertexFaces", "view": "2d", "lookAt": [-232, 260], "downsample": 0.12 },
    { "stage": "FirstPassFaceGeneration", "view": "2d", "lookAt": [-232, 260], "downsample": 0.12 },
    { "stage": "Final mesh", "view": "2d", "lookAt": [-232, 260], "downsample": 0.12 }
  ]
}
```

Interactive: `PageDown` / `PageUp` step stages in 2D, `V` toggles 2D/3D, gamepad `B` cycles line passes.

## Numeric dump

Model it on `MorphologyMeshTest/PolylineStructureHoleDiagnostic.cs` or `FailedSliceFixturesDiagnostic.cs`:
load the slice, `BajajMeshGenerator.GenerateFaces(mesh)`, then print `mesh.ManifoldReport`, every edge with
`Faces.Count != 2` (`A[shapeIndex]-B[shapeIndex] type=… faces=… ribbonEdge=…` with positions), and every face as
`[iVert(shape:vertex), …]`. Run with
`dotnet test MorphologyMeshTest --filter "FullyQualifiedName~<Class>" --logger "console;verbosity=detailed"`.
Mark live-data diagnostics `[TestCategory("LiveData")]`.

Run the slice the way BajajMultiTest does: ad-hoc repros apply process smoothing by default. If the failure only
appears in the multi test, do not pass `--correction none`. When `manifold.txt` carries a
`notes: same-section shapes a and b intersect` line, run the slice a second time **with** `--correction none`:
if the note disappears the overlap was created by registration correction (fix belongs in `MorphologyAlgorithms`, see
`LimitOffsetToAvoidSameSectionOverlap`); if it stays, the annotations overlap and the mesher cannot fix it.

## Worked example (RPC1 gap junction 52432)

Symptom: a hairline slit in the assembled ribbon. `Final mesh` 2D showed `SURFACE 4-5` spanning the whole slice
from the upper line's last vertex to the lower line's first. Stepping back: the edge was already in
`FirstPassDelaunay`, survived `Remove Invalid Edges`, and its fan of faces was present from the first mesh view
on. So the defect was Delaunay filling the convex hull of two open curves with nothing to reject it, not a cap or
normal problem, and the fix was a ribbon cleanup pass plus tests, not a change to `Final mesh` handling.

Second slice, same structure: after `Remove Invalid Edges` a bow-tie hole sat around a gold corresponding vertex;
`CompleteCorrespondingVertexFaces` filled it with faces joining the upper line's *previous* vertex to the lower
line's *next* vertex. The chords those faces used (`SURFACE 1-13`, `3-11`) were visible and labelled in
`FirstPassDelaunay`. Fix: classify chords that cross the twist as INVALID (`PolylineSpanPairing`). Neither fix
would have been found from the final view.
