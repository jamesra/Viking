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
  oriented rectangle ("run") at 13 angles, whichever allows the larger ID font. Runs come from rasterising the rotated
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
