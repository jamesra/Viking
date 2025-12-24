@echo off
echo Starting SAM2 Docker container with VS Code debugging support...
echo VS Code debug port: 5678
echo gRPC port: 50051
echo.
echo To connect VS Code debugger:
echo 1. Open VS Code
echo 2. Go to Run and Debug (Ctrl+Shift+D)
echo 3. Create launch.json with:
echo    {
echo      "name": "Python: Remote Attach",
echo      "type": "python",
echo      "request": "attach",
echo      "connect": {
echo        "host": "localhost",
echo        "port": 5678
echo      },
echo      "pathMappings": [
echo        {
echo          "localRoot": "${workspaceFolder}",
echo          "remoteRoot": "/home/user"
echo        }
echo      ]
echo    }
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
  -v C:/Temp/.X11-unix:/tmp/.X11-unix ^
  -e DISPLAY ^
  -p 8080:80 ^
  -p 50051:50051 ^
  -p 5678:5678 ^
  -e VS_CODE_DEBUG=true ^
  --name sam2-dev2 ^
  --gpus all ^
  sam2-local-2

echo Container started. Access the web interface at http://localhost:8080
echo gRPC service available on port 50051
echo VS Code debug port available on port 5678


