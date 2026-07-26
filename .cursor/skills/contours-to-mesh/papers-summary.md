# Paper summaries — contours to mesh

Sources: `MorphologyMesh/Papers/Bajaj96.pdf`, `MorphologyMesh/Papers/edwards2011topologically.pdf`.

> **Viking implementation advantage:** Both papers must solve the *correspondence problem* — inferring which contours on adjacent slices represent the same object. Viking skips this entirely. The `LocationLink` database table explicitly records contour-to-contour connections across slices as entered by annotators. The algorithm starts with correspondence already known; only tiling, branching, and untiled-region closing need to be computed.

## Bajaj, Coyle & Lin (1996) — *Arbitrary Topology Shape Reconstruction from Planar Cross Sections*

**Problem.** Reconstruct a triangle mesh of a 3D isosurface from sparse parallel 2D contours (medical imaging, or annotation traces).

**Three fundamental problems**

| Problem | Description |
|---------|-------------|
| Correspondence | Many topologies are consistent with the same two slices; need local rules to pair vertices |
| Tiling | Connect adjacent slices with slice chords forming tiling triangles |
| Branching | One contour splits to several (or merges); composite contours and horizontal caps |

**Three criteria** (Section 3) drive all theorems:

1. Surface is a piecewise-closed polyhedron (no self-intersection).
2. Vertical lines between slices intersect the surface at 0, 1, or along one segment (Criterion 2 / “unlikely topology” test).
3. Contours are recovered when the surface is cut at input slice planes.

**Key mechanisms**

- **Augmented contours** — insert vertices where a contour projection crosses another contour (Lemma 3); Viking: `AddPointsAtIntersections`.
- **Tiling triangles** — exactly two slice chords + one contour edge per triangle.
- **Dissimilar portions** — vertices that cannot legally tile to the opposite slice tile to the **medial axis** between slices (Fig. 3d).
- **Theorems 1–5** — tiling legality (projections, crossings, orientation).
- **Theorems 6–7** — correspondence uniqueness when criteria hold.
- **OTV (optimal tiling vertex)** — nearest opposite-slice vertex satisfying theorems; used in multi-pass chord generation.

**Limitations acknowledged.** Large slice spacing or tangent sampling can violate Criterion 2; some special cases still distort.

---

## Edwards & Bajaj (2011) — *Topologically correct reconstruction of tortuous contour forests*

**Problem.** Single-object Bajaj-style reconstruction composited across many neurons can yield **inter-object intersections** when slices are anisotropic (typical ssTEM: ~2–5 nm XY, ~45 nm Z, processes tens of nm apart).

**Contributions**

1. **Multi-component intersection removal** — move vertices along **Z** (orthogonal to slice plane) to separate overlapping meshes while preserving XY contour fidelity. Proves separation guarantees with minimum distance δ.
2. **Single-component robustness** — three **degeneracy cases** not in Bajaj96:
   - Chord projection through a third vertex (can violate Criterion 2 if paired chord accepted) → reject chord.
   - Overlapping oppositely oriented contour segments → rare; manual regional fix.
   - Vertex on boundary with both neighbors on same side → treat as non-overlapping.
3. **Untiled region medial axis (improved)** — Bajaj placed medial axis at mid-Z; Edwards **interpolates Z** per medial-axis vertex (Sibson / natural neighbor) so interior points satisfy Criterion 3 and avoid sliver/jagged tiles.

**Algorithm outline (Section 4, single component)**

1. Match labeled contours across slices.
2. Find overlapping corresponding pairs.
3. Penumbral regions → standard tiling.
4. Remaining **untiled** regions → convex decomposition → approximate medial axis → chords from region boundary to axis.
5. Z interpolation for medial-axis vertices (not uniform mid-plane).

**Viking implementation notes**

- Core Bajaj pipeline is in `MorphologyMesh`; Edwards chord degeneracy checks appear in `Theorem4` / `IsSliceChordValid`.
- Untiled closing uses `MedialAxisFinder.ApproximateMedialAxis` + triangulation in `TryClosingUntiledRegion`; Z is currently often `mesh.SliceCenterZ` (Edwards interpolation is a TODO/improvement area).
- Full **intersection removal across multiple structures** may not be wired into MonogameTestbed; paper is context for multi-object compositing goals.

---

## Terminology cheat sheet

| Term | Meaning |
|------|---------|
| Slice chord | Edge connecting a vertex on one slice to a vertex on the adjacent slice |
| Tiling triangle | Face with two slice chords + one in-slice contour edge |
| Untiled region | Region where no legal slice chord exists between boundary vertices |
| Penumbral region | Overlap zone where standard one-to-one tiling applies |
| OTV | Optimal tiling vertex — best opposite candidate for a given vertex |
| Corresponding vertex | Same (X,Y) on adjacent slices after augmentation |
| Solid / void region | CCW contour = solid inside; CW = hole/void outside |

---

## Bajaj96 figure reference

Figures live at `MorphologyMesh/Papers/bajaj96/Fig1.png` – `Fig28.png`.

| Fig | Shows | Code relevance |
|-----|-------|----------------|
| 1 | Correspondence problem: same cross-sections yield 4 different topologies (b)–(e) | Motivates `SliceGraph` / `ConcurrentTopologyInitializer` |
| 2 | Slice chord + tiling triangle definition | Core data type in `MorphMeshEdge`, `SliceChord` |
| 3 | Dissimilar contour tiling: (b) bad topology; (d) correct medial-axis tiling | Criterion 2; `TryClosingUntiledRegion` |
| 4 | Branching reconstructions (a)–(e): additive curve, composite contour, convex hull variants | `MeshAssemblyPlanner` branching handling |
| 5 | Oriented CCW/CW contours on parallel slices; solid/void regions | `MorphMeshOutwardOrientation`; winding convention |
| 6 | Criterion 2: vertical line intersection count 0/1/segment (a) vs violation (b) | `IsSliceChordValid`; Criterion 2 check |
| 7 | Projection definition; shadow region LS(q) / RS(q) | `Theorem2`; tiling region half-planes |
| 8 | Theorem 4 examples: valid T'1–T'3 vs invalid T'4–T'5 chord projections | `Theorem4` in `IsSliceChordValid` |
| 9 | Lemma 4: non-crossing slice chord index constraint | Tiling sequence monotonicity |
| 10 | Theorem 6 cases: (a) disjoint C1'/C2; (b) C1' inside C2 | `ConcurrentTopologyInitializer` correspondence |
| 11 | Augmented contours: (a) intersection; (b) overlap | `GridPolygon.AddPointsAtIntersections` |
| 12 | (a) Boundary chord check for Theorem 5; (b) six optimality cases for triangle formation | `OTVTable`; 4-pass tiling loop |
| 13 | Many-to-many branching full pipeline (a)–(i): slices → pass 1 → all passes → untiled → EVD → final mesh | **Key figure** — entire `BajajMeshGenerator.GenerateFaces` sequence |
| 14 | General vs numerically stable implementation (sharp triangle vs distortion trade-off) | Augmented-contour relaxation in Viking |
| 15 | Rough medial axis by repeated convex polygon decomposition | `MedialAxisFinder.ApproximateMedialAxis` |
| 16 | Branching tiling results: saddle-point curve (b) vs canyon line segments (d) | Untiled-region medial axis shape |
| 17 | Dissimilar contour comparison: (b/c) min-area failure; (d) shortest-chord failure; (f) Bajaj correct result | Demonstrates why heuristics fail; Theorem 2 necessity |
| 18 | Brain hemisphere reconstruction: Gouraud shading + wireframe | Results reference |
| 19 | Skull: (a) shaded; (b/c) two nasal slices; (d) their tiling | Complex real-data topology example |
| 20 | Freddy skeleton — full 3D view with numbered cross-section locations | Results reference |
| 21 | Freddy — tiling for 8 cross-section pairs | Real many-contour branching examples |
| 22 | Criterion 2 violations from undersampling (a)–(d): genus handles, near-tangent surfaces | `IsSliceChordValid` limitation cases |
| 23–28 | Appendix proof diagrams for Lemmas 1–5 and Theorems 1–7 | Background only; not directly wired to code |

---

## Edwards 2011 figure reference

Figures live at `MorphologyMesh/Papers/edwards2011topologically/fig1.png` – `fig15.png`.

| Fig | Shows | Code relevance |
|-----|-------|----------------|
| 1 | Pipeline overview: EM images → 2D contour tracing → per-object 3D mesh → composited multi-object result | End-to-end context for `MeshAssemblyPlanner` output |
| 2 | Component labeling: C(p) = set of contours whose XY projection contains point p; p₁ unlabeled, p₂ in {c₁,c₂}, p₃ in {c₂} | `ConcurrentTopologyInitializer` component assignment |
| 3 | Conflict removal by Z-shift: conflict points p^g and p^y moved along Z by s^g·ε and s^y·ε to eliminate inter-mesh overlap | Intersection-removal pass (TODO in Viking multi-object compositing) |
| 4 | Contour intersection removal: (a) overlapping contours; (b) after dilation δ/2; (c) after clipping; (d) after erosion | Separation guarantee algorithm |
| 5 | Single-component tiling: (a) after pass 1; (b) untiled region (yellow) tiled to medial axis with Z-interpolated vertices | `TryClosingUntiledRegion`; Z-interpolation improvement over Bajaj |
| 6 | Three Edwards degeneracy cases: (a) chord (a,b) illegal — projection hits vertex c; (b) no legal chords exist; (c) vertex a not tiled directly to lower contour | Extra checks in `Theorem4` / `IsSliceChordValid` |
| 7 | Medial-axis Z interpolation: (a) jaggies from Bajaj mid-plane placement; (b) smooth result with interpolated Z | Improvement area — `TryClosingUntiledRegion` currently uses `SliceCenterZ` |
| 8 | Intersection-removal algorithm steps: (a) conflict points on tile; (b) cut paths traced; (c) new polygons from cuts; (d) retriangulated | Internal detail of intersection removal |
| 9 | Two intersection cases: (a) edge-edge classic; (b) conflict at edges and vertices — before and after resolution | Covers degenerate mesh topology cases |
| 10 | ε calculation: vectors A,B from q^y to original conflict points; Ā,B̄ to resolved points; ε derived from δ | Separation distance math |
| 11 | Dendrite surface: (a) before smoothing; (b) after smoothing at ~half triangle count | Post-processing context; not in current Viking pipeline |
| 12 | Effect of separation distance δ: (a) δ=0 (touching); (b) δ=40 nm — surfaces changed only at region of close approach | Tuning the minimum-separation parameter |
| 13 | Intersection removal on real neuronal ssTEM data: (a) branch point before/after; (b) result on background image; (c) ≥8 intersections in small region | Shows why multi-object intersection removal matters |
| 14 | Two intersecting axons: (a) before; (b) zoomed intersection revealed; (c) after removal; (d) after smoothing | Full before/after of the algorithm on real data |
| 15 | Apical dendrite with transparency — interior endoplasmic reticulum visible | Final result / visual quality reference |
