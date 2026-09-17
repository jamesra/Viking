@echo off
echo Starting SAM2 Docker container with PyCharm debugging support...
echo Container debug server: 12347 (host) -> 12348 (container)
echo gRPC port: 50051
echo.
echo To connect PyCharm debugger:
echo 1. Open PyCharm
echo 2. Go to Run -> Edit Configurations
echo 3. Add new "Python Remote Debug" configuration:
echo    - Host: localhost
echo    - Port: 12347
echo    - Path mappings: Map local project to /home/user
echo 4. Start the container first (this script)
echo 5. Then connect PyCharm to the container debug server
echo.

@echo off
echo Starting SAM2 Docker container with configuration volume mounts...

REM Remove existing container if it exists
REM docker rm -f sam2-dev2 2>nul

REM Build the image first if it doesn't exist
cd ../..
docker build -t sam2-local-2 -f Servers/SegmentationServer/Dockerfile .
cd Servers/SegmentationServer

mkdir C:\Temp\.X11-unix

REM Run the container with volume mounts for configuration
docker run -it ^
  --restart unless-stopped ^
  -v C:/Temp/.X11-unix:/tmp/.X11-unix ^
  -e DISPLAY ^
  -p 8080:80 ^
  -p 50051:50051 ^
  -p 12347:12348 ^
  -e PYCHARM_DEBUG=true ^
  --name sam2-dev2 ^
  --gpus all ^
  sam2-local-2

echo Container started. Access the web interface at http://localhost:8080
echo gRPC service available on port 50051
echo Container debug server available on port 12347 (host) -> 12348 (container)


