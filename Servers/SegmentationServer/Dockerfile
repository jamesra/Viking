# syntax=docker/dockerfile:1
# Layer order is deliberate: expensive/rare steps first so a Python-only
# rebuild reuses torch, the SAM2 compile, and the checkpoint.

FROM nvidia/cuda:13.2.1-devel-ubuntu24.04

ENV DEBIAN_FRONTEND=noninteractive \
    LANG=C.UTF-8 \
    NVIDIA_VISIBLE_DEVICES=${NVIDIA_VISIBLE_DEVICES:-all} \
    NVIDIA_DRIVER_CAPABILITIES=compute,utility,graphics \
    PATH=/usr/local/nvidia/bin:/usr/local/cuda/bin:/home/user/.local/bin:${PATH} \
    CUDA_HOME=/usr/local/cuda \
    TORCH_CUDA_ARCH_LIST="8.9 9.0 10.0 12.0+PTX" \
    PIP_DISABLE_PIP_VERSION_CHECK=1 \
    PIP_ROOT_USER_ACTION=ignore \
    PYTHONUNBUFFERED=1

# Keep apt downloads in the BuildKit cache across layer invalidations.
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    rm -f /etc/apt/apt.conf.d/docker-clean \
    && echo 'Binary::apt::APT::Keep-Downloaded-Packages "true";' \
        > /etc/apt/apt.conf.d/keep-cache \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates curl gpg \
    && curl -fsSL 'https://keyserver.ubuntu.com/pks/lookup?op=get&search=0xF23C5A6CF475977595C89F51BA6932366A755776' \
        | gpg --dearmor -o /etc/apt/keyrings/deadsnakes.gpg \
    && echo 'deb [signed-by=/etc/apt/keyrings/deadsnakes.gpg] https://ppa.launchpadcontent.net/deadsnakes/ppa/ubuntu noble main' \
        > /etc/apt/sources.list.d/deadsnakes.list \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        python3.13 python3.13-dev \
    && update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.13 1 \
    && curl -sS https://bootstrap.pypa.io/get-pip.py | python3.13 \
    && useradd -ms /bin/bash user

WORKDIR /home/user

# Declared immediately above this RUN so a SAM2 SHA bump does not re-download torch.
ARG TORCH_CUDA_INDEX=https://download.pytorch.org/whl/cu132
RUN --mount=type=cache,target=/root/.cache/pip \
    python3 -m pip install torch==2.14.0 torchvision==0.29.0 --index-url "$TORCH_CUDA_INDEX"

# Only the Hiera-L weights the server loads. download_ckpts.sh pulls ~3 GB of unused sizes.
ARG SAM2_CHECKPOINT_URL=https://dl.fbaipublicfiles.com/segment_anything_2/092824/sam2.1_hiera_large.pt
ENV SAM2_CHECKPOINT=/home/user/checkpoints/sam2.1_hiera_large.pt
RUN --mount=type=cache,target=/var/cache/sam2 \
    mkdir -p /home/user/checkpoints \
    && if [ ! -s /var/cache/sam2/sam2.1_hiera_large.pt ]; then \
        curl -fsSL -o /var/cache/sam2/sam2.1_hiera_large.pt "$SAM2_CHECKPOINT_URL"; \
    fi \
    && cp /var/cache/sam2/sam2.1_hiera_large.pt "$SAM2_CHECKPOINT"

# Pin facebookresearch/sam2 so image builds are reproducible.
# Notify James if https://github.com/facebookresearch/sam2 has commits newer than this SHA.
ARG SAM2_GIT_SHA=2b90b9f5ceec907a1c18123530e92e794ad901a4
RUN --mount=type=cache,target=/root/.cache/pip \
    curl -fsSL "https://github.com/facebookresearch/sam2/archive/${SAM2_GIT_SHA}.tar.gz" \
        | tar -xz -C /tmp \
    && mv "/tmp/sam2-${SAM2_GIT_SHA}" /home/user/segment-anything-2 \
    && python3 -m pip install -e /home/user/segment-anything-2 --no-build-isolation

# After expensive layers so these ENVs do not bust torch/SAM2 cache.
# Compose mounts this path so inductor/triton artifacts survive restarts.
ENV TORCHINDUCTOR_CACHE_DIR=/home/user/.cache/torch_inductor \
    TORCHINDUCTOR_FX_GRAPH_CACHE=1 \
    TRITON_CACHE_DIR=/home/user/.cache/torch_inductor/triton \
    TORCHINDUCTOR_PERSISTENT_AUTOTUNE_DIR=/home/user/.cache/torch_inductor/autotune

RUN mkdir -p /home/user/.cache/torch_inductor/triton \
        /home/user/.cache/torch_inductor/autotune \
    && chown -R user:user /home/user/.cache

# File capability so unprivileged `user` can bind 443. Compose also adds NET_BIND_SERVICE.
RUN --mount=type=cache,target=/var/cache/apt,sharing=locked \
    --mount=type=cache,target=/var/lib/apt,sharing=locked \
    apt-get update \
    && apt-get install -y --no-install-recommends libcap2-bin \
    && setcap 'cap_net_bind_service=+ep' /usr/bin/python3.13

COPY Servers/SegmentationServer/segmentation_grpc/setup.py \
     Servers/SegmentationServer/segmentation_grpc/pyproject.toml \
     /home/user/segmentation_grpc/
COPY Servers/SegmentationServer/segmentation_server/setup.py \
     Servers/SegmentationServer/segmentation_server/pyproject.toml \
     Servers/SegmentationServer/segmentation_server/requirements-docker.txt \
     /home/user/segmentation_server/

# Placeholder packages so pip -e succeeds before the full source COPY.
RUN --mount=type=cache,target=/root/.cache/pip \
    mkdir -p /home/user/segmentation_grpc/segmentation_grpc \
        /home/user/segmentation_server/segmentation_server \
    && printf '%s\n' '"""Placeholder for Docker dependency layer."""' \
        > /home/user/segmentation_grpc/segmentation_grpc/__init__.py \
    && printf '%s\n' '"""Placeholder for Docker dependency layer."""' \
        > /home/user/segmentation_server/segmentation_server/__init__.py \
    && python3 -m pip install -e /home/user/segmentation_grpc \
    && python3 -m pip install -r /home/user/segmentation_server/requirements-docker.txt \
        --extra-index-url https://download.pytorch.org/whl/cu132 \
    && python3 -m pip install -e /home/user/segmentation_server --no-deps

COPY --chmod=755 Servers/SegmentationServer/start.sh /home/user/start.sh

COPY --chown=user:user gRPC_Protos/Segmentation /home/user/gRPC_Protos/Segmentation
COPY --chown=user:user Servers/SegmentationServer/segmentation_grpc /home/user/segmentation_grpc
COPY --chown=user:user Servers/SegmentationServer/segmentation_server/segmentation_server /home/user/segmentation_server/segmentation_server/

EXPOSE 80
EXPOSE 443
EXPOSE 5678
EXPOSE 12348

USER user

ENV PYTHONPATH=/home/user/segmentation_server:/home/user/segmentation_grpc \
    VS_CODE_DEBUG=false

STOPSIGNAL SIGTERM

ENTRYPOINT ["/home/user/start.sh"]
