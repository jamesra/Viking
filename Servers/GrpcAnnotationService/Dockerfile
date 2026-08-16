# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from project files only so a .cs edit does not re-download packages.
# Graph: GrpcAnnotationService → ConnectomeDataModelCore, AnnotationServiceTypes.gRPC,
# GeometryOGCMapper → Geometry → Geometry.Core, GraphLib, RTree, Utils;
# AnnotationServiceTypes.gRPC → AnnotationInterfaces, Geometry, UnitsAndScale.
COPY Directory.Build.props ./
COPY Servers/GrpcAnnotationService/nuget.docker.config Servers/GrpcAnnotationService/
COPY Servers/GrpcAnnotationService/GrpcAnnotationService.csproj Servers/GrpcAnnotationService/
COPY Servers/ConnectomeDataModelCore/ConnectomeDataModelCore.csproj Servers/ConnectomeDataModelCore/
COPY gRPCAnnotationServiceTypes/AnnotationServiceTypes.gRPC.csproj gRPCAnnotationServiceTypes/
COPY GeometrySqlTypeMapper/GeometryOGCMapper.csproj GeometrySqlTypeMapper/
COPY AnnotationInterfaces/AnnotationInterfaces.csproj AnnotationInterfaces/
COPY Geometry.Core/Geometry.Core.csproj Geometry.Core/
COPY Geometry/Geometry.csproj Geometry/
COPY GraphLib/GraphLib.csproj GraphLib/
COPY RTree/RTree.csproj RTree/
COPY UnitsAndScale/UnitsAndScale.csproj UnitsAndScale/
COPY Utils/Utils.csproj Utils/

WORKDIR /src/Servers/GrpcAnnotationService
# The repo-root NuGet.Config points at Windows-only local package sources that don't
# exist in this container, so restore/publish use a container-only config here.
RUN --mount=type=cache,id=viking-nuget,target=/root/.nuget/packages \
    dotnet restore "GrpcAnnotationService.csproj" --configfile nuget.docker.config

WORKDIR /src
COPY Servers/GrpcAnnotationService/ Servers/GrpcAnnotationService/
COPY Servers/ConnectomeDataModelCore/ Servers/ConnectomeDataModelCore/
COPY gRPCAnnotationServiceTypes/ gRPCAnnotationServiceTypes/
COPY GeometrySqlTypeMapper/ GeometrySqlTypeMapper/
COPY AnnotationInterfaces/ AnnotationInterfaces/
COPY Geometry.Core/ Geometry.Core/
COPY Geometry/ Geometry/
COPY GraphLib/ GraphLib/
COPY RTree/ RTree/
COPY UnitsAndScale/ UnitsAndScale/
COPY Utils/ Utils/

WORKDIR /src/Servers/GrpcAnnotationService
# Restore again so a host obj/ that slipped into COPY cannot poison assets
# with Windows NuGet fallback folders. The cache mount keeps this cheap.
RUN --mount=type=cache,id=viking-nuget,target=/root/.nuget/packages \
    dotnet publish "GrpcAnnotationService.csproj" -c Release -o /app/publish --configfile nuget.docker.config

# Self-signed localhost cert so Viking (.NET Framework / WinHttpHandler) can
# call gRPC over TLS. h2c on :80 remains for tests and modern .NET clients.
RUN mkdir -p /https && dotnet dev-certs https -ep /https/aspnetapp.pfx -p GrpcDevCert

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /https/aspnetapp.pfx /https/aspnetapp.pfx

# Bind-mount appsettings at run time (compose / run-with-config.cmd). Do not
# declare VOLUME on those files — Docker treats VOLUME as a directory and
# fails to start when the image already contains the files.

ENV ASPNETCORE_URLS=http://+:80;https://+:443
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/https/aspnetapp.pfx
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=GrpcDevCert

ENTRYPOINT ["dotnet", "GrpcAnnotationService.dll"]
