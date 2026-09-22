# 👀 Segment Anything 2 + Docker  🐳

![image](https://github.com/user-attachments/assets/7911d7b8-72a7-4c90-9da6-7a867b0136f8)


Segment Anything 2 in Docker. A simple, easy to use Docker image for Meta's SAM2 with GUI support for displaying figures, images, and masks. Built on top of the SAM2 repo: https://github.com/facebookresearch/segment-anything-2

📰 New: The project has been restructured into three separate components:
1. **segmentation_grpc**: Contains the gRPC interface definition and code generation
2. **SegmentationClient**: Contains the client implementation for interacting with the segmentation service
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

SAM2 `set_image()` (the Hiera encoder) is expensive. `predict()` from stored embeddings is cheap. Clients should encode once and reuse:

1. `UploadImage` — server runs `set_image()` once and returns `image_id`.
2. `SegmentImage` / `MultiSegmentImage` with that `image_id` — `predict()` only. Viking does this for each click.
3. `DeleteImage` when the viewport changes or the tool deactivates.

`image_id == 0` plus inline `image_data` is the slow fallback: the encoder runs on every request. Keep it for one-shot tools; do not use it for interactive tracing.

Cache defaults: 5 minute idle TTL, 1 GiB of encoded image bytes, and 8 images (VRAM proxy for embeddings). Override with `--cache-ttl-seconds`, `--cache-max-memory-bytes`, and `--cache-max-images`. Missing IDs return `NOT_FOUND`; Viking re-uploads.

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

### SegmentationClient

This project contains the client implementation for interacting with the segmentation service.

To segment an image:

```bash
python -m SegmentationClient --image path/to/image.png --coordinates 100,200 300,400
```

Default path: `UploadImage` → `SegmentImage(image_id)` → `DeleteImage`. Pass `--inline` to send image bytes on the segment request (re-encodes every call).

Optional arguments:
- `--server`: The address of the segmentation service (default: localhost:40080)
- `--labels`: Labels as l1,l2,... (e.g., 1,0). 1 indicates the point is in the foreground, 0 in the background. Defaults to assuming all points are foreground.
- `--multimask`: Output multiple masks per point
- `--inline`: Skip the cache and send image bytes with the segment request
- `--tls`: Use TLS. Also selected automatically when `--server` uses port 443

To test the service:

```bash
python -m SegmentationClient.test_service
```

### segmentation_server

This project contains the server implementation that runs in the Docker container.

To run the server:

```bash
python -m segmentation_server
```

Docker publishes cleartext gRPC as **40080:80** and TLS as **40443:443**. Host ports 80 and 443 belong to the reverse proxy. The router forwards `segmentation.codepharm.net:443` to host port 40443. TLS binds only when `SSL_CERT_PATH` and `SSL_KEY_PATH` point at certificate files. See [config-template/README.md](config-template/README.md) for Let's Encrypt enrollment.

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
- `--generate-grpc`: Generate gRPC code before starting the server
