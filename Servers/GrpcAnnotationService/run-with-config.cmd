@echo off
echo Starting GrpcAnnotationService with configuration volume mounts...

REM Build the image first if it doesn't exist
docker build -t grpc-annotation-service -f Servers/GrpcAnnotationService/Dockerfile .

REM Run the container with volume mounts for appsettings.json
docker run -it ^
  -p 5010:80 ^
  -p 5011:443 ^
  -v "%cd%/Servers/GrpcAnnotationService/appsettings.json:/app/appsettings.json:ro" ^
  -v "%cd%/Servers/GrpcAnnotationService/appsettings.Development.json:/app/appsettings.Development.json:ro" ^
  -e ASPNETCORE_ENVIRONMENT=Development ^
  -e ASPNETCORE_URLS=http://+:80;https://+:443 ^
  --name grpc-annotation-service ^
  grpc-annotation-service

echo Container started. Access the service at:
echo HTTP  (h2c, tests):     http://localhost:5010
echo HTTPS (Viking net48):   https://localhost:5011
