# Segmentation server

A gRPC service that serves Meta's [Segment Anything 2](https://github.com/facebookresearch/sam2) to the Viking annotation clients. A client names the 1024 x 1024 tiles under its prompt, sends what the object is (clicks, annotation shapes, a painted bitmap) and what it is not (neighbouring annotations), uploads the tiles the server asks for, and gets SAM2's logits back, one result per island of the prompt. It runs on an NVIDIA GPU in Docker and **only speaks TLS**.

The project has three parts:

1. **segmentation_grpc**: the generated gRPC stubs for the shared protos in `gRPC_Protos`: `Segmentation/SAM2/segmentation.proto` and the geometry it imports, `Common/geometry.proto`.
2. **segmentation_server**: the server that runs in the Docker container.
3. **Example**: a sample Python client that segments a picture through `UploadTile` / `SegmentTilesStream`. See [Example](Example/README.md).

The SAM2 version is pinned by commit in the [Dockerfile](Dockerfile) (`SAM2_GIT_SHA`). The Python packages pin the same commit, and a test fails if the two drift.

## Quickstart

You need an NVIDIA GPU, a current driver (595 or later for the CUDA 13.2 image), Docker with GPU support, and a TLS certificate for the host name clients will use.

1. Put a SAM2 checkpoint in `Meta` format where Compose mounts it (see "Fine-tuned weights" below), and the certificate where `SSL_CERT_PATH` / `SSL_KEY_PATH` point. For Let's Encrypt, `config-template/README.md` explains the renewer service.
2. From the repository root: `docker compose up -d segmentation-server` (add `--profile letsencrypt` the first time to enroll the certificate).
3. Check it: `python Example/client_example.py --server <host>:40443 --image <png> --coordinates 100,200`.

For a local run without Docker or Let's Encrypt, install both packages (`pip install -e segmentation_grpc segmentation_server[dev]`), generate a development certificate with `python -m segmentation_server.dev_cert --out ./dev-cert`, point `SSL_CERT_PATH` and `SSL_KEY_PATH` at it and run `python -m segmentation_server --tls-port 8443`. Clients then trust that certificate with `--ca-cert`.

## Tests

`cd segmentation_server && python -m pytest`. The suite needs no GPU: SAM2 is replaced by fakes, and the growth walk runs against a synthetic world (`tests/cell_world.py`). It also checks that the committed gRPC stubs match the proto and that dependency floors match what the stubs require.

## Tiles

SAM2 `set_image()` (the Hiera encoder) is expensive. `predict()` from stored embeddings is cheap, so the server
encodes each tile once and every client reuses it.

The service has four RPCs: `UploadTile`, `SegmentTilesStream`, `GetHeldTiles` and `GetServerStatus`. Every tile is on
a fixed grid of 1024 x 1024 tiles. A tile is a **`TileGrid`** (`volume`, `section`, `channel`, `transform`,
`downsample`, `space`: what every cell of one grid shares) and a **`GridCell`** (`row`, `col`), both from
`Common/geometry.proto`; a call names its grid once and every tile in it is a cell. Row and column are
`floor(position / (1024 * downsample))` from the origin, row increasing with Y. The pair is the reusable identity, not
a sequential `image_id`: any client that uploads a cell already cached gets `already_cached=true` and skips
`set_image()`; the cached picture is kept. The grid must name everything that changes the render (Viking puts its
channel set and capture-affecting view options into `channel`). `space` is `COORDINATE_SPACE_MOSAIC` or
`COORDINATE_SPACE_VOLUME`; `UNSPECIFIED`, or a downsample that is not positive, is `INVALID_ARGUMENT` on every RPC.
An `UploadTile` image must decode to exactly 1024 x 1024 (`INVALID_ARGUMENT` otherwise). An internal cache id may
appear in server logs for predictor bookkeeping; clients must not treat it as the tile identity.

1. Open `SegmentTilesStream` and send one `start` (`SegmentTilesRequest`: the `grid`, the cells under the prompt in
   `tiles`, the two `Feature`s, and `mask_tile_count`), then the announced mask tiles. Nothing needs uploading first.
2. For each `TilesNeeded` the server sends, `UploadTile` the cells in `tiles`, and reply with one `TilesAnswer`:
   `unavailable` for the cells the client cannot supply, and `background_shapes` for the cells in
   `background_cells`. The server asks only for cells none of its caches hold (see the lookup order below), the start
   cells included. The stream ends with `result` or an error.
3. Nothing is deleted by a client: idle LRU and the GPU check reclaim GPU slots.

A client uploading tiles ahead of a request (Viking's auto-segment prefetch) first asks `GetHeldTiles`
which of them the server holds (GPU cache, stored tiles) or reads itself from the annotation context's
tileset, and skips those. The answer is a hint: it pins nothing, and the stream still asks for a cell
dropped since.

The server pins every tile it uses for the whole call, so cache eviction cannot remove a tile
mid-request. A client that cancels or ends its half of the stream while tiles are awaited
abandons the request; no result is sent.

#### How `SegmentTilesStream` grows a mask

The server never sees a tile as the unit of work. It walks a grid of overlapping **cells**:
each cell is a 1024×1024 window, cells start every 512 px (50% overlap), and each cell owns
only its central 512×512 **core**. Cores partition the mosaic. A cell whose origin falls on a
tile corner (even row, even column) *is* an uploaded tile and reuses its pinned embedding; any
other cell is cropped from two or four uploaded tiles and predicted on a throwaway predictor
(its embedding is still cached on disk by image digest).

1. **The prompt** (`prompt.py`, `shapes.py`, `prompt_encodings.py`). Every coordinate is in the grid's space at its
   downsample, X right, Y up; pixel `(x, y)` covers `[x, x + 1) x [y, y + 1)` and a click is the pixel it falls in.
   What the object is: foreground clicks (`foreground_feature.points`), foreground shapes
   (`foreground_feature.shapes`) and a FOREGROUND bitmap (mask tiles). What it is not: background clicks, background
   shapes (the neighbouring annotations) and a BACKGROUND bitmap. A request needs a foreground click, shape or mask
   tile; a `Shape.point` in either `Feature` is `INVALID_ARGUMENT` (a click goes in `points`).

   Shapes are rasterized per tile or window, a pixel being inside when its centre is (the Viking client's own rule):
   a circle and an ellipse analytically, a line as a stroke of its `width` (round ends; under a pixel wide is drawn one
   pixel wide), a polygon by even-odd fill of all its rings (holes included). A `CurveFit` of
   `CURVE_METHOD_CATMULL_ROM` first replaces the points by Viking's centripetal Catmull-Rom outline (`curve_fit.py`, a
   port of `Geometry/CurveFitting/CatmullRom.cs` `FitCurve`, checked point for point against the C# assembly);
   `interpolations` 0 is straight segments. More than 64 interpolations, a ring under three points, a radius or
   semi-axis that is not positive, or a coordinate that is not finite is `INVALID_ARGUMENT`.

   **The server encodes the prompt for the model it serves** (the manifest's `prompt_encoding`), because the model
   only works with the `mask_input` values it was trained on. How each part is used follows from what it is, not from
   a client flag: a foreground geometry shape is *softened* (its edge is not trusted) and the foreground bitmap is
   used as drawn. For `edge-confidence-v6` a softened circle is its disc at `radius * circle_diameter_scale` (the
   manifest's `client_contract`; the client sends the TRUE radius), and every foreground part is encoded as a seed,
   `+SEED_LOGIT * c(d)` with the trainer's edge confidence of its own pixels; several parts of one object are combined
   by the largest value. Background shapes are each a negative by their own geometry: a circle by its radius trust
   (`circle_negative_canvas`, full to half the radius, nothing past `zero_frac`), any other shape and the background
   bitmap by the edge curve (`negative_canvas`). A pixel claimed by both sides is 0 (unknown). For `legacy-flat-v1`
   the object is a flat +8 and every background region -8. `edge-confidence-v7` (not served yet) trusts softened
   shapes relative to their own depth (`depth_trust.py`, Sam2Trainer `docs/depth_relative_trust.md`): a circle by
   `circle_trust` of its true radius, used positively, any other shape by its local thickness.

   **Islands.** An object prompt that falls apart is several objects, each segmented by its own walk and returned as
   its own `SegmentResult`. For v6 and the legacy encoding an island is an 8-connected component of the union of the
   foreground parts' pixels (the softened circle's disc, the shapes, the bitmap), joined across tile edges and corners;
   for v7 it is a trusted core (`rho <= full_frac`) with the falling zone nearest to it, and cores under 16 px are
   dropped. Islands are numbered 1, 2, ... by decreasing trusted area (prompt pixels for v6, core pixels for v7); past
   `SEGMENTATION_MAX_ISLANDS` (default 16) the smallest are dropped with a warning. A foreground click goes with the
   island it lies in, a click outside every island with island 1; an island with no click starts from its pixel
   nearest its centroid. Background clicks, shapes and bitmap go with every island. Clicks alone are one walk, as
   before. The prompt may cover at most `SEGMENTATION_MAX_MASK_TILES` tiles (`RESOURCE_EXHAUSTED`).

   Each island's pixels are pinned as object in its result, and a core wholly inside them is taken as object without
   a prediction (an island that leaves part of a core out is predicted, then its pixels are pinned). Background
   regions are cleared from the result's mask: a polygon, line or ellipse as drawn, a circle out to `zero_frac` of its
   radius.

   Each walk starts in **the fewest windows that between them see the whole island**: the candidates are the cells the
   island and its clicks touch, the one whose window holds the most of the prompt starts first, with ties going to the
   window with the most room round it, and that repeats for what is still unseen. An island bigger than any window is
   read through each core it touches, at least 256 px from the window border. Cells that were not needed start only if
   the chosen windows find nothing. The start window's `mask_input` is the encoder's whole prompt for that window.
   Edge crossings then carry the walk outward.

   **Mask tiles** carry painted bitmaps, so a bitmap can be any size. `mask_tile_count` is how many `MaskTile`s, of
   both kinds, follow `start` (in `mask_tiles` stream messages), 0 for none: every 1024 tile a bitmap touches, as
   `full`, a 1-bit PNG (1024x1024, or 256x256 at `scale` 4), or an empty tile. The server waits for all of them (the
   idle limit; `DEADLINE_EXCEEDED` otherwise) before growth starts, and refuses a repeated (cell, kind), a wrong-sized
   PNG, an unset kind, or more tiles than announced (`INVALID_ARGUMENT`), and a count over
   `SEGMENTATION_MAX_MASK_TILES` (default 256, `RESOURCE_EXHAUSTED`). Mask tiles are per request and never enter the
   image cache.

   **Background shapes on demand.** The client sends, with `start`, the background shapes that reach the cells in
   `tiles`. Before it predicts a window, the walk needs to know the neighbours of every tile the window covers: the
   cells in `tiles` are known from the start, and so is every cell the server has asked about once that `TilesNeeded`
   is answered. For the others the server lists them in `TilesNeeded.background_cells` (with any image tiles the
   window lacks, in the same message) and waits; the answer's `background_shapes` are the shapes reaching them that
   the client has not sent before, empty when none do. Each cell is asked about once per call.

   The mask that answers a prompt is chosen by one rule for every prediction, with no combining of
   masks: with a box, the highest-scoring candidate that has a pixel inside it (how much of the
   rectangle it fills is not asked: SAM2 leaves holes in an object's interior and stops short of a
   box beside an image border, and a mask that touches none of the rectangle is another object);
   without a box, the candidate that
   covers the most foreground points, ties broken by score. A start cell with no matching
   mask is logged and skipped. An island whose every start cell, reserve cells included, was
   rejected with nothing else found has no result; the call fails with `FAILED_PRECONDITION` and a
   `NO_MATCHING_MASK` detail only when that is true of every island. There is no empty or
   best-effort result.

   With one mask per prediction (what the Viking client asks for) the rule can only accept or
   refuse SAM2's single guess. Set `SEGMENTATION_BOX_MULTIMASK=1` to ask SAM2 for its three
   candidates on box prompts instead, so the highest-scoring one that covers the box and extends
   past it is chosen (the second `use_mask_input` pass is skipped then). It is off by default;
   each candidate's score, pixel count and verdict is logged on one `Box prompt ... candidates`
   line so the setting can be judged from real requests.
2. Blend the answer into the result as logits. SAM2's mask decoder emits logits at 1/4 of the
   window, 256x256 samples (four pixels each) for a 1024 window, and each core owns 128x128 of
   them. A prediction speaks only for the piece of its answer that holds a positive click and
   for the samples within 16 samples (64 px) of that piece; farther away, "not object" says
   nothing about a separate piece of the same structure (the other arm of a hairpin). Its logits
   are added to every core its window overlaps, weighted by distance from the window border
   (0 at the border, 1 across the core), so the cell that sees a pixel most centred decides it
   and a neighbor's unsure edge answer barely counts. A cell's later prediction replaces its
   earlier one. The result is where the fused logit is above the request's `mask_threshold`
   (bilinearly upsampled, as SAM2 does). The walk
   itself follows a separate control view, every pixel any one window answered as object away
   from its border, so a window with more context can reveal object a blind neighbor denied.
   A prediction that returns no logits contributes its core only.
3. When the result in a core touches an edge it shares with a neighbor, that stretch of set
   pixels along the edge is a **range** (a range under 16 px is noise and is ignored; only the
   outermost pixel counts, so a mask that stops one pixel short is not a crossing). The
   neighbor sees the last 256 px of this core as its own outer margin band. From the edge,
   project into that band pixel by pixel until a non-mask pixel, which gives a depth for each
   position along the range; the largest rectangle under those depths, with one side on the edge,
   is the neighbor's **box prompt** (no inset; it stops `SEGMENT_SEED_EDGE_CLEARANCE`, default 8,
   px short of the window border, because SAM2 treats an image border as an object boundary).
   The rectangle is recorded and tells the walk what the window has already seen, but it is **not
   sent to SAM2**: a box reaching toward the window border makes SAM2 stop at the border. The
   prompt is a click at its center, the foreground and background clicks in the
   window, and one click per other range on the edge (longest first, at most 24). One prediction
   is made per edge. A cell that was started by a user click (the first, or a click the walk had
   not reached) is prompted by the clicks and boxes, not by an edge. Corners are not followed.
   A continuation window is also given **what its neighbors found** as SAM2's mask prompt
   (`mask_input`): the fused logits over the window, 256x256, with 0 (neutral) wherever no window
   has predicted. The window therefore continues the same object instead of choosing one from its
   box and clicks alone, and it is not discarded for stopping short of the box.

   A prediction speaks only for the object its clicks found, and for background within 64 px of
   it; a sample that only such silent predictions cover stays **unseen** (`-20` in the fused logits),
   which is not "looked and found background". An object that runs into unseen samples has not been
   followed to its end, typically because a window ended in the middle of a core where no core edge
   shows it. After every prediction the walk finds those **frontiers** (at least 4 connected samples)
   and queues the cell whose core holds each, so its window is centered there, with a click on the
   object at the frontier and the mask prompt. A spot is tried once per 64 px square. The debug
   dump labels these predictions `frontier`.
4. A graph of cells whose edges hold the merged ranges already crossed prevents loops. A range
   that overlaps one already crossed is crossed again only if it is at least twice as long or it
   joins two stored ranges, so the return arm of a C or hairpin (a new range) is followed and a
   mask looping back over a seam is not. A range a cell's own last prediction already covers is
   not new. A prediction that finds no matching mask in a continuation cell skips that cell and
   logs a warning, and its ranges count as crossed; only the first cell's failure fails the call.
   Each cell is predicted at most 4 times, and the whole walk is bounded by a 48 cell and
   96 prediction budget. `SEGMENTATION_DEBUG_DUMP` records each prediction's edge, ranges, box and
   outcome, and the final graph.
5. Aligned tiles a cell needs but the server lacks are set aside and the walk carries on with
   the other cells. When the walk runs dry, the server sends `TilesNeeded` on the stream and
   waits. A tile is **settled** when its upload reaches the cache (the server sees every
   `UploadTile` land and wakes the stream waiting for it) or when an answer lists it in
   `unavailable`; uploads are never listed. The server resumes the **same walk** as soon as
   anything settles, re-queueing only the cells that were waiting, so it uses each tile as it
   lands instead of waiting for the whole batch or the answer. Nothing is predicted twice and
   nothing is restored from an earlier call, so the mask only gains pixels. A tile reported
   `unavailable` (or uploaded but already gone from the caches) is never asked for again in the
   call and the cells that need it stay out of the mask. The client answers every `TilesNeeded`
   once, in order, after its uploads; an answer with none outstanding is `INVALID_ARGUMENT`, and
   one listing more than `SEGMENTATION_MAX_ANSWER_TILES` cells or `SEGMENTATION_MAX_SHAPES`
   shapes is `RESOURCE_EXHAUSTED`. A tile of an answered `TilesNeeded` that was neither uploaded
   nor listed is looked up in the cache once, then still awaited. When no walk needs anything and
   every `TilesNeeded` has been answered, the server sends the finished `SegmentationResponse`
   and ends the call.
   A tile that another stream has already asked its client for is not asked for again. The
   server records each tile it asks for until the upload reaches the cache; a second stream
   that needs the same tile leaves it out of its `TilesNeeded`, waits for that upload, and
   pins it when it arrives. It asks its own client after `SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS`
   (default 5; `0` turns sharing off), or at once if the stream it was waiting on ends, is
   cancelled, or reports the tile `unavailable`. A client therefore sees a `TilesNeeded` that
   lists fewer tiles than the walk wanted, or none (only `background_cells`) for a round. A
   client that uploads and answers nothing for `SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS`
   (default 60) while a tile or an answer is owed gets `DEADLINE_EXCEEDED`.

### The result

`SegmentationResponse` holds one `SegmentResult` per island that found something, in island order, and the
`request_id` echoed back. A result is SAM2's fused logits at their native resolution (`logits_scale` 4: one sample per
4 x 4 pixels), cropped to what is above the request's `mask_threshold` (small holes filled first) plus 2 samples of
border, as little-endian float16, row-major, `logits_size.width` per row, rows from the top (largest Y) down. Samples
nobody predicted hold -20. `logits_origin` is the grid-space pixel (X right, Y up, at the grid's downsample) at the
top-left of sample (0, 0): sample (i, j) covers pixels `x` from `origin.x + 4i` to `origin.x + 4i + 3` and `y` from
`origin.y - 4j - 3` to `origin.y - 4j`. The client contours the logits at `mask_threshold` and places the contour with
the origin and scale. A walk whose predictions carried no logits (only test fakes) sends its mask as logits at the same
grid. `score` is the lowest score of the island's predictions; `island` is its number.

These are the numbers the earlier protocol sent with `LOGITS | NO_UPSCALE`, placed differently: it gave the walk's
bottom-left pixel as the response's `origin_x`/`origin_y`, the frame's height in samples, and the crop's corner as
`logits_x`/`logits_y` in Y-down pixels from the frame's top-left. `logits_origin` is
`(origin_x + logits_x, origin_y + height * 4 - 1 - logits_y)` (`responses.logits_origin`, pinned by
`tests/test_responses.py`). Masks, polygons, the labeled image and upscaled logits are no longer sent.

Set `SEGMENTATION_DEBUG_DUMP=1` to write one `.npz` per call (fused mask, every cell core, each
cell's raw answer, kept pieces and SAM2 logits, and the prompts) under the embedding cache mount
for offline inspection.

Request limits: a request over a limit is refused with `RESOURCE_EXHAUSTED`, naming the limit. `SEGMENTATION_MAX_TILES` (default 64) caps tiles on a `SegmentTilesStream` start, `SEGMENTATION_MAX_POINTS` (512) and `SEGMENTATION_MAX_SHAPES` (512) cap the points and shapes of each `Feature` (and the shapes of one `TilesAnswer`), `SEGMENTATION_MAX_MASK_TILES` (256) caps the announced mask tiles and the tiles the foreground prompt covers, `SEGMENTATION_MAX_ISLANDS` (16) caps the islands segmented (the smallest are dropped, not refused), and `SEGMENTATION_MAX_ANSWER_TILES` (4096) caps the cells one `TilesAnswer` may list and the tiles of one `MaskTiles` message. `SEGMENTATION_MAX_CONCURRENT_STREAMS` (32) caps `SegmentTilesStream` calls running at once, since each holds cache pins while it waits for tiles, and `SEGMENTATION_MAX_CONCURRENT_RPCS` (256) caps every call in flight; a call over either is `RESOURCE_EXHAUSTED` and can be retried. An image is refused when its declared size is over `SEGMENTATION_MAX_IMAGE_PIXELS` (100 million), before it is decoded. An upload that cannot fit under the cache byte cap, because every other entry is held by a running request, is `RESOURCE_EXHAUSTED`; the client can retry.

Cache defaults: 16 GiB of encoded image bytes; grid tiles do not expire and have no count cap (`--cache-ttl-seconds` and `--cache-max-images` apply only to the cache's non-tile entries, which the service no longer makes). Every cached image holds about 9 MiB of GPU embedding, so the GPU is the real limit: when free CUDA memory drops below max(1 GiB, 10% of the card), the least recently used idle entry is dropped. Override with `--cache-max-memory-bytes`. A tile that is already cached keeps its entry whatever bytes a later upload brings, so two clients that render a cell slightly differently do not keep replacing each other's embedding.

Tile lookup order: for every tile a `SegmentTilesStream` needs, the cells named in `start` included, the server tries the GPU cache, then the tile store, then another stream that has already asked its client for that tile, and only then asks its own client in `TilesNeeded`. A tile under a click that the server lacks is asked for like any other, so a client names its prompt cells without uploading them first. The tile store keeps the bytes of every tile uploaded, so a tile dropped from the GPU cache is rebuilt without a client: in host memory (`SEGMENTATION_TILE_RAM_BYTES`, default 2 GiB, `0` keeps none) and, when the embedding disk cache is on, on disk under `<SEGMENTATION_EMBEDDING_CACHE>/tiles` (`SEGMENTATION_TILE_CACHE_MAX_BYTES`, default 8 GiB, `0` for no cap), so they also survive a restart. A rebuilt tile goes back into the GPU cache, and the disk embedding cache below usually skips its encoder too. A stored tile that would not fit in the GPU cache is still used from its pixels. Nothing invalidates a stored tile except its cap: a client must put everything that changes the render into the key.

Tiles the server reads itself: when a stream's `annotation_context_id` is a context this server is local to (Identity lists them for the `segmentation` OAuth client: `IdentityServer__Endpoint`, `IdentityServer__ClientSecret`, `SEGMENTATION_SERVICE_NAME`, see `identity_client.py`), a tile no cache holds and no other stream is fetching is read by the server before the client is asked. It stitches the cell from the context's VikingXML tileset over HTTP (`tile_source.py`, `viking_volume.py`), encodes it as the client encodes a capture (`capture_encoder.py`), and keeps it in the tile store like an upload. Only plain greyscale cells in mosaic space are read: `space` is `COORDINATE_SPACE_MOSAIC`, and `channel` has no `[...]` suffix and names a tileset of the section at that downsample. `TileGrid.space` is part of the cache key, so mosaic and volume cells never share an entry. Everything else, and any tile the read fails for, is asked of the client in `TilesNeeded`. A tileset rebuilt in place on storage is not noticed until its cells leave the tile store.

The GPU cap is the hot set. Feature maps for each cell are also written under `SEGMENTATION_EMBEDDING_CACHE` (inside the container, `/var/cache/segmentation-embeddings`). Compose mounts `${SEGMENTATION_EMBEDDING_CACHE_HOST:-D:/Docker/cache/segmentation-embeddings}` there. A later upload, or a tile rebuilt from the tile store, with the same pixels reloads that file and skips `set_image()`. The directory is split by checkpoint identity and by encoder generation (`eager` while compile is warming, `compiled` after the swap), so a new checkpoint or the compiled encoder does not reuse the other generation. Default disk cap is 32 GiB (`SEGMENTATION_EMBEDDING_CACHE_MAX_BYTES`, `0` for no cap). A request that pins a cached tile refreshes its embedding file and stored tile, so a cell that stays hot on the GPU is not the first one the disk caps drop.

In front of the disk store, offset cell windows (the ones predicted on a throwaway predictor) keep their last few embeddings in host memory (`SEGMENTATION_EMBEDDING_RAM_ENTRIES`, default 4, `0` turns it off; a 1024 window is roughly 40 to 85 MiB). A repeat prediction on the same window pixels then skips both the encoder and the disk read, and with no disk store configured it is the only thing that does.

## GPU / CUDA

The Docker image is `nvidia/cuda:13.2.1-devel-ubuntu24.04` with `torch==2.14.0+cu132` and `torchvision==0.29.0`. It is built for **Ada sm_89 and later** (RTX 4500 Ada, RTX 40, L40, Hopper, Blackwell). Host NVIDIA driver must be **595+** or the container will not start.

This is not RTX A4500 (Ampere sm_86). Ampere, Turing, and older GPUs are not compile targets.

`TORCH_CUDA_ARCH_LIST` is `8.9 9.0 10.0 12.0+PTX`. That is nvcc SASS/PTX for SAM2’s small CUDA extension, not PyTorch `torch.compile`.

The Hiera **image encoder is compiled** with `torch.compile(mode="max-autotune", fullgraph=True, dynamic=False)` on CUDA. `predict()` stays eager. The process listens immediately on an uncompiled encoder. Compile and a 1024×1024 warmup run in the background; `GetServerStatus` reports `compile=warming` until that finishes, then `compile=ready`. Before the swap the compiled encoder must pass a numeric check: the same seeded image goes through the eager and the compiled encoder and the feature maps must agree (cosine similarity at least 0.99, relative L2 error at most 0.10, all values finite). If they do not, the server stays on the eager encoder and reports `compile=failed`. After the swap only cache entries built with the eager encoder are dropped; a dropped tile is rebuilt from the tile store, or asked for again. `GetServerStatus` also reports `encoder_generation` (`eager` or `compiled`) and `compile_status` as fields. First compile can take minutes; later `set_image()` calls reuse the compiled graph, and a warm Inductor cache makes the background pass much shorter. Pass `--no-compile-image-encoder` only if compile fails.

Inductor/Triton artifacts are stored under `TORCHINDUCTOR_CACHE_DIR` (`/home/user/.cache/torch_inductor` in the image). Compose mounts `${SEGMENTATION_INDUCTOR_CACHE:-D:/Docker/cache/segmentation-inductor}` there so a container restart is a cache hit (seconds) instead of a full retune. A torch, GPU, or SAM2 architecture change misses and retunes. Server Python edits do not.

On Windows + Docker Desktop (WSL2), a host sleep or GPU driver reset poisons the CUDA context. The next `UploadTile` then fails with `CUDA_ERROR_UNKNOWN` / `cuStreamIsCapturing`. The server treats that as fatal and exits so Docker can restart it with a fresh GPU context. If GPU containers fail to start with an `ld.so` assertion, shut down WSL (`wsl --shutdown`) so it remounts the NVIDIA libraries, then start Docker Desktop again.

## Project Structure and Usage

### segmentation_grpc

This project contains the generated gRPC code for the segmentation service: `segmentation_pb2.py`,
`segmentation_pb2_grpc.py` and `geometry_pb2.py`, committed, and re-exported from the package (the geometry types too,
so `from segmentation_grpc import TileGrid, Shape` works).

To regenerate them after a proto changes:

```bash
python -m segmentation_grpc
```

The protos import each other relative to `gRPC_Protos` (`segmentation.proto` has `import "Common/geometry.proto"`), so
`generate_grpc.py` runs `grpc_tools.protoc` with `gRPC_Protos` as the import root, writes into a scratch directory,
copies the three modules flat into the package, and rewrites protoc's `from Common import geometry_pb2` and
`from Segmentation.SAM2 import segmentation_pb2` to `from segmentation_grpc import ...`. It finds `gRPC_Protos` by
walking up from the package, or at `/home/user/gRPC_Protos` in the image, which copies `Segmentation` and `Common`
(`--generate-grpc` uses the same code). `tests/test_stub_sync.py` fails when the committed modules drift from either
proto.

### Example

[Example](Example/README.md) is a command-line sample. It lays a picture on a tile grid of its own, sends foreground and background points, uploads the tiles the server asks for, and plots one mask per island.

```bash
python Example/client_example.py --server segmentation.codepharm.net:40443 --image path/to/image.png --coordinates 100,200 300,400 --labels 1,0
```

Calls: `GetServerStatus` (for the served `model_id`), then one `SegmentTilesStream` with `UploadTile` for each tile it asks for (`segmentation_server/picture_segmentation.py`).

Optional arguments:
- `--server`: The address of the segmentation service (default: localhost:40443). The channel is always TLS.
- `--ca-cert`: PEM file to trust as the server's root, for a self-signed development certificate (`python -m segmentation_server.dev_cert`). Omit it for a Let's Encrypt server.
- `--labels`: Labels as l1,l2,... (e.g., 1,0). 1 indicates the point is in the foreground, 0 in the background. Defaults to assuming all points are foreground.
### Demo page

The server can serve a browser page on HTTPS port **8443** (Compose publishes host **40444**). It is off unless `--demo-site` is passed or `SEGMENTATION_DEMO_SITE=1`. The page uses the same Let's Encrypt files as gRPC TLS (`SSL_CERT_PATH` and `SSL_KEY_PATH`). The server will not start without those files, so the page is always available when it is enabled. `segmentation-certbot-renewer` (Compose profile `letsencrypt`) obtains the certificate for `segmentation.codepharm.net` and restarts this process on renewal.

Behind a reverse proxy that terminates TLS, set `SEGMENTATION_DEMO_PLAINTEXT=1` (or pass `--demo-plaintext`) to serve the page as plain HTTP instead. It then needs no certificate and listens on port **80** unless `--demo-port` says otherwise. It has no login unless `SEGMENTATION_DEMO_TOKEN` is set, so publish it only to the proxy. `rebuild-and-start.ps1` turns this on for every model container and publishes it on host port gRPC port + 100 (v4: 40546).

With the site enabled, open `https://segmentation.codepharm.net:40444`. Left-click is foreground, right-click is background. The page keeps the uploaded picture (the last 16, by id) and segments it like any client: the picture is laid on a tile grid of its own (volume `picture-<digest>`, mosaic space, downsample 1), and each segment runs one in-process `SegmentTilesStream`, uploading the tiles it asks for (`picture_segmentation.py`).

The page is cautious by default because it has no login of its own:

- It listens on `127.0.0.1` unless `--demo-bind` or `SEGMENTATION_DEMO_BIND` says otherwise. In Docker, the published port cannot reach loopback inside the container, so exposing the page means setting `SEGMENTATION_DEMO_BIND=0.0.0.0` on purpose. The server logs a warning when it is reachable beyond the host with no token.
- `SEGMENTATION_DEMO_TOKEN`, when set, must be sent as `X-Demo-Token` on every upload, segment and delete. The page asks for it once and keeps it for that browser tab.
- Every request that changes anything must carry `X-Demo-Client: 1`. A web page on another origin cannot add that header without a CORS preflight, which the server never allows, so such a page cannot drive the demo through a visitor's browser.
- Requests run on a pool of 8 workers with 16 waiting; more are refused with `503`. Connections time out after 30 seconds, a body needs a valid `Content-Length` (chunked is refused with `411`), and uploads over 64 MiB get `413`.
- Images are refused when their declared size is over 100 million pixels (`SEGMENTATION_MAX_IMAGE_PIXELS`), before any decoding. Servicer error details other than bad-request messages are logged, not shown in the browser.

### Container image

The image has a `HEALTHCHECK` that runs `python -m segmentation_server.healthcheck`: it asks the running server for `GetServerStatus` over TLS on loopback (using the certificate's public host name for verification, from `SEGMENTATION_HEALTHCHECK_HOST` or the `live/<domain>` path of `SSL_CERT_PATH`) and exits non-zero if it does not answer. The start period is 10 minutes, to cover waiting for the certificate, loading the weights and the first CUDA start.

`debugpy` and `pydevd-pycharm` open debug listeners, so the production image does not contain them. For a development image build with `--build-arg INSTALL_DEBUG_TOOLS=true` (then `VS_CODE_DEBUG` / `PYCHARM_DEBUG` work as before; without the tools the container says so and exits instead of failing later). Unknown values for on/off settings such as `SAM2_COMPILE_IMAGE_ENCODER` are logged and treated as **off**, not silently as the default.
### segmentation_server

This project contains the server implementation that runs in the Docker container.

To run the server:

```bash
python -m segmentation_server
```

The server only listens with TLS; there is no cleartext gRPC port. Docker publishes gRPC TLS as **40443:443** and the optional demo page as **40444:8443**, on loopback only until `SEGMENTATION_DEMO_HOST_BIND=0.0.0.0` is set, so a stack that does not run the page opens no second port on the network. Host ports 80 and 443 belong to the reverse proxy. The proxy routes `VIKING_HOSTNAME_SEGMENTATION` (port 443, by SNI) to host port 40443. `SSL_CERT_PATH` and `SSL_KEY_PATH` must point at the certificate chain (`fullchain.pem`) and key; the server waits up to `SEGMENTATION_TLS_WAIT_SECONDS` (default 120) for them, then exits so the container restart policy retries. For local runs, `python -m segmentation_server.dev_cert --out ./dev-cert` writes a self-signed pair (needs the `cryptography` package). In the `viking-segmentation` stack the pair is the shared certificate from `viking-certs` (`/certs/<TLS_CERT_NAME>/`); see `Documentation/source/deployment/segmentation.rst` and `tls.rst`. The demo page uses those same files and stays down until `SEGMENTATION_DEMO_SITE=1`.

Fine-tuned weights: put a Meta-format checkpoint at `D:\Docker\Run\segmentation-server\best_TEM_model.pt`. Compose mounts that folder at `/models` and sets `SAM2_CHECKPOINT=/models/best_TEM_model.pt` (override with `SEGMENTATION_MODEL_HOST` or `SAM2_CHECKPOINT`). Trainer `best_model.pt` is a raw state dict; convert it with `sam2-em-export-serve` before copying it here. Recreate the container after replacing the file. Eager and compiled encoders both load this checkpoint.

### Model manifest and serving several models

A model is more than weights: it was trained on one mask-prompt encoding, takes tiles or the whole screen, and saw one
version of the training data. That travels with the weights as `<stem>.model.json`, next to the checkpoint
(`v4.3.pt` and `v4.3.model.json`). A model is named `v<VERSION>.<EPOCH>` (`v4.3` is model 4 after trainer epoch 3,
counted from 0) and its id is `v<VERSION>`; the training data has its own numbering (`v3`, `v3.1`), reported
separately. The trainer writes it for every export. A model that predates the
exporter (v2, v3) gets a hand-written one; the trainer repository documents the format (`docs/model_manifest.md`),
ships examples, and checks them with `python -m sam2_segmentation_trainer.model_manifest check <file>`.

At start-up the server reads the manifest and:
- logs the model, its version, its training-data set and its encoding;
- reports them in `GetServerStatus.model` (`ModelInfo`: `model_id`, `model_version`, `training_data_set`,
  `training_data_fingerprint`, `prompt_encoding`, the operator's `description` of the model, and the manifest's
  `client_contract` as `contract_json`), so a client learns whether it must send `model_id`;
- applies the `client_contract`'s `circle_diameter_scale` to softened circles itself (it is informational to the
  client, which sends true radii);
- builds the start window's `mask_input` with the encoding the manifest names: `legacy-flat-v1` is the original flat
  +8/-8 scheme, `edge-confidence-v6` is the trainer's encoding (`trainer_prompt_encoding.py`, a byte-for-byte copy
  of the trainer's `annotation_crops/prompt_encoding.py`, kept in step by a test), `edge-confidence-v7` adds the
  depth-relative trust. Windows the walk grows into are still prompted with their neighbours' fused logits;
- **refuses to start** if the manifest names an encoding it does not implement, or `capabilities` other than
  `tile_grid` / `single` (every model is fed as a single-resolution tile grid; the section may be left out), since
  serving a model with a prompt it was not trained on degrades masks without any error.

Without a manifest the model is treated as a tiled model with the legacy encoding, and a warning says so; a
`<stem>.prompt_encoding.json` alone still names the encoding.

`SegmentTilesRequest` carries an optional `model_id`. A different id is refused with `FAILED_PRECONDITION`, naming
both models; an unset one is refused only when the manifest says `requires_model_id` (`SEGMENTATION_REQUIRE_MODEL_ID=1`
or `0` overrides it).

**Several models at once** run as separate containers of the same image, each reached by its own host name that the
reverse proxy routes by SNI. Each model is a row of `Run/viking-segmentation/models.json`
(`stack.ps1 viking-segmentation model v3 sam2_em_v3_epoch_9.pt` adds or changes one; `rebuild-and-start.ps1` in this
folder does the work), served as `segmentation-v3.<domain>` by default. Each container has its own cache folders, because the server deletes the other checkpoint folders it finds in its cache
at start-up. The shared certificate carries every name, and `viking-certs` restarts every container labelled
`viking.tls-reload` when it renews. See `Documentation/source/deployment/segmentation.rst`.

Weights with no sidecar (`<stem>.model.json`) are reported with `SEGMENTATION_MODEL_NAME` (each service sets it to
its prefix) as display name and the file stem as version; their `model_id` stays `unknown` so requests naming a model
are not refused.

Optional arguments:
- `--tls-port`: TLS gRPC listen port inside the container (default: `SEGMENTATION_TLS_PORT`, else 443). The old `--port` flag named the removed cleartext listener and is now an error, so a stale Compose command fails at start instead of serving the wrong port.
- `--workers`: The number of worker threads (default: 10)
- `--inference-workers`: SAM2 inference thread pool size (default: 1)
- `--cache-ttl-seconds`: Unused lifetime of non-tile cache entries; grid tiles do not expire (default: 300)
- `--cache-max-memory-bytes`: Cap on cached encoded image bytes (default: 16 GiB)
- `--cache-max-images`: Max non-tile cache entries; grid tiles are not counted (default: 4096)
- `--no-compile-image-encoder`: Skip Hiera `torch.compile` (default is on for CUDA)
- `--demo-site` / `--no-demo-site`: HTTPS point-prompt page (default off; `SEGMENTATION_DEMO_SITE=1` turns it on)
- `--demo-port`: Demo HTTPS port (default: 8443)
- `--demo-bind`: Address the demo page listens on (default: `SEGMENTATION_DEMO_BIND`, else `127.0.0.1`)
- `--generate-grpc`: Generate gRPC code before starting the server
