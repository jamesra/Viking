# Publication notes: Viking

Annotation software for connectomes: the Viking and Jotunn clients, their servers (annotation, section correction,
SAM2 segmentation) and the gRPC protocols. VikingLegacy (the `Legacy` branch) keeps its own notes from 2026-10-26
(`D:\src\git\VikingLegacy\docs\publication_notes.md`); older WCF client entries stay here. Material for future
papers: citations, formulas, algorithmic decisions and the reasoning behind them. Index of all projects:
`~/.cursor/publication-notes.md`. Format and rules: the `publication-notes` skill.

## Methods and contributions

- **Circles across mosaic and volume space** (`.cursor/rules/circle-mapping-scale.mdc`): a circle's centre maps as a
  point; its radius maps by the mean scale of four rim points about the mapped centre (the five-point mean scale),
  never copied between spaces.
- **Tile-grid SAM2 segmentation with server-owned growth** (`Clients/Viking/WebAnnotation/UI/Commands/Segmentation`,
  `Servers/SegmentationServer`): the client names the tiles under its prompts, the server grows the mask over a fixed
  1024 px tile grid and asks the client for the tiles it lacks, encoding each tile once and reusing it across clients.
- **Auto-segmentation of circle annotations in view** (`Clients/Viking/WebAnnotation/UI/AutoPolygonize`): circles are
  segmented nearest the cursor first, with requests pipelined and the tiles they need prefetched.
- **Contour stacks to 3D meshes** (`MorphologyMesh`): the Bajaj et al. (1996) cross-section reconstruction applied to
  annotated cell profiles.
- **Depth-relative seed trust** (served by `Servers/SegmentationServer`; defined in Sam2Trainer
  `docs/depth_relative_trust.md`): the planned v7 prompt encoding for circle and carried-polygon seeds; Viking's part is
  the proto field for circle seeds and the server encoder.
- **Polygon label placement** (`Geometry.Core/Algorithms/LabelLayout.cs`, `InscribedRuns.cs`, `PoleOfInaccessibility.cs`;
  client `Clients/Viking/WebAnnotation/View/PolygonLabelSet.cs`): a polygon or closed curve's ID, structure label, tags,
  parent ID and overlapped-link markers go either in the true maximum inscribed circle (pole of inaccessibility, by
  polylabel's branch and bound over signed distance to exterior and hole segments) or along the largest inscribed
  oriented rectangle ("run") at 13 angles, whichever allows the larger ID font. Runs come from rasterising the rotated
  polygon to a 128×128 grid and the histogram-and-stack maximal-rectangle pass, keeping the (width, height) Pareto front
  so any text aspect fits without rasterising again; the candidate chosen is verified against the real segments
  (Liang–Barsky clip) and shrunk by 10% up to four times if a raster cell hid an edge. Markers that do not fit go one at a
  time to the point of greatest clearance from outline, text and earlier markers, found with the same branch and bound
  on a 1-Lipschitz clearance function.

- **Curves through chosen control points** (`Geometry/CurveFitting/CatmullRom.cs` `RefineControlPoints`,
  `Geometry/CurveFitting/ControlRingCurve.cs`; ported from VikingLegacy 1c91fc81 without its client pen-splice and
  region-cut code): an outline is stored as the control points of a centripetal Catmull-Rom curve. Control points are
  chosen by insertion only: starting from a fixed set, the reference vertex that best closes the largest gap between
  curve and reference is inserted until every span is within the tolerance both ways, and spans are re-checked until a
  pass inserts nothing (an insertion changes the neighbouring spans' tangents). `ControlRingCurve.TrySplice` replaces the
  stretch of a control ring between two crossings of a stroke and keeps the other control points exactly.

## Formulas

Circle radius between spaces (section to volume, or section to section through the correction), with $\Phi$ the
point map, $e_k$ the four unit axis vectors and $M$ the rim points that map:

$$
c' = \Phi(c), \qquad R' = R \cdot \frac{1}{|M|} \sum_{k \in M} \frac{\lVert \Phi(c + R e_k) - \Phi(c) \rVert}{R}
$$

Depth-relative trust of a seed (Sam2Trainer `docs/depth_relative_trust.md`): $w(\rho)$, $\rho = 1 - d / D_\ell$, full
trust for $\rho \le 0.5$, a cosine fall to 0 at $\rho = 0.95$ (`circle_diameter_scale`).

- **Centripetal Catmull-Rom** (`CatmullRom.FitCurveSegmentWithTValues`): knots $t_{j+1} = t_j + \lVert P_{j+1} - P_j \rVert^{\alpha}$
  with $\alpha = 0.5$, evaluated between $P_1$ and $P_2$ by the Barry–Goldman pyramid of linear interpolations.
- **Control point acceptance** (`RefineControlPoints`): for each span between control points $i_s, i_e$ with curve
  samples $C$ and reference polyline $R_{[i_s, i_e]}$, accept when
  $\max\left(\max_{k \in (i_s, i_e)} d(R_k, C),\ \max_{c \in C} d(c, R_{[i_s,i_e]})\right) \le \tau$.

- **Label font score** (`LabelLayout.RunFontSize`): for content whose rows at an ID font of 1 are $t_w$ wide (widest
  row, each row's width times its scale) and $t_h$ tall (sum of row scales: structure label 0.5, tags 0.25, ID 1, parent
  0.5), a run of width $W$ and height $H$ holding $k$ end markers of diameter $F$ allows the ID font
  $F_k = \min\left(\frac{H}{1.1\,t_h}, \frac{W}{k + 1.1\,t_w}\right)$; its score is $F_k$ for a level run and $0.9\,F_k$
  otherwise. The circle of radius $R$ allows $F_c = R/2$ (the circle labels' existing size). A run is used when its
  text-only score $s_0 \ge 1.25\,F_c$; it then keeps the largest $k \le n$ markers with $s_k \ge 0.6\,s_0$, split between
  down and up links in proportion. Free-space markers maximise
  $f(p) = \min(d_P(p),\ d_T(p),\ \min_j(\lVert p - c_j\rVert - r_j))$, with $d_P$ the signed distance to the polygon,
  $d_T$ the signed distance outside the text box and $c_j, r_j$ the markers placed so far; radius
  $\min(F/2, 0.95 f)$, at least $0.1F$.

- **Location link fade depth** (`Clients/Viking/WebAnnotation/View/LocationLinkView.cs` `FadeDepthAt`, shader
  `PinchedFadeDepth` in `Clients/MonogameXNAGraphicsShared/Content/PinchedLineShaders.fx`): for a point $x$ along a link of
  length $L$ between end circles of radii $r_A, r_B$,
  $t(x) = \operatorname{clamp}\left(\min\left(\frac{x - r_A}{r_A}, \frac{L - x - r_B}{r_B}\right), 0, 1\right)$; drawn
  opacity $\alpha(x) = \alpha_0 \cdot \operatorname{lerp}(1, w, t(x))$ with waist opacity $w = 0$ at rest and $1$ when hovered.
- **Polygon link anchor** (`LocationLinkView.AnchorOnOutline`): with centroid $c$, partner centre $p$, exit point $E$ =
  the crossing of segment $cp$ with the outline farthest from $c$, $d = (p - E)/\lVert p - E \rVert$, entry $N$ = the
  nearest crossing behind $E$ along $-d$, the end circle has radius
  $r = \min(\operatorname{dist}(\tfrac{N + E}{2}, \partial P), \sqrt{A/\pi})$ and centre $E - r\,d$.

## Algorithmic decisions

- **2026-10-28 Control point refinement checks both directions and re-checks neighbours** (ported from VikingLegacy
  1c91fc81, 2026-10-27).
  - **Why:** the old greedy pass in `IdentifyControlPoints` checked each span once, measured only from the reference to
    the curve, and an insertion in the next span changed the accepted span's end tangent unchecked (measured on Legacy:
    worst 4.47 px at tolerance 3, 15.1 px at tolerance 12).
  - **Now:** `IdentifyControlPoints` builds on `RefineControlPoints`; the golden fingerprints in
    `GeometryTests/Phase7GoldenTests.cs` were re-recorded.
  - **Junctions:** where a stroke meets an old outline at an angle the curve rounds the corner, and with only the
    corner's two neighbours available refinement could not pin it (1.87 px at tolerance 1 in
    `ControlPointFittingTests`). `DensifyNextToCorners` adds points along the two segments meeting each junction, at
    most τ apart.
  - **Not ported:** Legacy's pen splice into the stored control ring, the 2 volume px pen tolerance cap and the
    multi-crossing region cut. They are client code (`ControlPointCut`, `PenRegionCut`, `PenInputHelper`).

- **2026-10-28 Correction lattices are sampled by one shared function** (ported from VikingLegacy cdaf80de).
  - **Why:** the client's `SectionLatticeField` was an older port of the service's sampler. Where a cell had only some
    corners, it went straight to inverse-distance weighting, and it placed fallback nodes at cell centres,
    (g + ½)·pitch. The service (`SectionVectorField`, `NeighborResidualField`) interpolates over the present corners
    and places nodes at g·pitch. So Viking's image shift and `CorrectPoints` disagreed near the edges of annotated
    areas.
  - **Now:** all three call `LatticeInterpolation.Sample`.
  - **Checked:** the service's answers are unchanged, bit for bit: the old `SectionVectorField.Sample` against the
    shared function on 100,000 random points over 200 random sparse lattices.
  - **Note:** with the 0.001 weight threshold, the inverse-distance branch only has support within about 31 nm of a
    node. In practice a point is trusted only in a cell with at least one published corner.
  - Where: `Geometry.Core/Algorithms/LatticeInterpolation.cs`.

- **2026-10-28 CurveFit returns for damaged sections only (service 1.7.0).** In the default mode, `CorrectStructures`
  applies the field, then the leave-one-out Catmull-Rom fit to locations on damaged sections only.
  - **The fit:** only locations on undamaged sections are fitting points. The window reaches out to seven of them on
    each side, so a run of damaged sections is bridged.
  - **The guards stay:** a move may not lengthen a location's links or create an overlap on its own section. A shift
    shared by a whole run is the field's job (pass 2), not the fit's.
  - **Why:** the field is smooth (4 µm lattice), so it cannot follow a tear or fold. The fit moves each location on its
    own. Its weakness, reading a change of shape as misregistration (c1178, c16137), still applies. On a damaged
    section the real misplacement is larger than that, so the trade reverses.
  - **Measured** 2026-10-28 by `DamagedSectionCorrectionAssessment`, against the builder-3 field. It used the 12 cells
    with the most locations on damaged sections in each of RC2, RPC1, RPC2 and InferiorMonkey: 48 cells, 8,775 links
    touching a damaged section. Overlap is shared area ÷ smaller area, and no variant optimises it.

    | Variant (moves damaged-section locations only) | Mean overlap | Pairs gaining / losing > 0.1 vs field | Cells better / worse |
    |---|---|---|---|
    | Field alone | 0.649 | — | — |
    | Fit, through undamaged locations only (shipped) | 0.728 | 2,449 / 839 | 43 / 5 |
    | Fit, damaged locations also fitting points | 0.728 | 2,448 / 833 | 43 / 5 |
    | Linear between nearest undamaged neighbours | 0.719 | 2,426 / 970 | 39 / 7 |

    - Without the largest cell (RC2 c1, 1,504 pairs): 0.613 field, 0.695 shipped fit.
    - The 5 worse cells are small InferiorMonkey cells (13–28 pairs, mean −0.005 to −0.06).
    - This agrees with SBFSEMpy's 1.4.0 split of their 11 cells: 194 pairs touching a damaged section, 0.699 → 0.763.
  - **The bar, set before the data:** beat the field on mean overlap, more pairs gaining than losing over at least 50
    pairs, and not carried by one cell. It passed.
  - **Rejected:**
    - Linear interpolation: lower mean and more losses.
    - The fit everywhere (2026-10-27 entry).
  - **Scope:** the fit corrects annotation positions, for consumers that measure annotations (`CorrectStructures`,
    e.g. SBFSEM-tools). Viking and Jotunn shift the image by the field (`GetSectionCorrection`, `CorrectPoints`) and do
    not move annotations, so a per-location fit has no role there; no client change is needed.
  - Where: `CorrectionServiceCore.CorrectStructuresAsync`, `MorphologyGraph.CurveFitOptions.DamagedSections`,
    `EvaluateLeaveOneOutCentroid(windowCountsControls)`; tests in `JointRegistrationTests` (`DamagedOnlyFit_*`).

- **2026-10-27 Damaged sections correct no other section (builder 3).**
  - **What is damaged:** each block's `NonStosSectionNumbers` (in `VolumeData.xml` next to the VikingXML) lists the
    sections the slice-to-volume registration was built without: torn, folded or otherwise damaged.
  - **Why:** the joint fit solves every section's shift from triples of consecutive annotations along a process. A
    tear or fold on a section displaces its annotations locally, so as the end of its neighbours' triples it made
    good sections read as misregistered.
  - **Pass 1:** the main fit uses no annotation on a damaged section. Processes are bridged across it, as the stos
    skips it, and skipped sections do not count toward the gap limit.
  - **Pass 2:** each damaged section's own shift is fitted from triples centred on it, against the pass-1-corrected
    sections either side held fixed (no unknowns there). It is kept only if it lowers those triples' median second
    difference.
  - **Measured:** on a synthetic 40-section volume (150 nm registration error per section, 700 processes) with
    section 20 torn (its right half displaced (1.5, −0.9) µm), the neighbours' triples (sections 18–22, bridging the
    tear) have a median second difference of 20.6 nm told versus 27.0 nm blind (raw 406). The torn section's own
    triples are 40.5 nm told versus 34.9 nm blind (raw 439): blind fits the tear a little better by bending the
    neighbours toward it.
  - **Rejected:** the single fit with damaged sections included (the cost above), and dropping damaged sections
    altogether (they still need correcting).
  - The smooth lattice still cannot follow a tear's edge, so a damaged section stays rougher than the rest; whether
    a per-location fit should run there is the open CurveFit question below.
  - Where: `AnnotationVizLib/Graph/Morphology/JointRegistrationField.cs` (`CollectTriples`, `CollectDamagedTriples`,
    `Solve`), `Servers/SectionCorrectionBuilder/DamagedSectionList.cs`; test
    `JointRegistrationTests.TornSection_DoesNotPullItsNeighbours_AndGetsItsOwnShift`.

- **2026-10-27 Del hides what a click would pick; the item fades back in before it can be clicked.**
  - **Why:** a large annotation on the current section covers the adjacent-section annotation the user wants to drag
    onto this section. Del must hide whatever a click there would pick, in the same order hover uses: this section
    first, then adjacent sections, links, and the arrow markers inside a location. Otherwise it would hide something
    other than what blocks the click.
  - **Links:** a link to a hidden location would point at nothing, so a link hides while either end is hidden.
  - **Return:** the item is invisible for 95% of the hide time, then fades in over the last 5% along a quarter sine
    wave, $\sin(\pi t/2)$, which rises fastest at the start so the item shows up at once. It is not clickable until
    the fade ends, so the user sees it return before it can take a click.
  - **One store:** hidden items are kept in one store keyed by what a view shows (location ID, link key,
    structure-link key, ring key), so every view of one item hides together.
  - Ported from VikingLegacy 57dd42bd, 7ccd32e7, 38bb08dd, 5a460603 and f946d5b0 (dev 372b4f35). Where:
    `Clients/Viking/WebAnnotation/UI/TemporarilyHiddenAnnotations.cs`.

- **2026-10-27 Section correction drops CurveFit: answers are the published field only.** CurveFit moved each
  location to a leave-one-out Catmull-Rom fit along its process after the field was applied. It was meant to remove
  outliers (folds, tears), but in practice it pulled every node toward its neighbours, and from one cell it cannot tell
  registration error from a change of shape: a soma whose outline takes in a process base on one section (InferiorMonkey
  c16137, 1337465) or a process leaving a soma (c1178). The joint field pools many cells, so one cell's lobe barely
  moves it. Measured by SBFSEM-tools 2.39.0 against service 1.4.0 on 2026-10-27 (02:40–03:05 UTC). The run used 11
  cells across InferiorMonkey, RC1, RC2, RPC1 and RPC2, all on builder-2 sets, and 7,347 linked outline pairs, with
  overlap measured as shared area ÷ smaller area. Each mode was compared with NONE:

  | | NEIGHBOR (field) | ALL (field + fit) |
  |---|---|---|
  | Mean overlap (NONE 0.761) | 0.792 | 0.791 |
  | Pairs rose / fell / same (±0.01) | 3,457 / 1,324 / 2,566 | 3,429 / 2,053 / 1,865 |
  | Median, p5, p1 change | +0.008, −0.06, −0.17 | +0.007, −0.19, −0.44 |
  | Pairs losing / gaining > 0.1 | 185 / 977 | 733 / 1,351 |
  | Outlines moved ≥ 0.25 µm / lost overlap / pushed off neighbours / by a change of shape | 72 / 20 / 6 / 0 | 352 / 194 / 46 / 20 |

  - ALL against NEIGHBOR: 783 pairs lose more than 0.1 and 707 gain.
  - Per cell, the fit helped only RC1 c410 (0.844 → 0.862). It made no difference on the InferiorMonkey cells and RC2
    c5106, and made RPC1 c430 (0.817 → 0.802), RPC1 c2628 (0.738 → 0.732) and RPC2 c787 worse.
  - It moved no outline more than 2 µm beyond the field. Its large moves were all on circles. The largest three are
    RC2 c74420 769565 by 20.4 µm, 772432 by 9.0 and 779690 by 3.6, all on Z 792. RC2 section 792 is extensively
    damaged and is in the block's `NonStosSectionNumbers` (`Y:\Volumes\RC2\TEM\VolumeData.xml`), which lists the
    sections left out of slice-to-volume registration. A fit along the process will move whatever sits on such a
    section; the list is the input to use if damaged sections should be skipped or down-weighted.
  - The 1.3.x branch and tip rules had already cut ALL's harm from 1.2.0's 1,262 / 991 / 368 / 196 (same columns as the
    last table row).

  Rejected: an overlap guard on the fit (SBFSEMpy's proposal), which adds a rule to keep a step that does not improve
  the answer. V2 `CorrectionMode` keeps `NEIGHBOR = 0` (the default) and `NONE = 1`; 2 and 3 are reserved (service
  1.5.0). The fit itself stays in `AnnotationVizLib` (`MorphologyGraph.CurveFitProcesses`) as an opt-in MonogameTestbed
  option (`--correction curvefit` or `all`). The testbed default is now `neighbor`, matching the service. Where:
  `Servers/GrpcSectionCorrectionService`, `gRPC_Protos/SectionCorrection/section_correction_v2.proto`,
  `Clients/MonogameTestbed/Program.cs`; Slack #ai thread 1792781373.698579.

- **2026-10-27 Service 1.5.2 restores the CurveFit pass, for a damaged-sections-only test.** Why: SBFSEMpy's split of
  the 11-cell run by damaged section (each block's `NonStosSectionNumbers`) showed the fit adding overlap there (194
  pairs, mean 0.699 field to 0.763 field + fit; 81% from RPC1 c2628) and not elsewhere (7,153 pairs, 0.794 to 0.792).
  Deciding whether a damaged-sections-only fit is worth keeping needs fit positions for more cells, and 1.5.1 computed
  none. James Anderson asked for the code back as 1.5.2 rather than a second 1.4.0 instance. The proto is unchanged
  (0 NEIGHBOR default, 1 NONE, 2 and 3 reserved), so the fit is a raw integer outside it: 4, field then CurveFit (0 before 1.5.0),
  because 0 cannot carry both. 2 and 3 stay the field, as in 1.5.1: production SBFSEM-tools still sends them, and
  mapping 3 back to its 1.4.0 meaning (fit alone) would have moved four volumes from the field to the fit at cutover. Unset or 0 is still the field. This is a measurement aid; the decision on a limited fit is
  pending. Where: `Servers/GrpcSectionCorrectionService/SectionCorrectionsServiceV2.cs` (`ToFitMode`),
  `CorrectionServiceCore.cs`; Slack #ai thread 1792781373.698579.

- **2026-10-26 Link fill colours are softened in OKLCH, 75% chroma; marker numbers use the same colour.** Why: the
  earlier RGB fade toward grey (55% saturation, entry below) shifted how colours look: a burnt orange down link,
  RGB (255, 165, 27), became a yellowish tan (220, 170, 94), which the user disliked, and marker numbers took the same
  blend. Scaling OKLab $a, b$ by 0.75 keeps OKLCH lightness and hue (Ottosson 2020): that orange becomes (239, 172, 91),
  red (230, 75, 60), the up-link blue (127, 0, 255) becomes (119, 65, 219). Overlapped-link marker numbers use exactly
  the link fill colour (`FillColorFor`). Rejected: restoring the raw colours (the user chose the original hue, softer).
  Where: `LocationLinkView.Soften`, `FillColorFor`, `OverlappedLocationLinkView`.

- **2026-10-26 Location link rendering mirrored from Legacy** (VikingLegacy `a892564c`, `5291eb09`, `31b45d9c`,
  `4101c96a`, `d455fb72`, `4cffd674`, `1ef4e036`; their entries follow, from VikingLegacy's notes). Left out because
  the gRPC client lacks the features they hook into: Del/Insert hiding of links (no `TemporarilyHiddenAnnotations`)
  and the label outline preference, so link end labels are always black text with a white outline, Legacy's
  default. Where: `Clients/Viking/WebAnnotation/View/LocationLinkView.cs`.
- **2026-10-26 Links re-point when an adjacent location becomes hidden or visible.** Why: link ends are decided when
  a link view is built, so a link kept ending in empty space (or on the covering location) until the section's links
  were rebuilt. The only places a location becomes hidden or visible are where an overlapped link is added to or
  removed from the section's tracker, so there the section's other links touching that adjacent location are removed
  and re-added. This runs after the tracker's write lock is released, and rebuilt links do not re-point their own
  neighbours, so it cannot cascade. Where: `SectionLocationLinkAnnotationsViewModel.RepointLinksOfAdjacentEnds`.

- **2026-10-26 Close links are more transparent.** Why: markers close together give a short link that barely narrows
  and never fades, drawn as a large dark block over the image (user, RC1 screenshot). The deepest a link fades is
  $t^* = \operatorname{clamp}(g / (r_A + r_B), 0, 1)$ with $g = L - r_A - r_B$ the gap between the markers' edges (where
  both ends' fade depths meet). Opacity is scaled by $\operatorname{lerp}(0.5, 1, t^*)$: a link that fades fully
  ($g \ge r_A + r_B$) is unchanged, one whose markers touch is drawn at half opacity, hovered or not. Where:
  `LocationLinkView.ClosenessOpacity`.

- **2026-10-26 A link to a hidden adjacent location ends on the location covering it.** Why: an adjacent location
  that overlaps a location it links to on the viewed section is not drawn (only the up/down symbol inside that
  location), so a link to it from elsewhere ended in empty space (user, RC1 screenshot). The end is now drawn at that
  covering location, found with the same shape intersection that hides it (`OverlapShape`); the colour, sections and
  labels still describe the real ends. Where: `LocationLinkView.CoveringLocation`.
- ~~**2026-10-26 Link fill colours keep 55% of their saturation.**~~ Superseded 2026-10-26 by the OKLCH entry above. Why: the direction colours (pure red toward a lower
  section, blue toward a higher one) looked harsh at the new opacity. Each channel moves toward the perceived
  brightness $0.3R + 0.59G + 0.11B$ by 45%, so brightness, opacity, border and shadow, and with them visibility, are
  unchanged. Rejected: lowering opacity, which makes links harder to see. Where: `LocationLinkView.Desaturate`.

- **2026-10-26 Links to drawn lines anchor on the line, as wide as the partner's end.** Why: a line or open curve end
  was a circle at its centroid half the line's length across, unrelated to what is drawn. The end now leaves the drawn
  line (polyline vertices, or the open curve smoothed as its view smooths it) at the point $Q$ nearest the partner, from
  the line's edge: radius $r = \max(w/2, r_\text{partner})$, centre $Q + d\,(w/2 - r)$. Rejected (user's choice): a
  bridge only the line's width $w$ thick, which fades out within a line width (the fade runs one end radius) and is too
  small to label. Open curves on an adjacent section are drawn as that half-length circle, so the link uses it there.
  Where: `LocationLinkView.AnchorOnLine`, `AnnotationViewFactory.AdjacentOpenCurveMarkerCircle`.

- **2026-10-26 Links to polygons anchor on the outline.** Why: a link end was the circle at the polygon's centroid with
  the equal-area radius $\sqrt{A/\pi}$, so on a long cell the link's wide end lay under the fill and on a thin cell the
  link began in empty space; adjacent-section polygons are drawn as `CalculateInscribedCircle` (centroid to nearest
  vertex), which the link did not use either. Now the viewed-section end is a circle just inside the point where the
  link's line last leaves the outline, sized by the cell's thickness there (formula above), and the adjacent end is the
  drawn marker circle (since 2026-10-26 the maximum inscribed circle, entry above). Rejected: the centroid-to-nearest-vertex "inscribed" radius for the viewed end (about half the
  diagonal of a rectangle, wider than the cell); the first crossing in list order (`IntersectionPoint`), which is not the
  exit on concave shapes. Where: `LocationLinkView.AnchorOnOutline`, `AnnotationViewFactory.AdjacentPolygonMarkerCircle`.
- **2026-10-26 Link middles are invisible and unpickable until hovered; hover fades over 350 ms.** Why: user feedback on
  RC1 that links cluttered the view between cells. Opacity falls linearly from each marker's edge to 0 one radius on; a
  link at rest cannot be picked where it is below a quarter of its end opacity ($t > 0.75$). Hovering draws it fully and
  makes all of it pickable so the mouse can follow it; the look and the end labels fade at a constant rate over 350 ms,
  so a reversed fade takes the time already spent. Where: `LocationLinkView` (`IsMouseOver`, `HoverAmountAt`).
- **2026-10-26 Fade measured from each marker's edge by its own radius.** Why: the earlier fade by how far the width had
  narrowed from the larger radius started the small end of a link between unequal markers already faded. Where:
  `FadeDepthAt` / `PinchedFadeDepth`.
- **2026-10-26 Link end labels sized in volume space, 0.16 of the marker radius.** Why: fixed screen-pixel labels did not
  zoom with the image as location labels do (user, RC1 location 986804); 0.16 keeps a seven-digit ID inside the strong
  part of the link. Labels under 6 screen pixels are skipped.
- **2026-10-22/26 Link border and shadow colours bypass the HCL blend.** Why: `BlendHCLColorOverBackground` returns the
  background for a colour of zero luma and keeps the background's luma when its luma alpha is 0, so the "contrasting"
  black or white border came out as the image itself or a grey at the image's brightness and barely showed. Border and
  the new outside shadow (5 px, black around light links, white around dark) are composited as plain RGB; the fill keeps
  the luma blend it had at alpha 64 ($\alpha_\text{luma} = 0.75$) so raising opacity does not change its colour. Where:
  `PinchedLineShaders.fx` `MyPSPinchedHSV`.

- **2026-10-26 Overlapped-link marker numbers use the toned link colour.** Why: the section number on a down marker
  was the saturated direction colour, which came out yellow; the user wants the burnt orange of a down link body (as
  Legacy draws it). The label is now `FillColorFor(GetLocationLinkColor(ColorForStructure(parent), span, direction))`
  (originally the RGB `Desaturate`; see the OKLCH entry) at full opacity, so up markers take the toned up-link colour too. Link bodies are drawn in it too since
  Legacy 4cffd674 was mirrored (entry below). Where: `OverlappedLocationLinkView` constructor, `LocationLinkView.FillColorFor`.
- **2026-10-26 Polygon labels use the maximum inscribed circle or a rotated run, never the centroid circle.** Why: the
  old "inscribed circle" (centroid, radius to the nearest vertex) lies outside C-shaped cells and cells with holes, is
  tiny on long processes, and pokes past edges between vertices (user report). The same circle placed adjacent-section
  markers and translate targets, so `Polygon.InscribedCircle` and `CalculateInscribedCircle` now return the maximum
  inscribed circle and the centroid version is deleted. Decisions: angles −75° to 90° in 15° steps plus the principal
  axis of the second moments of area, all in (−90°, 90°] so text never reads upside down and a vertical process reads
  bottom to top; a tilted run scores 0.9 so near-level text snaps to level; the circle is kept unless a run beats it by
  25%, so round cells look as before, only correctly centred; in a run down-links sit at the left end and up-links at
  the right (user's convention), in the circle up-links above and down-links below as before; markers that do not fit
  go to the next-deepest free spots, as Sam2Trainer `seed.py interior_points` picks seeds. A raster cell counts as
  inside only if the scanlines at its bottom, middle and top all cover it, and only the run the fit picks is checked
  against the segments, best score first until no unchecked run could win. Measured 2026-10-26 on synthetic smoothed
  cells: 2.7 ms (round, 300 vertices), 7.3 ms (process, 600), 11 ms (process, 2000) for the circle, runs and first fit
  on the background `Initialize`; a refit when links change takes under 0.5 ms. Rejected: NTS
  `MaximumInscribedCircle` (no client project references NTS); the nornir and Sam2Trainer raster routines (Python).
  Where: `Geometry.Core/Algorithms/LabelLayout.cs`, `Clients/Viking/WebAnnotation/View/PolygonLabelSet.cs`.
- **2026-10-26 Labels and link markers are laid out together, after smoothing.** Why: the fit depends on how many links
  go up and down, and the links setter could run before the background `Initialize` and cache a circle from the
  unsmoothed polygon. The setter now stores the links and `Initialize` applies them, under a lock. Where:
  `LocationPolygonView`, `LocationClosedCurveView`.

- **2026-10-22 Section corrections: detect change per section, rebuild the whole group.** The joint registration fit
  (one sparse least-squares solve over every section's shift, process second differences driven to zero) couples all
  sections, so one edited section can move every other section's field; an incremental update of only the changed
  sections and the sections their chains reach is not valid for it. Each publish stores a per-section fingerprint
  (location rows, newest edit, ID and parent sums, link rows and sums, stos file time); a check that finds any section
  changed rebuilds the group in full once the newest edit is `QuietMinutes` old, and an unchanged group is left alone.
  A weekly cron forces a rebuild for changes a fingerprint cannot see. Rejected: the per-section incremental builder,
  because it rebuilt with the leave-one-out residual field, which measured each section against uncorrected
  neighbours and made held-out structures less smooth in every volume (median held-out second difference, raw to joint
  fit: RPC1 70.5 to 62.2 nm, RPC2 55.9 to 55.0, RC1 92.9 to 75.0, RC2 255.5 to 79.2; commit 8c37e7cf). A single
  annotation watermark was also rejected as the change test: it cannot see a deleted location. Where:
  `Servers/SectionCorrectionBuilder/CorrectionPublisher.cs`, `Servers/SectionCorrection/SectionFingerprint.cs`.

- **2026-10-25 The segmentation server encodes prompts; shapes travel as geometry.** The fine-tuned model is not a
  generic SAM2: it learned one prompt language, the exact `mask_input` values of its training (v6: seed `+10 * c(d)`
  with the edge-confidence curve, neighbours `-10`; v7 adds the depth-relative taper). A client that rasterized circles
  could not apply the served model's `circle_diameter_scale` or a radius- or depth-relative trust, so circles, lines and
  polygons now reach the server as shared `viking.geometry.v1.Shape`s and the server encodes them for the model it
  serves (its manifest's `prompt_encoding`). Roles are inferred, not sent: object or neighbour is which `Feature` a
  shape is in; an object geometry shape is softened, a painted bitmap used as drawn. Every request returns one result
  per island of its object prompt (v6: connected components of the prompt; v7: trusted cores), with no switch to get
  it wrong. Removed as unused (by grep of every client): full-viewport images and their RPCs, multimask output,
  labeled images, rectangles, response-content flags, capability negotiation, and client-side background mask rounds
  (the server rasterizes neighbour shapes and asks for them as growth reaches new cells). The same shared geometry
  replaces WKT in the annotation API (one `Shape` per annotation, its type following from the shape) and the
  section-correction API.
- **2026-10-25 Circle seeds get a radius-relative taper (planned, v7).** Annotators often oversize circles; the trained
  seed encoding (v6) is a full-strength disc, so an oversized circle's rim is fed as object. Evidence: the client ignores
  the model contract's `circle_diameter_scale` and sends the full disc; the eval-only comparison on v5.4 is in
  Sam2Trainer `docs/circle_seed_trust_eval.md`. Proto change for circle seeds to follow.
- **2026-10-25 Tile uploads finish when the caller stops waiting.** The server encodes a tile it has started receiving
  regardless of the client, so cancelling an upload only made the next request send the same tile and the server encode
  it twice (seen as byte-identical image ids 1374/1376, 1375/1377). Uploads now run under the session's lifetime;
  callers stop waiting instead. Background prefetch starts no new tile while any request uploads tiles it waits for.
- **2026-10-24 Stream timeout resets on progress.** One 300 s deadline for a whole SegmentTilesStream cut off large
  cells (about 100 tiles) while the server was still working. Now 5 minutes with nothing moving either way (any server
  message, tile upload or answer restarts it), capped at 30 minutes. UploadTile 30 s to 120 s and the status query 5 s
  to 60 s, after the server held uploads in its queue for about 30 s under GPU contention.
- **2026-10-24 Paint segmentation: a newer mask supersedes the running request.** The first stroke's request ran to the
  5-minute deadline while every later stroke waited behind it; now a mask change cancels it and sends the current mask.
  Resegment starts with the dilate/erode mask edit.
- **2026-10-24 Auto-segment queue: cursor first, live re-pick, 500 ms settle, three requests in flight, tile prefetch
  within 4 radii.** Measured on 32 circles (v5.4): model time about 0.5 s per request but 4-9 s per request needing
  tiles, so the server was about 20% busy; tile round trips, not inference, bounded throughput. Prefetch moves the
  server's per-tile embedding ahead of the requests.
- **2026-10-24 Tiles stay lossless PNG (level 6, about 6% smaller than BestSpeed).** On 20 real tiles: PNG level 6 676
  KiB vs 716 KiB as sent, WebP lossless 639 KiB, JPEG q90 419 KiB (min PSNR 37.0 dB, max error 22 grey levels), WebP q90
  385 KiB (41.7 dB). Lossy rejected for now: the server's embedding cache is keyed on decoded pixels (a lossy switch
  re-encodes every tile) and mask quality under lossy input is unmeasured.
- **2026-10-24 Tile renders cancel only themselves.** Each render cancelled the previous one, so overlapping requests
  cancelled each other's captures ("Capture failed" on first renders).

## Discussion points

- **2026-10-24 A painted seed mask grows past itself.** Probe of the deployed v5 server (model id `v5`, image built
  2026-10-20; its prompt encoding was not recorded (verify)) on RC2 section 643, ds 1, `TEM.Leveled`, the client's own
  captured tiles, one interior click at mosaic (43143, 55203). It replayed the client's `SegmentTilesStream` (start,
  FOREGROUND disc tiles with role SEED, empty BACKGROUND tiles, tiles uploaded when asked) at mask threshold 0.5 and
  measured the returned mask in mosaic pixels:

  | Prompt | Seed area | Result area | Result extent |
  |---|---|---|---|
  | Click only | 0 | 350,903 | 1079 x 835 |
  | Click + seed disc r = 80 | 20,106 | 333,560 | 1095 x 656 |
  | Click + seed disc r = 300 (4 tiles) | 282,743 | 432,568 | 1083 x 1089 |

  A seed is a start, not a ceiling: the small disc grew about 17-fold, and the large one, crossing four tiles, grew past
  its 600 px diameter on both axes while the server fetched two more tiles. The seed also reshapes the answer (the
  small disc's result is 5% smaller and shorter than the click's). Seed pixels are always object
  (`Servers/SegmentationServer/segmentation_server/segmentation_server/tile_growth.py`, `GrowthWalk`), so the large disc's
  result exceeds the click-only object by 81,665 px: paint past a cell's edge stays in the answer, and the model cannot
  correct over-painting. So a user's "segmentation stops at my paint" was not the encoding. The causes were on the
  client, where paint beyond the canvas (the starting view plus half of it on each side) was dropped without being sent
  (fixed in VikingLegacy `f8b7f6c1`), and in latency: the deployed image made the client re-upload 3-5 tiles the server
  already held on every request (1.3 of 3.4 s), which `fc19a525` removes. Limits: one object on one section, no ground truth, and the probe script was not
  kept in the repo.
- **GPU sharing between training and serving.** With a training run on the same GPU, a tile embedding took 20-30 s
  instead of 0.3-0.5 s, uploads waited up to 90 s, and a 93-tile mask could not finish within 5 quiet minutes. Serving
  latency depends on GPU isolation more than on client tuning.
- **Annotator circles are oversized more often than measured.** The Oct 13 measurement compared circles with the
  equal-area radius of the linked polygon, which exceeds the inscribed radius for elongated cells, so it understated how
  large circles are relative to the cell (Sam2Trainer `docs/training_plan_assessment.md`, 1.3).

## References

- Agafonkin, V. (2016). Polylabel: a fast algorithm for finding the pole of inaccessibility of a polygon. Mapbox,
  https://github.com/mapbox/polylabel (verify blog date). Used for: the branch and bound behind
  `PoleOfInaccessibility.Maximize`.
- Anderson, J. R., Jones, B. W., Yang, J.-H., Shaw, M. V., Watt, C. B., Koshevoy, P., Spaltenstein, J., Jurrus, E.,
  Kannan, U. V., Whitaker, R. T., Mastronarde, D., Tasdizen, T., Marc, R. E. (2009). A computational framework for
  ultrastructural mapping of neural circuitry. *PLoS Biology* 7(3), e1000074. (verify) Used for: the connectome
  workflow Viking serves.
- Anderson, J. R., Mohammed, S., Grimm, B., Jones, B. W., Koshevoy, P., Tasdizen, T., Whitaker, R., Marc, R. E. (2011).
  The Viking viewer for connectomics: scalable multi-user annotation and summarization of large volume data sets.
  *Journal of Microscopy* 241(1), 13-28. (verify) Used for: Viking's design.
- Bajaj, C. L., Coyle, E. J., Lin, K.-N. (1996). Arbitrary topology shape reconstruction from planar cross sections.
  *Graphical Models and Image Processing* 58(6), 524-543. (verify) Used for: `MorphologyMesh` contour-to-mesh tiling.
- Barry, P. J. & Goldman, R. N. (1988). A recursive evaluation algorithm for a class of Catmull-Rom splines. *SIGGRAPH
  Computer Graphics* 22(4), 199–204. doi:10.1145/378456.378511 (verify). Used for: the pyramid evaluation in
  `FitCurveSegmentWithTValues`.
- Catmull, E. & Rom, R. (1974). A class of local interpolating splines. In *Computer Aided Geometric Design*, Academic
  Press, 317–326 (verify). Used for: the interpolating curve through annotation control points.
- Garcia-Castellanos, D. & Lombardo, U. (2007). Poles of inaccessibility: a calculation algorithm for the remotest
  places on Earth. *Scottish Geographical Journal* 123(3), 227–233. doi:10.1080/14702540801897809 (verify). Used for: the
  pole of inaccessibility as the label anchor.
- Ravi, N., et al. (2024). SAM 2: Segment Anything in Images and Videos. arXiv:2408.00714. Used for: the segmentation
  server's model.
- Yuksel, C., Schaefer, S. & Keyser, J. (2011). Parameterization and applications of Catmull-Rom curves. *Computer-Aided
  Design* 43(7), 747–755. doi:10.1016/j.cad.2010.08.008 (verify). Used for: centripetal parameterization ($\alpha = 0.5$),
  which avoids cusps and self-intersections within a span.

## Open questions for a paper

- Auto-segment throughput and acceptance with the prefetch and pipelining, measured on an isolated GPU.
- Whether the tapered circle seed (v7) reduces corrections of auto-segmented outlines in production.
