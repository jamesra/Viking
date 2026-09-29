# 👀 Segment Anything 2 + Docker  🐳

![image](https://github.com/user-attachments/assets/7911d7b8-72a7-4c90-9da6-7a867b0136f8)


Segment Anything 2 in Docker. A simple, easy to use Docker image for Meta's SAM2 with GUI support for displaying figures, images, and masks. Built on top of the SAM2 repo: https://github.com/facebookresearch/segment-anything-2

📰 New: The project has been restructured into three separate components:
1. **segmentation_grpc**: Contains the gRPC interface definition and code generation
2. **Example**: The Python point-prompt sample lives in VikingLegacy at `Servers/SegmentationServer/Example`.
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

## Project Structure and Usage

### segmentation_grpc

This project contains the gRPC interface definition and code generation for the segmentation service.

To generate the gRPC code:

```bash
python -m segmentation_grpc
```

### Example

The command-line sample is `Servers/SegmentationServer/Example` in the VikingLegacy repo (`python client_example.py`). This tree no longer contains a copy.

### segmentation_server

This project contains the server implementation that runs in the Docker container.

To run the server:

```bash
python -m segmentation_server
```

Optional arguments:
- `--port`: The port to listen on (default: 50051)
- `--workers`: The number of worker threads (default: 10)
- `--generate-grpc`: Generate gRPC code before starting the server
