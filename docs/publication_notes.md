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

## Formulas

Circle radius between spaces (section to volume, or section to section through the correction), with $\Phi$ the
point map, $e_k$ the four unit axis vectors and $M$ the rim points that map:

$$
c' = \Phi(c), \qquad R' = R \cdot \frac{1}{|M|} \sum_{k \in M} \frac{\lVert \Phi(c + R e_k) - \Phi(c) \rVert}{R}
$$

Depth-relative trust of a seed (Sam2Trainer `docs/depth_relative_trust.md`): $w(\rho)$, $\rho = 1 - d / D_\ell$, full
trust for $\rho \le 0.5$, a cosine fall to 0 at $\rho = 0.95$ (`circle_diameter_scale`).

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

## Algorithmic decisions

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
- Garcia-Castellanos, D. & Lombardo, U. (2007). Poles of inaccessibility: a calculation algorithm for the remotest
  places on Earth. *Scottish Geographical Journal* 123(3), 227–233. doi:10.1080/14702540801897809 (verify). Used for: the
  pole of inaccessibility as the label anchor.
- Ravi, N., et al. (2024). SAM 2: Segment Anything in Images and Videos. arXiv:2408.00714. Used for: the segmentation
  server's model.

## Open questions for a paper

- Auto-segment throughput and acceptance with the prefetch and pipelining, measured on an isolated GPU.
- Whether the tapered circle seed (v7) reduces corrections of auto-segmented outlines in production.
