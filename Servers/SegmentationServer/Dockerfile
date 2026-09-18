# Use an NVIDIA CUDA image as the base
FROM nvidia/cuda:12.8.1-devel-ubuntu24.04

ENV DEBIAN_FRONTEND=noninteractive
ENV LANG=C.UTF-8
ENV PATH="${PATH}:/home/user/.local/bin"
ENV NVIDIA_VISIBLE_DEVICES=${NVIDIA_VISIBLE_DEVICES:-all}
ENV NVIDIA_DRIVER_CAPABILITIES=${NVIDIA_DRIVER_CAPABILITIES:+$NVIDIA_DRIVER_CAPABILITIES,}graphics
ENV PATH=/usr/local/nvidia/bin:/usr/local/cuda/bin:${PATH}
ENV CUDA_HOME="/usr/local/cuda"
ENV TORCH_CUDA_ARCH_LIST="6.0 6.1 7.0 7.5 8.0 8.6+PTX 8.9"

RUN echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections

RUN set -x \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        apt-transport-https ca-certificates \
        git curl \
        ffmpeg libsm6 libxext6 \
        software-properties-common \
    && add-apt-repository ppa:deadsnakes/ppa \
    && apt-get update \
    && apt-get install -y --no-install-recommends \
        python3.13 python3.13-venv python3.13-dev \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -ms /bin/bash user

RUN update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.13 1 \
    && update-alternatives --set python3 /usr/bin/python3.13

RUN curl -sS https://bootstrap.pypa.io/get-pip.py | python3.13

WORKDIR /home/user

# Pin facebookresearch/sam2 so image builds are reproducible.
# Notify James if https://github.com/facebookresearch/sam2 has commits newer than this SHA.
ARG SAM2_GIT_SHA=2b90b9f5ceec907a1c18123530e92e794ad901a4

RUN git clone https://github.com/facebookresearch/sam2.git segment-anything-2 \
    && cd segment-anything-2 \
    && git checkout "$SAM2_GIT_SHA" \
    && python3 -m pip install -e . -v \
    && python3 -m pip install -e ".[demo]" \
    && python3 -m pip install "grpcio>=1.71.0" "grpcio-tools>=1.71.0" numpy torch Pillow opencv-python matplotlib \
    && cd checkpoints && ./download_ckpts.sh

COPY ./Servers/SegmentationServer/segmentation_grpc/setup.py ./Servers/SegmentationServer/segmentation_grpc/pyproject.toml /home/user/segmentation_grpc/
COPY ./Servers/SegmentationServer/segmentation_server/setup.py ./Servers/SegmentationServer/segmentation_server/pyproject.toml /home/user/segmentation_server/
COPY ./Servers/SegmentationServer/segmentation_server/requirements-docker.txt /home/user/segmentation_server/

# Placeholder packages so pip -e succeeds before the full source COPY
RUN mkdir -p /home/user/segmentation_grpc/segmentation_grpc \
        /home/user/segmentation_server/segmentation_server \
    && printf '%s\n' '"""Placeholder for Docker dependency layer."""' \
        > /home/user/segmentation_grpc/segmentation_grpc/__init__.py \
    && printf '%s\n' '"""Placeholder for Docker dependency layer."""' \
        > /home/user/segmentation_server/segmentation_server/__init__.py

RUN cd /home/user/segmentation_grpc \
    && python3 -m pip install -e .

RUN cd /home/user/segmentation_server \
    && python3 -m pip install -r requirements-docker.txt \
    && python3 -m pip install -e . --no-deps \
    && python3 -m pip install debugpy

COPY ./gRPC_Protos /home/user/gRPC_Protos
COPY ./Servers/SegmentationServer/segmentation_grpc /home/user/segmentation_grpc
COPY ./Servers/SegmentationServer/segmentation_server/segmentation_server /home/user/segmentation_server/segmentation_server/

RUN chown -R user:user /home/user/gRPC_Protos /home/user/segmentation_grpc /home/user/segmentation_server

EXPOSE 50051
EXPOSE 5678
EXPOSE 12348

USER user

ENV PYTHONPATH=/home/user/segmentation_server:/home/user/segmentation_grpc
ENV VS_CODE_DEBUG="false"

STOPSIGNAL SIGTERM

COPY --chown=user:user ./Servers/SegmentationServer/start.sh /home/user/start.sh
RUN chmod +x /home/user/start.sh

ENTRYPOINT ["/home/user/start.sh"]
