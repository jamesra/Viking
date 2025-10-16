# Use an NVIDIA CUDA image as the base
FROM nvidia/cuda:12.8.1-devel-ubuntu24.04

# Set up environment variables
ENV DEBIAN_FRONTEND=noninteractive
ENV PATH="${PATH}:/home/user/.local/bin"

# We love UTF!
ENV LANG=C.UTF-8

RUN echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections

# Set the nvidia container runtime environment variables
ENV NVIDIA_VISIBLE_DEVICES=${NVIDIA_VISIBLE_DEVICES:-all}
ENV NVIDIA_DRIVER_CAPABILITIES=${NVIDIA_DRIVER_CAPABILITIES:+$NVIDIA_DRIVER_CAPABILITIES,}graphics
ENV PATH=/usr/local/nvidia/bin:/usr/local/cuda/bin:${PATH}
ENV CUDA_HOME="/usr/local/cuda"
ENV TORCH_CUDA_ARCH_LIST="6.0 6.1 7.0 7.5 8.0 8.6+PTX 8.9"

# Install some handy tools. Even Guvcview for webcam support!
RUN set -x \
	&& apt-get update \
	&& apt-get install -y openssh-server \
	&& apt-get install -y apt-transport-https ca-certificates \
	&& apt-get install -y git vim tmux nano htop sudo curl wget gnupg2 \
	&& apt-get install -y bash-completion \
	&& rm -rf /var/lib/apt/lists/* \
	&& useradd -ms /bin/bash user \
	&& echo "user:user" | chpasswd && adduser user sudo \
	&& echo "user ALL=(ALL) NOPASSWD: ALL " >> /etc/sudoers

RUN set -x \
    && apt-get update && apt-get install ffmpeg libsm6 libxext6  -y

RUN set -x \
    && apt-get update \
    && apt-get install -y software-properties-common \
    && add-apt-repository ppa:deadsnakes/ppa \
    && apt-get update \
    && apt-get install -y python3.13 python3.13-venv python3.13-dev \
    && apt-get install -y python3.13-tk

RUN update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.12 1 \
    && update-alternatives --install /usr/bin/python3 python3 /usr/bin/python3.13 2


RUN curl -sS https://bootstrap.pypa.io/get-pip.py | python3.13

WORKDIR /home/user

RUN git clone https://github.com/facebookresearch/segment-anything-2 
RUN cd segment-anything-2 && \
    python3 -m pip install -e . -v && \
    python3 -m pip install -e ".[demo]" && \
    python3 -m pip install grpcio grpcio-tools numpy torch Pillow opencv-python matplotlib

RUN cd segment-anything-2/checkpoints && ./download_ckpts.sh && cd ..

# Copy checkpoints to where the installed sam2 package expects them
RUN mkdir -p /usr/local/lib/python3.13/dist-packages/checkpoints && \
    cp -r /home/user/segment-anything-2/checkpoints/* /usr/local/lib/python3.13/dist-packages/checkpoints/

RUN cd /home/user/segment-anything-2/sam2/configs/sam2.1/ && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_l.yaml && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_s.yaml && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_b+.yaml && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_t.yaml && \
	cd ..

# Copy the shared gRPC proto files (required by segmentation_grpc)
COPY ./gRPC_Protos/ /home/user/gRPC_Protos/

# Copy only dependency files first (for better layer caching)
COPY ./Servers/SegmentationServer/segmentation_grpc/setup.py ./Servers/SegmentationServer/segmentation_grpc/pyproject.toml /home/user/segmentation_grpc/
COPY ./Servers/SegmentationServer/segmentation_grpc/segmentation_grpc/ /home/user/segmentation_grpc/segmentation_grpc/
COPY ./Servers/SegmentationServer/segmentation_server/setup.py ./Servers/SegmentationServer/segmentation_server/pyproject.toml /home/user/segmentation_server/
COPY ./Servers/SegmentationServer/segmentation_server/segmentation_server/ /home/user/segmentation_server/segmentation_server/

# Debug: Check what packages are installed before installing
RUN python3 -m pip list | grep -i sam || echo "No SAM packages found"

# Install packages (regular install, not editable)
RUN --mount=type=cache,target=/root/.cache/pip \
    cd /home/user/segmentation_grpc && \
    python3 -m pip install --no-build-isolation .

RUN --mount=type=cache,target=/root/.cache/pip \
    cd /home/user/segmentation_server && \
    python3 -m pip install --no-build-isolation .
  
# Set ownership of the project directories to the user
RUN chown -R user:user /home/user/segmentation_grpc
RUN chown -R user:user /home/user/segmentation_server
  
# Expose the gRPC port
EXPOSE 50051
 
RUN usermod -aG dialout user
USER user

ENV PYTHONPATH=/usr/bin/python3

STOPSIGNAL SIGTERM

# Print installed packages for debugging
RUN echo "========================================" && \
    echo "INSTALLED PYTHON PACKAGES:" && \
    echo "========================================" && \
    python3 -m pip list && \
    echo "========================================"


ENTRYPOINT ["python3", "-m", "segmentation_server"]
#CMD sudo service ssh start && python3 -m segmentation_grpc && python3 -m segmentation_server --generate-grpc & /bin/bash
