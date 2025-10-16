@echo off
echo Starting SAM2 Docker container with configuration volume mounts...

REM Build the image first if it doesn't exist
docker build -t sam2-local-2 -f Servers/SegmentationServer/Dockerfile .

REM Remove existing container if it exists
docker rm -f sam2-dev2 2>nul

mkdir C:\Temp\.X11-unix

REM Run the container with volume mounts for configuration
docker run -it ^
  -v C:/Temp/.X11-unix:/tmp/.X11-unix ^
  -e DISPLAY ^
  -p 8080:80 ^
  -p 50051:50051 ^
  --name sam2-dev2 ^
  --gpus all ^
  sam2-local-2

echo Container started. Access the web interface at http://localhost:8080
echo gRPC service available on port 50051


