@echo off
echo Starting SAM2 gRPC container (port 50051)...
cd /d "%~dp0..\.."
docker build -t sam2-local-2 -f Servers/SegmentationServer/Dockerfile .
docker run -it --restart unless-stopped --gpus all -p 50051:50051 --name sam2-dev2 sam2-local-2
echo gRPC service available on port 50051
