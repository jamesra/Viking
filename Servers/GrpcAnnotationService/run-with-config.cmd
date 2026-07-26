@echo off
echo Starting GrpcAnnotationService with configuration volume mounts...

REM Build the image first if it doesn't exist
docker build -t grpc-annotation-service -f Servers/GrpcAnnotationService/Dockerfile .

REM Run the container with volume mounts for appsettings.json
docker run -it ^
  -p 5001:80 ^
  -p 5002:443 ^
  -v "%cd%/Servers/GrpcAnnotationService/appsettings.json:/app/appsettings.json:ro" ^
  -v "%cd%/Servers/GrpcAnnotationService/appsettings.Development.json:/app/appsettings.Development.json:ro" ^
  -e ASPNETCORE_ENVIRONMENT=Development ^
  -e ASPNETCORE_URLS=http://+:80;https://+:443 ^
  --name grpc-annotation-service ^
  grpc-annotation-service

echo Container started. Access the service at:
echo HTTP: http://localhost:5001
echo HTTPS: https://localhost:5002
