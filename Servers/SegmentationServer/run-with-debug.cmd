@echo off
echo Starting SAM2 gRPC container with VS Code debugpy on 5678...
cd /d "%~dp0..\.."
docker build -t sam2-local-2 -f Servers/SegmentationServer/Dockerfile .
docker run -it --restart unless-stopped --gpus all -p 50051:50051 -p 5678:5678 -e VS_CODE_DEBUG=true --name sam2-dev2 sam2-local-2
echo gRPC service available on port 50051
echo VS Code debug port available on port 5678
