# Segmentation server

A gRPC service that serves Meta's [Segment Anything 2](https://github.com/facebookresearch/sam2) to the Viking annotation clients. A client uploads image tiles (or a whole viewport), sends foreground and background clicks and optional boxes, and gets masks and polygons back. It runs on an NVIDIA GPU in Docker and **only speaks TLS**.

The project has three parts:

1. **segmentation_grpc**: the generated gRPC stubs for the shared proto in `gRPC_Protos/Segmentation/SAM2/segmentation.proto`.
2. **segmentation_server**: the server that runs in the Docker container.
3. **Example**: a sample Python client for `UploadImage` / `SegmentImage`. See [Example](Example/README.md).

The SAM2 version is pinned by commit in the [Dockerfile](Dockerfile) (`SAM2_GIT_SHA`). The Python packages pin the same commit, and a test fails if the two drift.

## Quickstart

You need an NVIDIA GPU, a current driver (595 or later for the CUDA 13.2 image), Docker with GPU support, and a TLS certificate for the host name clients will use.

1. Put a SAM2 checkpoint in `Meta` format where Compose mounts it (see "Fine-tuned weights" below), and the certificate where `SSL_CERT_PATH` / `SSL_KEY_PATH` point. For Let's Encrypt, `config-template/README.md` explains the renewer service.
2. From the repository root: `docker compose up -d segmentation-server` (add `--profile letsencrypt` the first time to enroll the certificate).
3. Check it: `python Example/client_example.py --server <host>:40443 --image <png> --coordinates 100,200`.

For a local run without Docker or Let's Encrypt, install both packages (`pip install -e segmentation_grpc segmentation_server[dev]`), generate a development certificate with `python -m segmentation_server.dev_cert --out ./dev-cert`, point `SSL_CERT_PATH` and `SSL_KEY_PATH` at it and run `python -m segmentation_server --tls-port 8443`. Clients then trust that certificate with `--ca-cert`.

## Tests

`cd segmentation_server && python -m pytest`. The suite needs no GPU: SAM2 is replaced by fakes, and the growth walk runs against a synthetic world (`tests/cell_world.py`). It also checks that the committed gRPC stubs match the proto and that dependency floors match what the stubs require.

## Cached vs inline

SAM2 `set_image()` (the Hiera encoder) is expensive. `predict()` from stored embeddings is cheap. Clients should encode once and reuse.

### Full-frame path (`UploadImage`)

1. `UploadImage` — server runs `set_image()` once and returns a session `image_id`.
2. `SegmentImage` / `MultiSegmentImage` with that `image_id` — `predict()` only.
3. `DeleteImage` when the viewport changes or the tool deactivates.

`image_id == 0` plus inline `image_data` is the slow fallback: the encoder runs on every request. Keep it for one-shot tools; do not use it for interactive tracing.

### Tile path (`UploadTile` / `SegmentTilesStream`)

Interactive Viking uses 1024×1024 mosaic cells. The reusable identity is **`TileCoord`**
(`volume`, `section`, `channel`, `transform`, `downsample`, `row`, `col`) — not a sequential
`image_id`. Any client that uploads the same coord with identical bytes gets `already_cached=true`
and skips `set_image()`. `SegmentTilesStream` looks up cells by those coords. An internal cache id may
appear in server logs for predictor bookkeeping; clients must not treat it as the tile identity.

1. `UploadTile` for each cell that holds a foreground click.
2. Open `SegmentTilesStream` and send one `start` (`SegmentTilesRequest`: those `TileCoord`s and
   mosaic-space prompts).
3. For each `TilesNeeded` the server sends, `UploadTile` those cells and reply with a
   `TilesAnswer`. The stream ends with `result` (the finished mask) or an error.
4. No `DeleteImage` for tiles — idle TTL / LRU / entry cap reclaim GPU slots.

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

1. Start the walk in **the fewest windows that between them see the whole prompt**. The prompt
   is every box (`foreground_boxes`) and every foreground point. A modern client sends the
   square inscribed in a circle as the box, plus four foreground clicks on the axes at 95% of
   the radius (outside the square's edges, just inside the circle), and nothing else; the center
   is not sent. The candidates are the cells the prompt touches (each whose core holds part of a
   box, each that owns a point). The candidate whose window holds the most of the prompt starts
   first, with ties going to the window with the most room round it; that repeats for what is
   still unseen. A window holds a box only when the whole box reaches its core and sits clear of
   the window border, so a prompt that fits one window is predicted once, with all of it. A box
   bigger than any window is cut to its overlap with each core it touches, which lies inside
   the object and at least 256 px from the window border, where a mask can cover it, and every
   one of those cores starts. Cells the prompt touches that were not needed start only if the
   chosen windows find nothing, and a click the mask does not reach starts its own cell. A core
   the box covers entirely is taken as object without a prediction (its tiles are not needed).
   Edge crossings then carry the walk outward. A box the client sent is the user's statement
   that its region is the structure, so it is added to the finished mask whole: what SAM2
   leaves out of it (an organelle, a pale compartment) is not left as a hole. Only the result
   is changed: the walk, and whether the request found anything, ignore the box. Boxes the
   server inferred from nine-click circles are not added.

   The mask that answers a prompt is chosen by one rule for every RPC, with no combining of
   masks: with a box, the highest-scoring candidate that covers **at least 95%** of the box's pixels and
   has more pixels than it (auto-segment masks grow past their rectangle, so one that does not,
   or that is only the rectangle, is not an answer to it); without a box, the candidate that
   covers the most foreground points, ties broken by score. A start cell with no matching
   mask is logged and skipped; the call fails with `FAILED_PRECONDITION` and a `NO_MATCHING_MASK`
   detail only when every start cell, reserve cells included, was rejected and nothing else was
   found. There is no empty
   or best-effort result.

   With one mask per prediction (what the Viking client asks for) the rule can only accept or
   refuse SAM2's single guess. Set `SEGMENTATION_BOX_MULTIMASK=1` to ask SAM2 for its three
   candidates on box prompts instead, so the highest-scoring one that covers the box and extends
   past it is chosen (the second `use_mask_input` pass is skipped then). It is off by default;
   each candidate's score, pixel count and verdict is logged on one `Box prompt ... candidates`
   line so the setting can be judged from real requests.

   *Legacy prompt.* The production client sends nine clicks (the center, four at half the radius
   rotated 45 degrees, four at 0.8 on the axes) and no box. The server infers the inscribed box
   from them (`circle_boxes.find_circles`) and leaves the ring clicks out of the SAM2 prompt. That
   inference exists only for that client and is deleted when it is retired.
2. Drop any piece of the answer that holds no positive click, then OR the core into the result.
   Pixels in the outer 256 px margin are ORed in only where SAM2's logit is at least
   `SEGMENT_MARGIN_LOGIT` (default 1.5; a pixel is object above 0), so the margin counts only
   where the model is clearly sure. A prediction that returns no logits contributes its core
   only. Each pixel is stored in the core of the cell that owns it, so a neighbor that sees more
   of the object can add to the cell centered on it. The owner has the last word: once a cell
   has predicted, core pixels where its own logit is below `SEGMENT_OWNER_VETO_LOGIT` (default
   -1.0) are removed if a neighbor's margin put them there, and later neighbors cannot add
   them. This stops a neighbor's window border (where SAM2 tends to run a mask to the image
   edge) from leaving a straight cut. Pixels the owner accepted itself are never removed. The
   logits are SAM2's low-resolution map for its best mask, upsampled to the window.
3. When the result in a core touches an edge it shares with a neighbor, that stretch of set
   pixels along the edge is a **range** (a range under 16 px is noise and is ignored; only the
   outermost pixel counts, so a mask that stops one pixel short is not a crossing). The
   neighbor sees the last 256 px of this core as its own outer margin band. From the edge,
   project into that band pixel by pixel until a non-mask pixel, which gives a depth for each
   position along the range; the largest rectangle under those depths, with one side on the edge,
   is the neighbor's **box prompt** (no inset; it stops `SEGMENT_SEED_EDGE_CLEARANCE`, default 8,
   px short of the window border, because SAM2 treats an image border as an object boundary).
   The prompt is that box, a click at its center, the foreground and background clicks in the
   window, and one click per other range on the edge (longest first, at most 24). One prediction
   is made per edge. A cell that was started by a user click (the first, or a click the walk had
   not reached) is prompted by the clicks and boxes, not by an edge. Corners are not followed.
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
   waits. The client uploads those tiles and answers with `TilesAnswer` (`ready` or
   `unavailable` per tile); the server then resumes the **same walk**, re-queueing only the
   cells that were waiting. Nothing is predicted twice and nothing is restored from an earlier
   call, so the mask only gains pixels. A tile reported `unavailable` (or reported ready but no
   longer cached) is never asked for again and the cells that need it stay out of the mask.
   When no cell is waiting any more the server sends the finished `SegmentationResponse` and
   ends the call.
   A tile that another stream has already asked its client for is not asked for again. The
   server records each tile it asks for until the upload reaches the cache; a second stream
   that needs the same tile leaves it out of its `TilesNeeded`, waits for that upload, and
   pins it when it arrives. It asks its own client after `SEGMENTATION_TILE_SHARE_TIMEOUT_SECONDS`
   (default 5; `0` turns sharing off), or at once if the stream it was waiting on ends, is
   cancelled, or reports the tile `unavailable`. A client therefore sees a `TilesNeeded` that
   lists fewer tiles than the walk wanted, or none for a round. A client that sends no answer
   that settles a requested tile for `SEGMENTATION_TILE_ANSWER_TIMEOUT_SECONDS` (default 60)
   gets `DEADLINE_EXCEEDED`; empty or unrelated answers do not restart that clock.

The response mask is the fused cores with `origin_x`/`origin_y` at the mosaic pixel of its
lower-left corner, always a multiple of 512 wide and tall.

Set `SEGMENTATION_DEBUG_DUMP=1` to write one `.npz` per call (fused mask, every cell core, each
cell's raw answer, kept pieces and SAM2 logits, and the prompts) under the embedding cache mount
for offline inspection.

Request limits: a request over a limit is refused with `RESOURCE_EXHAUSTED`, naming the limit. `SEGMENTATION_MAX_TILES` (default 64) caps tiles on a `SegmentTilesStream` start, `SEGMENTATION_MAX_POINTS` (512) caps the prompt points on any segment request, `SEGMENTATION_MAX_BOXES` (16) caps boxes, `SEGMENTATION_MAX_SETS` (5000) caps prompt sets on one `SegmentVolumes` stream, and `SEGMENTATION_MAX_ANSWER_TILES` (4096) caps the tiles one `TilesAnswer` may list (over it is `INVALID_ARGUMENT`). `SEGMENTATION_MAX_CONCURRENT_STREAMS` (32) caps `SegmentTilesStream` calls running at once, since each holds cache pins while it waits for tiles, and `SEGMENTATION_MAX_CONCURRENT_RPCS` (256) caps every call in flight; a call over either is `RESOURCE_EXHAUSTED` and can be retried. An image is refused when its declared size is over `SEGMENTATION_MAX_IMAGE_PIXELS` (100 million), before it is decoded. An upload that cannot fit under the cache byte cap, because every other entry is held by a running request, is `RESOURCE_EXHAUSTED`; the client can retry.

Cache defaults: 5 minute idle TTL for ad-hoc uploads (grid tiles do not expire), 16 GiB of encoded image bytes, and 4096 ad-hoc uploads. Grid tiles have no count cap. Every cached image holds about 9 MiB of GPU embedding, so the GPU is the real limit: when free CUDA memory drops below max(1 GiB, 10% of the card), the least recently used idle entry is dropped, tile or ad-hoc. The client re-uploads the PNG and the disk embedding cache below skips the encoder. Override with `--cache-ttl-seconds`, `--cache-max-memory-bytes`, and `--cache-max-images`. Missing full-frame IDs return `NOT_FOUND`; missing tiles return `TILE_NOT_FOUND` with row/col so the client can re-upload.

The GPU cap is the hot set. Feature maps for each cell are also written under `SEGMENTATION_EMBEDDING_CACHE` (inside the container, `/var/cache/segmentation-embeddings`). Compose mounts `${SEGMENTATION_EMBEDDING_CACHE_HOST:-D:/Docker/cache/segmentation-embeddings}` there. A later upload of the same PNG bytes reloads that file and skips `set_image()`. The directory is split by checkpoint identity and by encoder generation (`eager` while compile is warming, `compiled` after the swap), so a new checkpoint or the compiled encoder does not reuse the other generation. Default disk cap is 32 GiB (`SEGMENTATION_EMBEDDING_CACHE_MAX_BYTES`, `0` for no cap). The client still sends the PNG; the disk hit skips the encoder only.

## GPU / CUDA

The Docker image is `nvidia/cuda:13.2.1-devel-ubuntu24.04` with `torch==2.14.0+cu132` and `torchvision==0.29.0`. It is built for **Ada sm_89 and later** (RTX 4500 Ada, RTX 40, L40, Hopper, Blackwell). Host NVIDIA driver must be **595+** or the container will not start.

This is not RTX A4500 (Ampere sm_86). Ampere, Turing, and older GPUs are not compile targets.

`TORCH_CUDA_ARCH_LIST` is `8.9 9.0 10.0 12.0+PTX`. That is nvcc SASS/PTX for SAM2’s small CUDA extension, not PyTorch `torch.compile`.

The Hiera **image encoder is compiled** with `torch.compile(mode="max-autotune", fullgraph=True, dynamic=False)` on CUDA. `predict()` stays eager. The process listens immediately on an uncompiled encoder. Compile and a 1024×1024 warmup run in the background; `GetServerStatus` reports `compile=warming` until that finishes, then `compile=ready`. Before the swap the compiled encoder must pass a numeric check: the same seeded image goes through the eager and the compiled encoder and the feature maps must agree (cosine similarity at least 0.99, relative L2 error at most 0.10, all values finite). If they do not, the server stays on the eager encoder and reports `compile=failed`. After the swap only cache entries built with the eager encoder are dropped, so a client that uploaded after the swap keeps its `image_id`; clients with an older id get `NOT_FOUND` / `TILE_NOT_FOUND` and re-upload. `GetServerStatus` also reports `encoder_generation` (`eager` or `compiled`) and `compile_status` as fields. First compile can take minutes; later `set_image()` calls reuse the compiled graph, and a warm Inductor cache makes the background pass much shorter. Pass `--no-compile-image-encoder` only if compile fails.

Inductor/Triton artifacts are stored under `TORCHINDUCTOR_CACHE_DIR` (`/home/user/.cache/torch_inductor` in the image). Compose mounts `${SEGMENTATION_INDUCTOR_CACHE:-D:/Docker/cache/segmentation-inductor}` there so a container restart is a cache hit (seconds) instead of a full retune. A torch, GPU, or SAM2 architecture change misses and retunes. Server Python edits do not.

On Windows + Docker Desktop (WSL2), a host sleep or GPU driver reset poisons the CUDA context. The next `UploadImage` then fails with `CUDA_ERROR_UNKNOWN` / `cuStreamIsCapturing`. The server treats that as fatal and exits so Docker can restart it with a fresh GPU context. If GPU containers fail to start with an `ld.so` assertion, shut down WSL (`wsl --shutdown`) so it remounts the NVIDIA libraries, then start Docker Desktop again.

## Project Structure and Usage

### segmentation_grpc

This project contains the gRPC interface definition and code generation for the segmentation service.

To generate the gRPC code:

```bash
python -m segmentation_grpc
```

### Example

[Example](Example/README.md) is a command-line sample. It uploads an image, sends foreground and background points, and plots the mask.

```bash
python Example/client_example.py --server segmentation.codepharm.net:40443 --image path/to/image.png --coordinates 100,200 300,400 --labels 1,0
```

Default path: `UploadImage` → `SegmentImage(image_id)` → `DeleteImage`. Pass `--inline` to send image bytes on the segment request (re-encodes every call).

Optional arguments:
- `--server`: The address of the segmentation service (default: localhost:40443). The channel is always TLS.
- `--ca-cert`: PEM file to trust as the server's root, for a self-signed development certificate (`python -m segmentation_server.dev_cert`). Omit it for a Let's Encrypt server.
- `--labels`: Labels as l1,l2,... (e.g., 1,0). 1 indicates the point is in the foreground, 0 in the background. Defaults to assuming all points are foreground.
- `--multimask`: Output multiple masks per point
- `--inline`: Skip the cache and send image bytes with the segment request
### Demo page

The server can serve a browser page on HTTPS port **8443** (Compose publishes host **40444**). It is off unless `--demo-site` is passed or `SEGMENTATION_DEMO_SITE=1`. The page uses the same Let's Encrypt files as gRPC TLS (`SSL_CERT_PATH` and `SSL_KEY_PATH`). The server will not start without those files, so the page is always available when it is enabled. `segmentation-certbot-renewer` (Compose profile `letsencrypt`) obtains the certificate for `segmentation.codepharm.net` and restarts this process on renewal.

With the site enabled, open `https://segmentation.codepharm.net:40444`. Left-click is foreground, right-click is background. The page calls `UploadImage` once, then `SegmentImage` on that id.

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

The server only listens with TLS; there is no cleartext gRPC port. Docker publishes gRPC TLS as **40443:443** and the optional demo page as **40444:8443**, on loopback only until `SEGMENTATION_DEMO_HOST_BIND=0.0.0.0` is set, so a stack that does not run the page opens no second port on the network. Host ports 80 and 443 belong to the reverse proxy. The router forwards `segmentation.codepharm.net:443` to host port 40443. `SSL_CERT_PATH` and `SSL_KEY_PATH` must point at the certificate chain (`fullchain.pem`) and key; the server waits up to `SEGMENTATION_TLS_WAIT_SECONDS` (default 120) for them, then exits so the container restart policy retries. For local runs, `python -m segmentation_server.dev_cert --out ./dev-cert` writes a self-signed pair (needs the `cryptography` package). See [config-template/README.md](config-template/README.md) for Let's Encrypt enrollment. The demo page uses those same files and stays down until `SEGMENTATION_DEMO_SITE=1`.

Fine-tuned weights: put a Meta-format checkpoint at `D:\Docker\Run\segmentation-server\best_TEM_model.pt`. Compose mounts that folder at `/models` and sets `SAM2_CHECKPOINT=/models/best_TEM_model.pt` (override with `SEGMENTATION_MODEL_HOST` or `SAM2_CHECKPOINT`). Trainer `best_model.pt` is a raw state dict; convert it with `sam2-em-export-serve` before copying it here. Recreate the container after replacing the file. Eager and compiled encoders both load this checkpoint.

`SegmentVolumes` is the auto-segmentation stream. The client sends one foreground/background set at a time for a cached `image_id`, and the server writes one `SegmentationResponse` per set. Interactive clicks stay on unary `SegmentImage`.

Optional arguments:
- `--tls-port`: TLS gRPC listen port inside the container (default: `SEGMENTATION_TLS_PORT`, else 443). The old `--port` flag named the removed cleartext listener and is now an error, so a stale Compose command fails at start instead of serving the wrong port.
- `--workers`: The number of worker threads (default: 10)
- `--inference-workers`: SAM2 inference thread pool size (default: 1)
- `--cache-ttl-seconds`: Unused cached-image lifetime (default: 300)
- `--cache-max-memory-bytes`: Cap on cached encoded image bytes (default: 1 GiB)
- `--cache-max-images`: Max ad-hoc uploaded images; grid tiles are not counted (default: 4096)
- `--no-compile-image-encoder`: Skip Hiera `torch.compile` (default is on for CUDA)
- `--demo-site` / `--no-demo-site`: HTTPS point-prompt page (default off; `SEGMENTATION_DEMO_SITE=1` turns it on)
- `--demo-port`: Demo HTTPS port (default: 8443)
- `--demo-bind`: Address the demo page listens on (default: `SEGMENTATION_DEMO_BIND`, else `127.0.0.1`)
- `--generate-grpc`: Generate gRPC code before starting the server
