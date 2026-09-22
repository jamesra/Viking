@echo off
REM Headless gRPC SAM2 server. Build from the repository root.
cd /d "%~dp0..\.."
docker build -t sam2-local-2 -f Servers/SegmentationServer/Dockerfile .
docker run -it --rm --gpus all -p 40080:80 --name sam2-dev2 sam2-local-2 --port 80
