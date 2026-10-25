# Publication notes: Viking

Annotation software for connectomes: the Viking and Jotunn clients, their servers (annotation, section correction,
SAM2 segmentation) and the gRPC protocols. VikingLegacy (the `Legacy` branch) shares these notes. Material for future
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

## Formulas

Circle radius between spaces (section to volume, or section to section through the correction), with $\Phi$ the
point map, $e_k$ the four unit axis vectors and $M$ the rim points that map:

$$
c' = \Phi(c), \qquad R' = R \cdot \frac{1}{|M|} \sum_{k \in M} \frac{\lVert \Phi(c + R e_k) - \Phi(c) \rVert}{R}
$$

Depth-relative trust of a seed (Sam2Trainer `docs/depth_relative_trust.md`): $w(\rho)$, $\rho = 1 - d / D_\ell$, full
trust for $\rho \le 0.5$, a cosine fall to 0 at $\rho = 0.95$ (`circle_diameter_scale`).

## Algorithmic decisions

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

- **GPU sharing between training and serving.** With a training run on the same GPU, a tile embedding took 20-30 s
  instead of 0.3-0.5 s, uploads waited up to 90 s, and a 93-tile mask could not finish within 5 quiet minutes. Serving
  latency depends on GPU isolation more than on client tuning.
- **Annotator circles are oversized more often than measured.** The Oct 13 measurement compared circles with the
  equal-area radius of the linked polygon, which exceeds the inscribed radius for elongated cells, so it understated how
  large circles are relative to the cell (Sam2Trainer `docs/training_plan_assessment.md`, 1.3).

## References

- Anderson, J. R., Jones, B. W., Yang, J.-H., Shaw, M. V., Watt, C. B., Koshevoy, P., Spaltenstein, J., Jurrus, E.,
  Kannan, U. V., Whitaker, R. T., Mastronarde, D., Tasdizen, T., Marc, R. E. (2009). A computational framework for
  ultrastructural mapping of neural circuitry. *PLoS Biology* 7(3), e1000074. (verify) Used for: the connectome
  workflow Viking serves.
- Anderson, J. R., Mohammed, S., Grimm, B., Jones, B. W., Koshevoy, P., Tasdizen, T., Whitaker, R., Marc, R. E. (2011).
  The Viking viewer for connectomics: scalable multi-user annotation and summarization of large volume data sets.
  *Journal of Microscopy* 241(1), 13-28. (verify) Used for: Viking's design.
- Bajaj, C. L., Coyle, E. J., Lin, K.-N. (1996). Arbitrary topology shape reconstruction from planar cross sections.
  *Graphical Models and Image Processing* 58(6), 524-543. (verify) Used for: `MorphologyMesh` contour-to-mesh tiling.
- Ravi, N., et al. (2024). SAM 2: Segment Anything in Images and Videos. arXiv:2408.00714. Used for: the segmentation
  server's model.

## Open questions for a paper

- Auto-segment throughput and acceptance with the prefetch and pipelining, measured on an isolated GPU.
- Whether the tapered circle seed (v7) reduces corrections of auto-segmented outlines in production.
