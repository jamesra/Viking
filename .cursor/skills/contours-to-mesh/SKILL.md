---
name: contours-to-mesh
description: Guides contour-to-3D-mesh work using the Bajaj algorithm and Viking MorphologyMesh implementation. Use when editing MonogameTestbed mesh tests, MorphologyMesh generators, slice graphs, region closing, slice chords, medial-axis untiled regions, or debugging BajajTest / BajajMultiTest mesh failures.
---

# Contours to 3D Mesh

Viking reconstructs 3D cell/process surfaces from 2D annotation contours (XY cross-sections at known Z). The pipeline follows **Bajaj et al. 1996** with untiled-region improvements from **Edwards & Bajaj 2011**.

## When to read more

| Need | Read |
|------|------|
| Paper concepts distilled | [papers-summary.md](papers-summary.md) |
| Class/file map and pipeline order | [code-map.md](code-map.md) |
| Original PDFs | `MorphologyMesh/Papers/Bajaj96.pdf`, `MorphologyMesh/Papers/edwards2011topologically.pdf` |
| Bajaj96 figures (Fig1–Fig28) | `MorphologyMesh/Papers/bajaj96/Fig1.png` – `Fig28.png` |
| Edwards figures (fig1–fig15) | `MorphologyMesh/Papers/edwards2011topologically/fig1.png` – `fig15.png` |
| Internal walkthrough with figures | `Documentation/source/developerdocs/mesh/overview.rst` |

## Big picture (Bajaj)

Three coupled problems on sparse parallel slices:

1. **Correspondence** — which contour vertices on slice *i* connect to which on slice *i+1*
2. **Tiling** — fill the strip between slices with **tiling triangles** (two slice chords + one contour edge)
3. **Branching** — Y-junctions where one contour maps to several on the adjacent slice

Bajaj imposes three surface **criteria**, then derives local rules:

| Criterion | Meaning |
|-----------|---------|
| **C1** | Reconstructed surface + solids form piecewise-closed polyhedra (no self-intersection) |
| **C2** | Any vertical line between slices hits the surface 0, 1, or along one segment (rules out “comb” topologies) |
| **C3** | Resampling the surface on a slice reproduces the input contours |

**Untiled regions** (dissimilar contour portions, branches, gaps with no legal slice chord) are filled by tiling to an **approximate medial axis** (edge Voronoi / convex decomposition), not by arbitrary chords.

## Viking pipeline (high level)

```
MorphologyGraph (annotations + LocationLinks)
  → SliceGraph + SliceTopology (corresponding vertices at intersections)
  → BajajMeshGenerator per slice (parallel)
  → MeshAssemblyPlanner binary-tree composite
  → Collada export / MonogameTestbed visualization
```

Per-slice `GenerateFaces` order (see `BajajMeshGenerator.GenerateFaces`):

1. `AddDelaunayEdges` — XY Delaunay, constrained by contours
2. `GenerateRegionGraph` — classify edges; group faces into regions
3. `RemoveInvalidEdges` — drop edges/faces that cannot be exterior
4. `CompleteCorrespondingVertexFaces` — early faces at corresponding vertices
5. **Pass 1** `MergeAndCloseRegionsPass` — close **UNTILED** regions via medial axis
6. `FirstPassSliceChordGeneration` — OTV tables, multi-pass loosening criteria
7. `FirstPassFaceGeneration`
8. **Pass 2** `SecondPassRegionDetection` + `MergeAndCloseRegionsPass` — cap remaining holes
9. `FirstPassFaceGeneration` again
10. `CapMeshEnd` on open ends; `EnsureFacesHaveExternalNormals` / `RecalculateNormals`

## Code ↔ paper mapping

| Paper concept | Code |
|---------------|------|
| Correspondence (solved explicitly) | Viking uses `LocationLinks` (DB table) — contour-to-contour mapping across slices is **recorded by the annotator**, not inferred by geometry. Both papers solve correspondence algorithmically; Viking does not. |
| Augmented contours / correspondence | `GridPolygon.AddPointsAtIntersections`, `SliceGraph`, `ConcurrentTopologyInitializer` |
| Edge classification | `BajajMeshGenerator` after Delaunay; `EdgeType` enum |
| Theorems 1–7, slice-chord validity | `SliceChordTestType`, `Theorem2`, `Theorem4`, `IsSliceChordValid` |
| Optimal tiling vertex (OTV) | `BajajOTVAssignmentView`, `OTVTable`, `CreateOptimalTilingVertexTable` |
| Untiled / medial-axis closing | `RegionGraphExtensions.TryClosingUntiledRegion`, `MedialAxisFinder` |
| Edwards degeneracy cases | Extra checks in chord validation; manual fallback for rare overlap cases |
| Compositing | `MeshAssemblyPlanner`, `SliceGraphMeshModel` |

## Invariants (do not break casually)

- **Corresponding vertices** must exist wherever upper/lower shapes intersect; do not move contour vertices after topology is built without re-running correspondence.
- **Slice chords** must pass `SliceChordTestType` suites; passes intentionally loosen from strict to permissive.
- **Untiled closing** triangulation input must be deduplicated (`MeshExtensions.CleanRegionTriangulationInput`) — degenerate/colinear points crash divide-and-conquer Delaunay.
- **Winding** — use `MorphMeshOutwardOrientation` / `FaceHasCCWWinding`; fix normals after face edits.
- **Edge face capacity** — a CONTOUR edge carries one face, every other edge two. `MorphRenderMesh.AddFace` refuses (and counts in `FacesRefusedAtEdgeCapacity`) any face whose edge already has two; the passes that add faces (`TryAddCorrespondingNeighborPair` wedges, `TryClosingUntiledRegion`, `TryClosingSmallRegion`, `TryClosingPinchedRegion`, the `SplitFace(quad)` in `RemoveFirstAdjacentCorrespondingVerticies`) still check the CONTOUR-1 limit themselves and prefer leaving a hole to creating a non-manifold edge. A loop that re-queues a vertex after `AddFace` must check that the face count grew, or a refused face spins it forever. Medial-axis patch faces are anchors that `MeshWindingReorientation` will not flip.
- **Do not weld near-coincident cross-section vertices.** Correspondence is exact-XY only (`PositionToIndex` in `PopulateMesh`; insertion snaps within `Tolerance.Epsilon` = 0.001 nm). A 0.5 nm weld was tried to remove a 0.05 nm sliver (RPC1 133336/133341) and reverted: copy-pasted polygons on adjacent sections sit ~0.1 nm apart after process smoothing, and turning those pairs into corresponding vertices broke seven slices that had always meshed. The sliver case is handled by the `AddFace` capacity refusal instead.
- **Registration correction** (`--correction`, before slicing) may translate process/terminal contours (neighbor hop field and/or residual-ordered Catmull-Rom curvefit). `LimitOffsetToAvoidSameSectionOverlap` halves a curvefit move until it no longer intersects a same-section contour of the same structure. A `same-section shapes a and b intersect` note that disappears with `--correction none` was correction; one that stays is the annotation.
- **Delaunay input** — `AddDelaunayEdges` retries a failed triangulation up to four times with a ≤4 nm site jitter (`TriangulateSitesWithRetry`); a slice that still throws `EdgesIntersectTriangulationException` has genuinely degenerate input.
- **Two-polyline slices** are rebuilt as a ruled strip in `PolylineRibbonMeshGenerator.CleanRibbonFaces`; only forks/mixed slices use the fold-removal heuristics.
- **Coordinates** — mesh vertices are volume coordinates; see `coordinate-spaces` rule.

## Debugging workflow

1. For a **single slice pair**, dump BAJAJTEST screenshots with the CLI (see [bajajtest-cli](../bajajtest-cli/SKILL.md)). For a whole cell, use `BajajMultiTest` or `MeshTest` and check Trace for `Exception building mesh U: … D: …`.
2. Identify stage: step through every stage view in order, 2D line views first, and blame the first stage where the defect appears (see [bajaj-stage-walkthrough](../bajaj-stage-walkthrough/SKILL.md)); correspondence warnings in slice-graph build vs pass-2 untiled triangulation vs slice-chord failure.
3. For one failing region, breakpoint `TryClosingUntiledRegion` or `SecondPassRegionDetection`; dump perimeter + medial-axis points before `Triangulate`.
4. Fix upstream (correspondence) before downstream (Delaunay) when both fail.
5. Add **GeometryTests** / FSCheck cases for minimal degenerate polygons; add location IDs to `ReproCase` arrays when found in the wild.
6. Before and after the fix, re-check every previously fixed slice: unit tests plus baseline-image comparison, and append the new slice to the list (see [mesh-difficult-cases](../mesh-difficult-cases/SKILL.md)). Geometry changes that alter any past case must be judged improved/equivalent, not silently accepted.

## Testing targets

- `GeometryTests` — Delaunay, polygon intersection, transforms
- `Clients/MonogameTestbed/BajajTest.cs` — single-slice ReproSet visual debugger (`--mode BajajTest --screenshots`)
- `Clients/MonogameTestbed/BajajMultiTest.cs` — whole-cell multi-slice runs
- `Clients/MonogameTestbed/MeshTest.cs` — parameterized repro cases
- `AnnotationVizLibTests/MorphologyGraphTest.cs` — graph loading

## Future direction (from overview.rst)

Viking knows **LocationLinks** (which contours connect) but not hole correspondence across slices. Region pairing for tunnels and tighter slice-chord criteria remain improvement areas.
