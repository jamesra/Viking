@echo off
echo Starting SAM2 gRPC container with PyCharm debug on host 12347 -^> container 12348...
cd /d "%~dp0..\.."
docker build -t sam2-local-2 -f Servers/SegmentationServer/Dockerfile .
docker run -it --restart unless-stopped --gpus all -p 50051:50051 -p 12347:12348 -e PYCHARM_DEBUG=true --name sam2-dev2 sam2-local-2
echo gRPC service available on port 50051
echo PyCharm debug port available on host 12347
