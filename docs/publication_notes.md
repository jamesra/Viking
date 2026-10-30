# Publication notes: VikingLegacy

The WCF Viking client (released Viking, VikingAU, MonogameTestbed) on the `Legacy` branch of the Viking repo. Material
for future papers: citations, formulas, algorithmic decisions and the reasoning behind them. Entries made before
2026-10-26 are in Viking's notes (`D:\src\git\Viking\docs\publication_notes.md`, `dev`). Index of all projects:
`~/.cursor/publication-notes.md`. Format and rules: the `publication-notes` skill.

## Methods and contributions

- **Polygon label placement** (`Geometry.Core/Algorithms/LabelLayout.cs`, `InscribedRuns.cs`, `PoleOfInaccessibility.cs`;
  client `Clients/Viking/WebAnnotation/View/PolygonLabelSet.cs`): a polygon or closed curve's ID, structure label, tags,
  parent ID and overlapped-link markers go either in the true maximum inscribed circle (pole of inaccessibility, by
  polylabel's branch and bound over signed distance to exterior and hole segments) or along the largest inscribed
  oriented rectangle ("run") at 37 angles (every 5° and the principal axis; 13 at 15° steps before 2026-10-29),
  whichever allows the larger ID font. Runs come from rasterising the rotated
  polygon to a 128×128 grid and the histogram-and-stack maximal-rectangle pass, keeping the (width, height) Pareto front
  so any text aspect fits without rasterising again; the candidate chosen is verified against the real segments
  (Liang–Barsky clip) and shrunk by 10% up to four times if a raster cell hid an edge. Markers that do not fit go one at a
  time to the point of greatest clearance from outline, text and earlier markers, found with the same branch and bound
  on a 1-Lipschitz clearance function.

- **Pen outlines as Catmull-Rom control points** (`Geometry/CurveFitting/CatmullRom.cs` `RefineControlPoints`,
  `Geometry/CurveFitting/ControlRingCurve.cs`; client `Clients/Viking/WebAnnotation/UI/Actions/ControlPointCut.cs`,
  `PenRegionCut.cs`): a pen stroke becomes the control points of a centripetal Catmull-Rom curve. Control points are chosen
  by insertion only: starting from a fixed set, the reference vertex that best closes the largest gap between curve and
  reference is inserted until every span is within the tolerance both ways, and spans are re-checked until a pass inserts
  nothing (an insertion changes the neighbouring spans' tangents). An edit splices the pen stretch into the stored control
  ring: the crossings are found on the drawn curve, the kept control points are reused exactly (in section space, so they
  never round-trip through the transform), and refinement adds only stroke or old-curve points near the splice. A stroke
  crossing the outline more than twice (cutting a bridge out of a cell) or crossing two rings removes or adds the region it
  encloses; the boolean runs on the drawn curves and each resulting ring is refitted the same way, keeping stored points.

## Formulas

- **Centripetal Catmull-Rom** (`CatmullRom.FitCurveSegmentWithTValues`): knots $t_{j+1} = t_j + \lVert P_{j+1} - P_j \rVert^{\alpha}$
  with $\alpha = 0.5$, evaluated between $P_1$ and $P_2$ by the Barry–Goldman pyramid of linear interpolations.
- **Control point acceptance** (`RefineControlPoints`): for each span between control points $i_s, i_e$ with curve
  samples $C$ and reference polyline $R_{[i_s, i_e]}$, accept when
  $\max\left(\max_{k \in (i_s, i_e)} d(R_k, C),\ \max_{c \in C} d(c, R_{[i_s,i_e]})\right) \le \tau$, with
  $\tau = \min(\text{Pen Simplify Threshold},\ 2)$ volume pixels for pen strokes.

- **Label font score** (`LabelLayout.RunFontSize`): for content whose rows at an ID font of 1 are $t_w$ wide (widest
  row, each row's width times its scale) and $t_h$ tall (sum of row scales: structure label 0.5, tags 0.25, ID 1, parent
  0.5), a run of width $W$ and height $H$ holding $k$ end markers of diameter $F$ allows the ID font
  $F_k = \min\left(\frac{H}{1.1\,t_h}, \frac{W}{k + 1.1\,t_w}\right)$; a run at angle $\theta$ scores
  $s_k = F_k / g(\theta)$ with required gain $g(\theta) = 1 + G\,\lvert\sin\theta\rvert$ (since 2026-10-29; before, $F_k$
  level and ~~$0.9\,F_k$~~ tilted). $G = (1 - w)\cdot 1.0$ for the Label rotation preference $w \in (0, 1]$, default
  $w = 0.5$, $G = 0.5$; $w = 0$ ("Never") considers level runs only. The circle of radius $R$ allows $F_c = R/2$ (the
  circle labels' existing size). The best-scoring run is used when its text-only font
  $F_0 \ge \max(1.25,\ g(\theta))\,F_c$; ~~it then keeps the largest $k \le n$ markers with $s_k \ge 0.6\,s_0$, split
  between down and up links in proportion~~ (superseded 2026-10-29 by the marker packing below). Free-space markers
  maximise $f(p) = \min(d_P(p),\ d_T(p),\ \min_j(\lVert p - c_j\rVert - r_j))$, with $d_P$ the signed distance to the
  polygon, $d_T$ the signed distance outside the text box and $c_j, r_j$ the markers placed so far; radius
  $\min(F/2, 0.95 f)$, at least $0.1F$, and since 2026-10-29 at most $0.95\,d_P$ (at the inscribed centre if $d_P \le 0$).

- **Marker packing** (`LabelLayout.PackingOrder`, `EndBlockSlot`, `AddCircleRows`, since 2026-10-29): a packing is
  $(r, s)$, up to $r$ hex-packed rows per direction of markers at scale $s$ of full size, tried in the order
  $s \in (1, 0.75, 0.5)$, then $r = 1 \ldots 4$. In a run (pitch $d = sF$, radius $0.45d$) marker $j$ of an end's $m$
  sits $u_j = \lfloor j/r' \rfloor + \tfrac12[\,j \bmod r' \text{ odd}\,] + \tfrac12$ pitches in from the end and
  $v_j = (\tfrac{r'-1}{2} - j \bmod r')\tfrac{\sqrt3}{2}$ across, $r' = \min(r, m)$. The ends take
  $w = s(\max_j u^{\downarrow}_j + \max_j u^{\uparrow}_j + 1)$ font lengths and $h = s((r'-1)\tfrac{\sqrt3}{2} + 0.9)$
  font thicknesses, so $F = \min\!\left(\frac{H}{1.1\,t_h}, \frac{W}{w + 1.1\,t_w}, \frac{H}{h}\right)$. The first
  packing with $F/g(\theta) \ge 0.6\,s_0$ whose markers all clear the outline is used; failing all, $k = n-1 \ldots 1$
  markers at $s = 0.5$, the rest to free space. In the circle of radius $R$, row $k$ is an arc of radius
  $\rho_k = \tfrac{7}{12}R + \rho_m + k\sqrt3\,\rho_m$, $\rho_m = sR/6$ (at $s = 1$ row 0 is circle annotations'
  $0.75R$ arc), with pitch $\varphi = 2\arcsin(\rho_m/\rho_0)$ shared by every row and lattice angles $t\varphi$,
  $t \in \mathbb{Z} + \tfrac{p_k}{2}$, $|t| \le (\pi/\varphi - 1)/2$ (each marker stays in its half), the phase $p_k$
  alternating so each row sits in the gaps of the one inside it; up-links above the centre, down-links mirrored below.
  A row is used only if every marker in it clears the outline and holes. Score factor $m = 1 - 0.25\,(1 - \bar s)$,
  $\bar s$ the mean marker scale with a free-space marker counting 0.25; Fit compares $F_0\,m_{run}$ with
  $\max(1.25, g(\theta))\,F_c\,m_{circle}$~~, and hysteresis uses the same products~~ (superseded 2026-10-29: hysteresis removed, user).

- ~~**Label placement hysteresis** (`LabelLayout.Fit(content, G, previous)`, `Advantage`, since 2026-10-29): with the
  new best placement $B$ and the label's previous placement $P$ (from the same layout, and still holding the content:
  same row scales, same up and down link counts, $1.1\,t_w F_P \le$ its line width), $P$ is kept when
  $A(B, P) \le 1 + h$, $h = 0.05$. The advantage follows the rules Fit decides by: between runs
  $A = \frac{F_0(B)/g(\theta_B)}{F_0(P)/g(\theta_P)}$; a run over the circle $A = F_0(B) / (\max(1.25, g(\theta_B))\,F_c)$; the
  circle over a run $A = \max(1.25, g(\theta_P))\,F_c / F_0(P)$; $F_0$ is the text-only font of the run's rectangle for
  the current content. A tilted $P$ at $G = \infty$ has $A = \infty$, so "Never" always levels the text.~~
  superseded 2026-10-29: removed, user (see the decision of that date).
- **Label switch easing** (`Geometry.Core/Algorithms/LabelEasing.cs`): each text row moves from its shown pose
  $(c_0, \theta_0, f_0)$ to its new one $(c_1, \theta_1, f_1)$ with $s = 1 - (1 - t/T)^3$, $T = 150$ ms:
  $c = c_0 + s\,(c_1 - c_0)$, $f = f_0 (f_1/f_0)^s$, $\theta = N(\theta_0 + s\,\delta)$ where $\delta$ is
  $\theta_1 - \theta_0$ or that minus $\operatorname{sign}\cdot\pi$, whichever is shorter (direct on a tie), and $N$
  maps to $(-\pi/2, \pi/2]$; text at $\theta$ and $\theta + \pi$ lies on the same line and the renderer never draws
  it upside down, so the turn is at most 90°.

- **Location link fade depth** (`Clients/Viking/WebAnnotation/View/LocationLinkView.cs` `FadeDepthAt`, shader
  `PinchedFadeDepth` in `Clients/MonogameXNAGraphicsShared/Content/PinchedLineShaders.fx`): for a point $x$ along a link of
  length $L$ between end circles of radii $r_A, r_B$,
  $t(x) = \operatorname{clamp}\left(\min\left(\frac{x - r_A}{r_A}, \frac{L - x - r_B}{r_B}\right), 0, 1\right)$; drawn
  opacity $\alpha(x) = \alpha_0 \cdot \operatorname{lerp}(1, w, t(x))$ with waist opacity $w = 0$ at rest and $1$ when hovered.
- **Polygon link anchor** (`LocationLinkView.AnchorOnOutline`): with centroid $c$, partner centre $p$, exit point $E$ =
  the crossing of segment $cp$ with the outline farthest from $c$, $d = (p - E)/\lVert p - E \rVert$, entry $N$ = the
  nearest crossing behind $E$ along $-d$, the end circle has radius
  $r = \min(\operatorname{dist}(\tfrac{N + E}{2}, \partial P), \sqrt{A/\pi})$ and centre $E - r\,d$.
- **Inverse of an annotation edit** (`LocationVolumeGeometry.ToMosaic`, since 2026-10-30): with section-to-volume map
  $T$ and the forward five-point mean scale $s_T(c, r) = \tfrac14 \sum_{k=1}^{4} \lVert T(c + r e_k) - T(c) \rVert / r$
  ($e_k$ the east, north, west and south unit vectors), a circle edited in volume space as $(c_v, r_v)$ is saved as
  $c_m = T^{-1}(c_v)$ and the fixed point $r_m \leftarrow r_v / s_T(c_m, r_m)$, started from the inverse five-point
  estimate; a line width $w_v$ is saved as $w_m \leftarrow w_v / s_T(\bar{x}_m, w_m/2)$ ($\bar{x}_m$ the centroid of the
  mosaic shape; a closed curve samples at its own radius instead). Control points map pointwise by $T^{-1}$, all or none.

## Algorithmic decisions

- **2026-10-30 The stored volume cache always holds the volume's default transform.** Decision (James, approved design):
  mosaic is the truth and is what is saved; `VolumeShape` (and the `VolumeX`/`VolumeY` the server computes from it) is a
  cache, written by every save and by the refresh after a `.stos` change (Update Volume Positions, VikingAU) as the
  forward owner's geometry under the volume's *default* section-to-volume transform, whatever transform is on screen.
  Why: server queries, OData and export read the cache, and before this a save under another transform (or a refresh run
  with a picked transform) left sections, even locations on one section, in different spaces. The cache value is
  `LocationVolumeGeometry.CacheShape`: a circle's exact five-point-mean circle, not the smoothed ring's inscribed circle,
  which the old refresh wrote about 2% smaller than the client save. Policies: a section the default has no entry for (the
  reference section) maps by identity, as the viewer and `MappingManager` place it; a default transform that did not load
  while others did is never replaced by identity (the save keeps the transform it was made under, the refresh refuses);
  a location the default cannot map exactly gets the owner's one failure policy (extrapolated), not "no cache", because
  the WCF server fills a missing `VolumeShape` with a circle of the *mosaic* radius at the old `VolumePosition`
  (Viking `Servers/AnnotationService/.../LocationExtensions.cs`), wrong for every non-circle type. Rejected: sending no
  `VolumeShape` (that server fallback), caching under the active transform (inconsistent basis), keeping the smoothed
  circle (two cache values for one circle). Where: `SqlGeometryAnnotationExtensions/VolumeCache.cs`,
  `LocationObjExtensions.SetShapeFromGeometryInSection`, `VolumeViewModel.GetDefaultSectionToVolumeTransform`,
  `Clients/Viking/WebAnnotation/VolumePositionUpdateJob.cs`, `Clients/VikingAU/Program.cs`.

- **2026-10-30 Annotation edits are saved by one inverse owner, solved against the forward rule.** Why: mosaic is the
  truth and every view draws the forward owner's mapping, so an edit made on screen must save the mosaic geometry whose
  forward mapping is what the user saw. The one-step inverse five-point mean is not the inverse of the forward one under
  shear or unequal axis scales (for $A = \operatorname{diag}(2, 1)$ the forward scale is $1.5$, the inverse $0.75$, so a
  saved circle would redraw 12.5% larger), and copying a volume line width into the mosaic `Width` ignored the scale
  altogether. Rejected: keeping the user's volume shape as the cache (it drifts from what a changed `.stos` draws) and
  saving partly mapped shapes (an edit with a point outside the transform is refused with the reason). Where:
  `SqlGeometryAnnotationExtensions/LocationVolumeGeometry.Inverse.cs`, `LocationObjExtensions.TryApply`.

- **2026-10-29 Label placement hysteresis removed; the eased move, 5° angles and 50 ms relayout throttle stay.** Why:
  James, after using it: the jitter the hysteresis was meant to hide (labels flipping between level and tilted near a
  tie while the Label rotation slider moves) is a non-issue, so every fit now takes the best placement and the views
  keep no previous placement. Removed: `LabelLayout.Hysteresis`, `Fit(content, G, previous)`, `Holds`, `Advantage`,
  `LabelPlacement.Owner`, `LabelContent.SameRowScales`/`SameMarkerCounts`, the polygon and closed-curve views'
  `labelPlacement`, and the hysteresis tests (the FsCheck property now checks a plain fit for changed content). Kept:
  the 150 ms eased move (a polygon view eases from its previous labels whenever it rebuilds in the same layout), the 5°
  run angles, and `AnnotationMenu.RequestLabelRelayout`'s 50 ms throttle, which bounds relayout cost while dragging, not
  jitter. Where: `Geometry.Core/Algorithms/LabelLayout.cs`, `Clients/Viking/WebAnnotation/View/PolygonLabelSet.cs`,
  `LocationPolygonView.cs`, `LocationClosedCurveView.cs`.

- **2026-10-29 Link markers stay inside the polygon: a staggered second row, then smaller markers, before free space.**
  Why: James's RC2 screenshots of cell C 45733 (location 386775, section 645, two links down to 643 and three up to
  646). Level, the run's ends held only four markers: five in one row left $F_5 = 109.8$ against $0.6\,s_0 = 112.8$
  (reproduced from the location's control points smoothed as the viewer does, with Arial row widths), so the fifth
  went to the deepest free spot, the bulb at the cell's far end over 1100 units from the other up-links, where it
  looked lost (he saw it as outside and hidden). The circle placement already put up-links above and down-links
  below (the client drew it with circle annotations' arcs), so the level label in the screenshot was a run, and that
  was the path to fix.
  His rule: stagger a second row by half a marker to save height, then shrink and add rows, then anything else. Now
  (formula above) the cell's level label holds all five in two staggered full-size rows at font 124.7 (was 120.4), and
  the default tilted run still holds one row. Decisions: rows stagger along the run's axis at each end (down left, up
  right) and outward from the text in the circle, so both conventions hold; all circle rows share the innermost row's
  angular pitch with alternating phase, so they interleave without overlap
  ($d^2 = 3\rho_m^2 + 4\rho_a\rho_b\sin^2(\varphi/4) \ge 4\rho_m^2$); the circle's row 0 uses $2\arcsin(\rho_m/\rho_0)$
  rather than circle annotations' $2\rho_m/\rho_0$ (0.8% wider, so neighbours no longer overlap); a row past the
  inscribed circle is kept only where the real outline allows; the smallest marker is half size (`MinimumMarkerScale`:
  a run pitch of half the ID font, a circle radius $R/12$), since a smaller one's section number is unreadable; the
  run's font also obeys the end blocks' height, so a staggered block never sticks out of the run. Marker cost enters
  the score as a factor on both sides of the run/circle test, the run's side still its text-only font $F_0$, so labels
  whose markers are full size on both sides decide exactly as before. Free-space markers are capped at $0.95\,d_P$
  (the old $0.1F$ floor could cross the outline). The circle placement now carries its own slots and polygon views
  draw both kinds from the placement; circle annotations keep their arc code (more than seven links a side still wrap
  there, as before). ~~Hysteresis: a circle kept when the link counts change is replaced by the circle laid out for the
  new counts.~~ (superseded 2026-10-29: hysteresis removed, user.) Rejected: a second circle row inward (it covers the text); markers below half size (unreadable);
  shrinking before staggering (the user's order). Where: `Geometry.Core/Algorithms/LabelLayout.cs` (`PackingOrder`,
  `ChooseEndMarkers`, `EndBlockSlot`, `PlaceInCircle`, `AddCircleRows`, `ClearOfOutline`, `MarkerReadability`),
  `Clients/Viking/WebAnnotation/View/PolygonLabelSet.cs`, `OverlappedLinkCircleView.cs`; tests
  `GeometryTests/LabelLayoutTests.cs` (stagger, shrink, circle halves, the screenshot cell) and
  `GeometryTests/FSCheck/PolygonSpec.cs` (`MarkersStayInsideAndClearOfHoles`).

- **2026-10-29 Polygon labels switch placement smoothly: hysteresis, an eased move, and 5° run angles.** Why: the
  user found the live Label rotation relayout rough, with labels jumping between level and tilted. Three changes,
  all chosen by him. (1) ~~Hysteresis $h = 0.05$ (formula above): a label keeps its placement unless the new best is
  over 5% better by the rule Fit decides with, so a near-tie does not flip as the slider moves; the core returns the
  previous object itself, the view keeps it per polygon and drops it with a new `LabelLayout` (changed outline), and a
  placement that no longer holds the content (longer text, a new link) is never kept.~~ superseded 2026-10-29: removed,
  user. (2) A 150 ms cubic ease-out of
  each text row's centre, angle (the short way modulo 180°, consistent with `LabelView.DrawRotated`'s upside-down flip)
  and font, from where the old labels are shown at that moment, so a relayout mid-animation carries on from the
  current pose; a relayout that keeps the placement continues the running animation unchanged. Circle and run label
  sets have the same rows (ID, structure label, tags, parent), so a circle ↔ run switch animates row by row like any
  other; line widths scale with the font so wrapping does not change mid-move; overlapped-link markers move at once
  (they are pickable geometry). The overlay asks for another frame after drawing labels while any label is easing, and
  is idle otherwise. (3) Run angles every 5° from −85° to 90° (36, plus the principal axis unless within 1° of a step)
  instead of 15°: an arm off the principal axis got text up to 7.5° off its direction. Measured 2026-10-29, net48
  Release, synthetic cells (`round`: 300 vertices, radius 300 with 5% and 3% harmonics; `process`: a bent tapering
  process 1600 long, about 110 to 40 wide, 580 and 1980 vertices; not the 2026-10-26 shapes), median of 30: `LabelLayout`
  constructor 2.1 → 6.1 ms (round, 2.9×), 2.8 → 5.7 ms (process 600, 2.1×), 5.4 → 9.0 ms (process 2000, 1.7×), within
  the 3× and 40 ms budget set beforehand, on the background `Initialize`. `Fit` (four rows, two markers; median of 2000
  across the slider range) went 205 → 670 µs (round) and 166 → 480 µs (process) with 5° alone, because `TryBestRun`
  sorted every Pareto run (thousands for a round cell) for each marker count; it now picks the next best bound by a
  linear scan (verification stops after one or two), and `LabelContent` measures its text size once, so `Fit` is
  16 µs (round) and 14 µs (process), 12× faster than before the change~~; with hysteresis it costs the same~~. Rejected:
  5° only near the principal axis, or a coarse-then-fine search (not needed at these costs); animating the link markers
  (they are hit-tested views rebuilt with the placement); a group transform for circle ↔ run (circle rows are not a
  rigid copy of run rows). Where: `Geometry.Core/Algorithms/LabelLayout.cs` (`Fit`; ~~`Holds`, `Advantage`,
  `Hysteresis`~~ removed), `InscribedRuns.cs` (`StepDegrees`, `Angles`), `LabelEasing.cs`;
  `Clients/Viking/WebAnnotation/View/StructureLabelsBase.cs` (`TransitionFrom`, `DrawLabel`), `PolygonLabelSet.cs`,
  `LocationPolygonView.cs`, `LocationClosedCurveView.cs`, `UI/AnnotationOverlay.cs`; tests
  `GeometryTests/LabelLayoutTests.cs`, `GeometryTests/LabelEasingTests.cs`, `GeometryTests/FSCheck/PolygonSpec.cs`. To
  mirror into Viking `dev` `Clients/` and `Geometry.Core`.

- **2026-10-29 Label tilt costs a gain that grows with the angle; one preference sets it.** Why: a user disliked rotated
  polygon labels. The flat 0.9 tilt score (2026-10-26 entry) let a 30° run beat level text for an 11% larger font, the
  same price as 90°, and charged a principal axis 0.3° off level the full 10%. A run at $\theta$ now scores
  $F / (1 + G\lvert\sin\theta\rvert)$: at the default $G = 0.5$ it must beat level text by 13% at 15°, 25% at 30°, 35%
  at 45° and 50% at 90°, while a long thin process, which gains several-fold along its length, still rotates up to
  vertical. The factor is at most 1, so it keeps the raster bound used to verify runs best first an upper bound. The
  Label rotation preference $w$ (Annotation Preferences → Visuals → Text, "Never" 0 to "Freely" 1, default 0.5) sets
  $G = (1 - w)\cdot 1.0$; at 0 only level runs and the circle are considered. The circle test takes the larger of the
  circle preference and the tilt's required gain, $F_0 \ge \max(1.25, g(\theta))\,F_c$, rather than their product:
  the circle is level text, so a tilted run needs its required gain over it as over a level run, but multiplied
  ($1.25 \times 1.5 = 1.875$ at 90°) a two-line label in a thin vertical process (run font $H/2.2$ against circle font
  $H/4$, ratio 1.82 whatever the length) would always fall back to the circle. Rejected: a separate on/off switch beside
  a strength (the user chose one slider whose minimum is off); a threshold on the angle (a 30° process would then never
  get text along it). Measured 2026-10-29 (synthetic, net48 Release): with the layouts cached, refitting a four-row label
  with two markers costs 150 µs per cell (500 cells, 75 ms on one thread); the live relayout runs it in parallel per
  section, visible sections first, at most every 50 ms while the slider moves. Where:
  `Geometry.Core/Algorithms/LabelLayout.cs` (`RequiredGain`, `RotationGainForSetting`, `Fit`),
  `Clients/Viking/WebAnnotation/UI/AnnotationMenu.cs` (`RequestLabelRelayout`); tests
  `GeometryTests/LabelLayoutTests.cs`, `GeometryTests/FSCheck/PolygonSpec.cs`. To mirror into Viking `dev` `Clients/`
  (same files, e81a41a0).

- **2026-10-28 Correction lattices are sampled by one shared function.** Why: the client's `SectionLatticeField` was an
  older port of the service's sampler. Where a cell had only some corners it went straight to inverse-distance weighting,
  and it placed fallback nodes at cell centres, (g + ½)·pitch, while the service (`SectionVectorField`,
  `NeighborResidualField`) interpolates over the present corners and places nodes at g·pitch. So the shim and
  `CorrectPoints` disagreed near the edges of annotated areas. All three now call `LatticeInterpolation.Sample`. Note:
  with the 0.001 weight threshold the inverse-distance branch only has support within ~31 nm of a node, so in practice a
  point is trusted only in a cell with at least one published corner. Where: `Geometry.Core/Algorithms/LatticeInterpolation.cs`.
- **2026-10-27 Pen tolerance is in volume pixels, capped at 2.** Why: it was Pen Simplify Threshold (3) × downsample, so a
  stroke drawn at downsample 4 could leave the stroke by 12 volume px and at 8 by 24, wider than a membrane (7–10 nm). With
  the cap the drawn curve stays within ~2 px of the fitted stroke at every zoom (measured, below). Trade-off: below the
  hand's jitter the fit keeps the jitter (at downsample 8, 82 samples gave 71 control points). Where:
  `Clients/Viking/NGVV/UI/Command/PenInputHelper.cs` `MaxSimplifiedPathToleranceInVolumePixels`.
- **2026-10-27 Pen edits splice into the stored control points.** Why: Pen Mode edits walked the smoothed display ring,
  Douglas–Peucker reduced it at max(4, 4 × downsample) volume px and saved the survivors as control points, which were
  curved again. Douglas–Peucker leaves very uneven spacing, and a curve through uneven points overshoots outward on convex
  stretches; the whole outline was re-sampled at every edit, so untouched membrane moved too. Rejected: Douglas–Peucker on
  control points (they are curve control points, not polygon vertices); re-simplifying the whole outline after mapping (the
  retrace command did this; error compounds per edit). Where: `ControlRingCurve.TrySplice`, `ControlPointCut`,
  `RetraceAndReplacePathCommand.TrySpliceOutput`.
- **2026-10-27 Control point refinement checks both directions and re-checks neighbours.** Why: the old greedy pass checked
  each span once, measured only from the reference to the curve, and an insertion in the next span changed the accepted
  span's end tangent unchecked (measured: worst 4.47 px at tolerance 3, 15.1 px at tolerance 12). Where: `RefineControlPoints`;
  `IdentifyControlPoints` now builds on it (golden fingerprints in `GeometryTests/Phase7GoldenTests.cs` re-recorded).
- **2026-10-27 Junctions are densified before refinement.** Why: where a stroke meets the old outline at an angle the curve
  rounds the corner, and with only the corner's two neighbours available refinement could not pin it (1.87 px at tolerance 1
  in `ControlPointFittingTests`). Points added along the two segments meeting each junction, at most τ apart, bring it
  within tolerance. Where: `CatmullRomControlPointSimplification.DensifyNextToCorners`.
- **2026-10-27 Multi-crossing strokes cut or add the enclosed region.** Why: cutting a bridge out of a cell needs four
  crossings, and a cut from the outline into a hole crosses two rings; both were refused or cut along one arm only. The
  stroke closed by a straight line between its ends is subtracted (ends outside) or added (ends inside) with SqlGeometry on
  the drawn curves; pieces become choices. Limitation: a closing line that crosses the cell removes more than intended.
  Where: `PenRegionCut`.

- **2026-10-26 Link fill colours are softened in OKLCH, 75% chroma; marker numbers use the same colour.** Why: the
  earlier RGB fade toward grey (55% saturation, entry below) shifted how colours look: a burnt orange down link,
  RGB (255, 165, 27), became a yellowish tan (220, 170, 94), which the user disliked, and marker numbers took the same
  blend. Scaling OKLab $a, b$ by 0.75 keeps OKLCH lightness and hue (Ottosson 2020): that orange becomes (239, 172, 91),
  red (230, 75, 60), the up-link blue (127, 0, 255) becomes (119, 65, 219). Overlapped-link marker numbers use exactly
  the link fill colour (`FillColorFor`). Rejected: restoring the raw colours (the user chose the original hue, softer).
  Where: `LocationLinkView.Soften`, `FillColorFor`, `OverlappedLocationLinkView`.

- **2026-10-26 Overlapped-link marker numbers use the link body's toned colour.** Why: the section number on a down
  marker was the saturated direction colour, which came out yellow; the user wants the burnt orange of a down link body.
  The label is now `FillColorFor(GetLocationLinkColor(ColorForStructure(parent), span, direction))` at full
  opacity (originally the RGB `Desaturate`; see the OKLCH entry), the link fill's expression, so up markers take the toned up-link colour too. Where:
  `OverlappedLocationLinkView` constructor.
- **2026-10-26 Links re-point when an adjacent location becomes hidden or visible.** Why: link ends are decided when
  a link view is built, so a link kept ending in empty space (or on the covering location) until the section's links
  were rebuilt. The only places a location becomes hidden or visible are where an overlapped link is added to or
  removed from the section's tracker, so there the section's other links touching that adjacent location are removed
  and re-added. This runs after the tracker's write lock is released, and rebuilt links do not re-point their own
  neighbours, so it cannot cascade. Where: `SectionLocationLinkAnnotationsViewModel.RepointLinksOfAdjacentEnds`.

- **2026-10-26 Polygon labels use the maximum inscribed circle or a rotated run, never the centroid circle.** Why: the
  old "inscribed circle" (centroid, radius to the nearest vertex) lies outside C-shaped cells and cells with holes, is
  tiny on long processes, and pokes past edges between vertices (user report). The same circle placed adjacent-section
  markers and translate targets, so `Polygon.InscribedCircle` and `CalculateInscribedCircle` now return the maximum
  inscribed circle and the centroid version is deleted. Decisions: angles ~~−75° to 90° in 15° steps~~ (superseded 2026-10-29:
  every 5°, entry above) plus the principal axis of the second moments of area, all in (−90°, 90°] so text never reads upside down and a vertical process reads
  bottom to top; ~~a tilted run scores 0.9 so near-level text snaps to level~~ superseded 2026-10-29: a tilted run's
  score is divided by $1 + G\lvert\sin\theta\rvert$ (entry above); the circle is kept unless a run beats it by
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

## Discussion points

- **Pen precision measurements (2026-10-27, synthetic membrane).** Radius-400 volume px outline with 5%, 3% and 1% harmonic
  wobble, sampled as the pen does (every 4 screen px, 0.5 screen px jitter), display at 3 interpolations. Old pipeline,
  new outline: +7/−9 px at downsample 4, +12/−24 at 8. Old Pen Mode edit: +23/−4 px at downsample 4 (outward bias), +25/−7
  after four more edits; retrace re-simplification: inward error 9 → 20 px after two edits. New: drawn curve within ~2 px of
  the fitted stroke both ways at downsample 1–8; five spliced edits kept every untouched control point bit-exactly and moved
  untouched outline by 0.02 px (sampling only). The remaining error against the true membrane is the hand's jitter in the
  samples (±5 px at downsample 4), which no fit can remove. Harness: session scratch program; reproducible from the tests in
  `GeometryTests/ControlPointFittingTests.cs` and `Clients/Viking/WebAnnotationTests/Actions/PenRegionCutTests.cs`.

## References

- Agafonkin, V. (2016). Polylabel: a fast algorithm for finding the pole of inaccessibility of a polygon. Mapbox,
  https://github.com/mapbox/polylabel (verify blog date). Used for: the branch and bound behind
  `PoleOfInaccessibility.Maximize`.
- Barry, P. J. & Goldman, R. N. (1988). A recursive evaluation algorithm for a class of Catmull-Rom splines. *SIGGRAPH
  Computer Graphics* 22(4), 199–204. doi:10.1145/378456.378511 (verify). Used for: the pyramid evaluation in
  `FitCurveSegmentWithTValues`.
- Catmull, E. & Rom, R. (1974). A class of local interpolating splines. In *Computer Aided Geometric Design*, Academic
  Press, 317–326 (verify). Used for: the interpolating curve through annotation control points.
- Douglas, D. H. & Peucker, T. K. (1973). Algorithms for the reduction of the number of points required to represent a
  digitized line or its caricature. *Cartographica* 10(2), 112–122. doi:10.3138/FM57-6770-U75U-7727. Used for: the
  rejected reduction of pen edits (polygon vertices, not curve control points).

- Garcia-Castellanos, D. & Lombardo, U. (2007). Poles of inaccessibility: a calculation algorithm for the remotest
  places on Earth. *Scottish Geographical Journal* 123(3), 227–233. doi:10.1080/14702540801897809 (verify). Used for: the
  pole of inaccessibility as the label anchor.
- Yuksel, C., Schaefer, S. & Keyser, J. (2011). Parameterization and applications of Catmull-Rom curves. *Computer-Aided
  Design* 43(7), 747–755. doi:10.1016/j.cad.2010.08.008 (verify). Used for: centripetal parameterization ($\alpha = 0.5$),
  which avoids cusps and self-intersections within a span.

## Open questions for a paper
