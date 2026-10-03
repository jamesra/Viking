# 👀 Segment Anything 2 + Docker  🐳

![image](https://github.com/user-attachments/assets/7911d7b8-72a7-4c90-9da6-7a867b0136f8)


Segment Anything 2 in Docker. A simple, easy to use Docker image for Meta's SAM2 with GUI support for displaying figures, images, and masks. Built on top of the SAM2 repo: https://github.com/facebookresearch/segment-anything-2

📰 New: The project has been restructured into three separate components:
1. **segmentation_grpc**: Contains the gRPC interface definition and code generation
2. **Example**: Sample Python client for `UploadImage` / `SegmentImage`. See [Example](Example/README.md).
3. **segmentation_server**: Contains the server implementation that runs in the Docker container

📰 We also have a ROS Noetic supported image in the [ROS Noetic branch](https://github.com/peasant98/SAM2-Docker/tree/ros-noetic)!


## Quickstart

This quickstart assumes you have access to an NVIDIA GPU. You should have installed the NVIDIA drivers and CUDA toolkit for your GPU beforehand. Also, make sure to install Docker [here](https://docs.docker.com/engine/install/).

First, let's install the NVIDIA Container Toolkit:

```bash
distribution=$(. /etc/os-release;echo $ID$VERSION_ID) \
   && curl -s -L https://nvidia.github.io/nvidia-docker/gpgkey | sudo apt-key add - \
   && curl -s -L https://nvidia.github.io/nvidia-docker/$distribution/nvidia-docker.list | sudo tee /etc/apt/sources.list.d/nvidia-docker.list
sudo apt-get update
sudo apt-get install -y nvidia-docker2
sudo systemctl restart docker
```

To get the SAM2 Docker image up and running, you can run (for NVIDIA GPUs that support at least CUDA 12.6)

```bash
sudo usermod -aG docker $USER
newgrp docker
docker run -it -v /tmp/.X11-unix:/tmp/.X11-unix  -e DISPLAY=$DISPLAY --gpus all peasant98/sam2:latest bash
```

We have a CUDA 12.1 docker image too, which can be run as follows:

```bash
docker run -it -v /tmp/.X11-unix:/tmp/.X11-unix  -e DISPLAY=$DISPLAY --gpus all peasant98/sam2:cuda-12.1 bash
```

From this shell, you can run SAM2, as well as display plots and images.

## Running the Example

To check SAM2 is working within the container, we have an example in `examples/image_predictor.py` to test the image mask generation. To run:

```bash
# mount this repo, which is assumed to be in the current directory
docker run -it -v /tmp/.X11-unix:/tmp/.X11-unix  -v `pwd`/SAM2-Docker:/home/user/SAM2-Docker -e DISPLAY=$DISPLAY --gpus all peasant98/sam2:cuda-12.1 bash

# in the container!
cd SAM2-Docker/
python3 examples/image_predictor.py

```

## Building and Running Locally

To build and run the Dockerfile:

```bash
docker build -t sam2:latest . 
```

And you can run as:

```bash
docker run -it -v /tmp/.X11-unix:/tmp/.X11-unix  -e DISPLAY=$DISPLAY --gpus all sam2:latest bash
```


Example of running Python code to display masks:

![alt text](image.png)

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

1. Predict the cell that owns the first foreground click, with every foreground click and
   background click that falls in its window.
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
3. If the result inside a core touches the core edge, or a prediction's margin put pixels in a
   core, predict that cell. Its positive clicks are the foreground clicks in its window plus
   1 to 3 seeds at the center (deepest point) of each separate piece of the result in its outer
   256 px margin, so SAM2 continues the same object. A piece that lies wholly inside the window
   center gets its own seed too.
4. A cell is predicted again (at most 4 times) when the result in its window has grown beyond
   what it last predicted, which is how a C or hairpin that returns through cores already
   visited is completed. Total work is bounded by a 48 cell and 96 prediction budget.
5. Aligned tiles a cell needs but the server lacks are set aside and the walk carries on with
   the other cells. When the walk runs dry, the server sends `TilesNeeded` on the stream and
   waits. The client uploads those tiles and answers with `TilesAnswer` (`ready` or
   `unavailable` per tile); the server then resumes the **same walk**, re-queueing only the
   cells that were waiting. Nothing is predicted twice and nothing is restored from an earlier
   call, so the mask only gains pixels. A tile reported `unavailable` (or reported ready but no
   longer cached) is never asked for again and the cells that need it stay out of the mask.
   When no cell is waiting any more the server sends the finished `SegmentationResponse` and
   ends the call.

The response mask is the fused cores with `origin_x`/`origin_y` at the mosaic pixel of its
lower-left corner, always a multiple of 512 wide and tall.

Set `SEGMENTATION_DEBUG_DUMP=1` to write one `.npz` per call (fused mask, every cell core, each
cell's raw answer, kept pieces and SAM2 logits, and the prompts) under the embedding cache mount
for offline inspection.

Cache defaults: 5 minute idle TTL, 1 GiB of encoded image bytes, and 32 GPU embeddings. Override with `--cache-ttl-seconds`, `--cache-max-memory-bytes`, and `--cache-max-images`. Missing full-frame IDs return `NOT_FOUND`; missing tiles return `TILE_NOT_FOUND` with row/col so the client can re-upload.

The GPU cap is the hot set. Feature maps for each cell are also written under `SEGMENTATION_EMBEDDING_CACHE` (inside the container, `/var/cache/segmentation-embeddings`). Compose mounts `${SEGMENTATION_EMBEDDING_CACHE_HOST:-D:/Docker/cache/segmentation-embeddings}` there. A later upload of the same PNG bytes reloads that file and skips `set_image()`. The directory is split by checkpoint identity and by encoder generation (`eager` while compile is warming, `compiled` after the swap), so a new checkpoint or the compiled encoder does not reuse the other generation. Default disk cap is 32 GiB (`SEGMENTATION_EMBEDDING_CACHE_MAX_BYTES`, `0` for no cap). The client still sends the PNG; the disk hit skips the encoder only.

## GPU / CUDA

The Docker image is `nvidia/cuda:13.2.1-devel-ubuntu24.04` with `torch==2.14.0+cu132` and `torchvision==0.29.0`. It is built for **Ada sm_89 and later** (RTX 4500 Ada, RTX 40, L40, Hopper, Blackwell). Host NVIDIA driver must be **595+** or the container will not start.

This is not RTX A4500 (Ampere sm_86). Ampere, Turing, and older GPUs are not compile targets.

`TORCH_CUDA_ARCH_LIST` is `8.9 9.0 10.0 12.0+PTX`. That is nvcc SASS/PTX for SAM2’s small CUDA extension, not PyTorch `torch.compile`.

The Hiera **image encoder is compiled** with `torch.compile(mode="max-autotune", fullgraph=True, dynamic=False)` on CUDA. `predict()` stays eager. The process listens immediately on an uncompiled encoder. Compile and a 1024×1024 warmup run in the background; `GetServerStatus` reports `compile=warming` until that finishes, then `compile=ready`. The image cache is flushed once at the swap so clients re-upload against the compiled encoder. First compile can take minutes; later `set_image()` calls reuse the compiled graph, and a warm Inductor cache makes the background pass much shorter. Pass `--no-compile-image-encoder` only if compile fails.

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
python Example/client_example.py --server localhost:40080 --image path/to/image.png --coordinates 100,200 300,400 --labels 1,0
```

Default path: `UploadImage` → `SegmentImage(image_id)` → `DeleteImage`. Pass `--inline` to send image bytes on the segment request (re-encodes every call).

Optional arguments:
- `--server`: The address of the segmentation service (default: localhost:40080)
- `--labels`: Labels as l1,l2,... (e.g., 1,0). 1 indicates the point is in the foreground, 0 in the background. Defaults to assuming all points are foreground.
- `--multimask`: Output multiple masks per point
- `--inline`: Skip the cache and send image bytes with the segment request
- `--tls`: Use TLS. Also selected automatically when `--server` uses port 443

### Demo page

The server can serve a browser page on HTTPS port **8443** (Compose publishes host **40444**). It is off unless `--demo-site` is passed or `SEGMENTATION_DEMO_SITE=1`. The page uses the same Let's Encrypt files as gRPC TLS (`SSL_CERT_PATH` and `SSL_KEY_PATH`). If those files are missing, gRPC cleartext still starts and the page does not bind. `segmentation-certbot-renewer` (Compose profile `letsencrypt`) obtains the certificate for `segmentation.codepharm.net` and restarts this process on renewal.

With the site enabled and the certificate present, open `https://segmentation.codepharm.net:40444`. Left-click is foreground, right-click is background. The page calls `UploadImage` once, then `SegmentImage` on that id. There is no login; leave the flag off on a host you do not want to expose.

### segmentation_server

This project contains the server implementation that runs in the Docker container.

To run the server:

```bash
python -m segmentation_server
```

Docker publishes cleartext gRPC as **40080:80**, gRPC TLS as **40443:443**, and the optional demo page as **40444:8443**. Host ports 80 and 443 belong to the reverse proxy. The router forwards `segmentation.codepharm.net:443` to host port 40443. TLS binds only when `SSL_CERT_PATH` and `SSL_KEY_PATH` point at certificate files. See [config-template/README.md](config-template/README.md) for Let's Encrypt enrollment. The demo page uses those same files and stays down until `SEGMENTATION_DEMO_SITE=1`.

Fine-tuned weights: put a Meta-format checkpoint at `D:\Docker\Run\segmentation-server\best_TEM_model.pt`. Compose mounts that folder at `/models` and sets `SAM2_CHECKPOINT=/models/best_TEM_model.pt` (override with `SEGMENTATION_MODEL_HOST` or `SAM2_CHECKPOINT`). Trainer `best_model.pt` is a raw state dict; convert it with `sam2-em-export-serve` before copying it here. Recreate the container after replacing the file. Eager and compiled encoders both load this checkpoint.

`SegmentImageSets` is the auto-segmentation stream. The client sends one foreground/background set at a time for a cached `image_id`, and the server writes one `SegmentationResponse` per set. Interactive clicks stay on unary `SegmentImage`.

Optional arguments:
- `--port`: Cleartext listen port inside the container (default: 50051; Compose starts it with `--port 80`)
- `--workers`: The number of worker threads (default: 10)
- `--inference-workers`: SAM2 inference thread pool size (default: 1)
- `--cache-ttl-seconds`: Unused cached-image lifetime (default: 300)
- `--cache-max-memory-bytes`: Cap on cached encoded image bytes (default: 1 GiB)
- `--cache-max-images`: Max cached images / GPU embeddings (default: 8)
- `--no-compile-image-encoder`: Skip Hiera `torch.compile` (default is on for CUDA)
- `--demo-site` / `--no-demo-site`: HTTPS point-prompt page (default off; `SEGMENTATION_DEMO_SITE=1` turns it on)
- `--demo-port`: Demo HTTPS port (default: 8443)
- `--generate-grpc`: Generate gRPC code before starting the server
