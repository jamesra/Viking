@echo off
echo Starting GrpcAnnotation with configuration volume mounts...

REM Build the image first if it doesn't exist
docker build -t grpc-annotation -f Servers/GrpcAnnotation/Dockerfile .

REM Run the container with volume mounts for appsettings.json
docker run -it ^
  -p 5000:80 ^
  -p 5003:443 ^
  -v "%cd%/Servers/GrpcAnnotation/appsettings.json:/app/appsettings.json:ro" ^
  -v "%cd%/Servers/GrpcAnnotation/appsettings.Development.json:/app/appsettings.Development.json:ro" ^
  -e ASPNETCORE_ENVIRONMENT=Development ^
  -e ASPNETCORE_URLS=http://+:80;https://+:443 ^
  --name grpc-annotation ^
  grpc-annotation

echo Container started. Access the service at:
echo HTTP: http://localhost:5000
echo HTTPS: https://localhost:5003












































