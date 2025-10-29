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

RUN cd /home/user/segment-anything-2/sam2/configs/sam2.1/ && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_l.yaml && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_s.yaml && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_b+.yaml && \
	wget https://github.com/facebookresearch/sam2/blob/main/sam2/configs/sam2.1/sam2.1_hiera_t.yaml && \
	cd ..

# Copy package setup files for dependency installation
# This creates the package directories so pip install -e works
COPY ./Servers/SegmentationServer/segmentation_grpc/setup.py ./Servers/SegmentationServer/segmentation_grpc/pyproject.toml /home/user/segmentation_grpc/
COPY ./Servers/SegmentationServer/segmentation_grpc/segmentation_grpc/__init__.py /home/user/segmentation_grpc/segmentation_grpc/segmentation_grpc/
COPY ./Servers/SegmentationServer/segmentation_server/setup.py ./Servers/SegmentationServer/segmentation_server/pyproject.toml /home/user/segmentation_server/
COPY ./Servers/SegmentationServer/segmentation_server/segmentation_server/__init__.py /home/user/segmentation_server/segmentation_server/
COPY ./Servers/SegmentationServer/segmentation_server/requirements-docker.txt /home/user/segmentation_server/

# Install dependencies (this layer is cached unless setup files change)
# Install grpc package first (lighter dependency)
RUN cd /home/user/segmentation_grpc && \
    python3 -m pip install -e .

# Install server package using Docker-specific requirements (excludes sam2)
RUN cd /home/user/segmentation_server && \
    python3 -m pip install -r requirements-docker.txt && \
    python3 -m pip install -e . --no-deps && \
    python3 -m pip install debugpy

# Copy the proto file
COPY ./gRPC_Protos /home/user/gRPC_Protos

# NOW copy the full project code (changes here won't invalidate pip install cache)
COPY ./Servers/SegmentationServer/segmentation_grpc /home/user/segmentation_grpc
COPY ./Servers/SegmentationServer/segmentation_server/segmentation_server /home/user/segmentation_server/segmentation_server/
  
# Set ownership of the project directories to the user
RUN chown -R user:user /home/user/gRPC_Protos
RUN chown -R user:user /home/user/segmentation_grpc
RUN chown -R user:user /home/user/segmentation_server
  
# Expose the gRPC port and debug port
EXPOSE 50051
EXPOSE 5678

RUN usermod -aG dialout user
USER user

ENV PYTHONPATH=/usr/bin/python3

STOPSIGNAL SIGTERM

# Create a startup script that supports debugging
RUN echo '#!/bin/bash\n\
if [ "$DEBUG" = "true" ]; then\n\
    echo "Starting with debugpy on port 5678..."\n\
    python3 -m debugpy --listen 0.0.0.0:5678 --wait-for-client -m segmentation_server\n\
else\n\
    echo "Starting normally..."\n\
    python3 -m segmentation_server\n\
fi' > /home/user/start.sh && chmod +x /home/user/start.sh

ENTRYPOINT ["/home/user/start.sh"]
#CMD sudo service ssh start && python3 -m segmentation_grpc && python3 -m segmentation_server --generate-grpc & /bin/bash
