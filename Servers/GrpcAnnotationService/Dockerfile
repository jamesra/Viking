# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from project files only so a .cs edit does not re-download packages.
# Graph: GrpcAnnotationService → ConnectomeDataModelCore, AnnotationServiceTypes.gRPC,
# SectionCorrection → Geometry → Geometry.Core, GraphLib, RTree, Utils;
# GeometryOGCMapper → Geometry; AnnotationServiceTypes.gRPC → AnnotationInterfaces, Geometry, UnitsAndScale.
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
COPY Servers/SectionCorrection/SectionCorrection.csproj Servers/SectionCorrection/

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
COPY Servers/SectionCorrection/ Servers/SectionCorrection/

WORKDIR /src/Servers/GrpcAnnotationService
# Restore again so a host obj/ that slipped into COPY cannot poison assets
# with Windows NuGet fallback folders. The cache mount keeps this cheap.
RUN --mount=type=cache,id=viking-nuget,target=/root/.nuget/packages \
    dotnet publish "GrpcAnnotationService.csproj" -c Release -o /app/publish --configfile nuget.docker.config

FROM base AS final
RUN apt-get update && apt-get install -y --no-install-recommends openssl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .

# Bind-mount appsettings at run time (compose / run-with-config.cmd). Do not
# declare VOLUME on those files — Docker treats VOLUME as a directory and
# fails to start when the image already contains the files.

ENV ASPNETCORE_URLS=http://+:80;https://+:443

# HTTPS fallback when /certs/${TLS_CERT_NAME}/ (SSL_CERT_PATH/SSL_KEY_PATH, see Program.cs) is not
# mounted, so Viking (.NET Framework / WinHttpHandler) can still call gRPC over TLS; h2c on :80
# remains for tests and modern .NET clients. The self-signed localhost certificate is made when the
# container starts: a key generated at build time would ship in the published image.
COPY --chmod=755 <<'EOF' /usr/local/bin/annotation-entrypoint.sh
#!/bin/sh
set -e
if [ -z "${ASPNETCORE_Kestrel__Certificates__Default__Path:-}" ]; then
    dir=/tmp/https
    if [ ! -f "$dir/cert.pem" ]; then
        mkdir -p "$dir"
        openssl req -x509 -newkey rsa:2048 -nodes -days 825 -subj /CN=localhost \
            -addext "subjectAltName=DNS:localhost,IP:127.0.0.1" \
            -keyout "$dir/key.pem" -out "$dir/cert.pem" 2>/dev/null
        chmod 600 "$dir/key.pem"
    fi
    export ASPNETCORE_Kestrel__Certificates__Default__Path="$dir/cert.pem"
    export ASPNETCORE_Kestrel__Certificates__Default__KeyPath="$dir/key.pem"
fi
exec dotnet /app/GrpcAnnotationService.dll "$@"
EOF
# A Windows checkout (core.autocrlf) gives the heredoc CRLF line ends, which breaks the shebang.
RUN sed -i 's/\r$//' /usr/local/bin/annotation-entrypoint.sh

# SOURCE_REVISION: the Viking commit, passed by the compose build (STACK_SOURCE_REVISION from stack.ps1).
ARG SOURCE_REVISION=
LABEL org.opencontainers.image.source="https://github.com/jamesra/Viking" \
      org.opencontainers.image.title="viking-annotation" \
      org.opencontainers.image.revision="${SOURCE_REVISION}"

ENTRYPOINT ["/usr/local/bin/annotation-entrypoint.sh"]
