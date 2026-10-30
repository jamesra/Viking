# ==============================================================================
# Viking Volume Annotation Services - Combined Docker Image
# ==============================================================================
#
# This Dockerfile creates a single container hosting two services for
# connectome data access:
#
# 1. ConnectomeODataV4 (Web API) - Provides OData v4 access to connectome data
# 2. DataExport (ASP.NET Core) - Exports morphology and network data
#
# Both services are hosted on IIS and accessible via different URL paths:
#   - /odata      - OData v4 RESTful API for data queries
#   - /dataexport - Network and morphology export endpoints
#
# The WCF AnnotationService is no longer part of this image: it is retired on
# dev and built and deployed from VikingLegacy. Annotation reads and writes use
# the gRPC annotation service (Servers/GrpcAnnotationService).
#
# Build from solution root:
#   docker build -f Servers/VolumeAnnotationServices/Dockerfile -t viking-annotation-services .
#
# ==============================================================================

# Escape character for PowerShell commands
# escape=`

# ==============================================================================
# Stage 1: Build the .NET Framework service (ConnectomeODataV4)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/framework/sdk:4.8-windowsservercore-ltsc2022 AS build-framework
WORKDIR /src

# Copy solution and project files
COPY Everything.sln .
COPY Directory.Build.props .
COPY NuGet.Config .

# Copy all project files for restore
COPY AnnotationInterfaces/*.csproj ./AnnotationInterfaces/
COPY AnnotationVizLib/*.csproj ./AnnotationVizLib/
COPY AnnotationVizLibODataClient/*.csproj ./AnnotationVizLibODataClient/
COPY UnitsAndScale/*.csproj ./UnitsAndScale/
COPY Geometry/*.csproj ./Geometry/
COPY SqlGeometryUtils/*.csproj ./SqlGeometryUtils/
COPY SqlServerTypesLoader/*.csproj ./SqlServerTypesLoader/
COPY Utils/*.csproj ./Utils/
COPY Triangle.NET/*.csproj ./Triangle.NET/
COPY TriangleNetGeometryExtensions/*.csproj ./TriangleNetGeometryExtensions/
COPY GeometrySqlTypeMapper/*.csproj ./GeometrySqlTypeMapper/
COPY SqlGeometryAnnotationExtensions/*.csproj ./SqlGeometryAnnotationExtensions/

# Copy server projects
COPY Servers/ConnectomeDataModel/*.csproj ./Servers/ConnectomeDataModel/
COPY Servers/WebAppSettings/*.csproj ./Servers/WebAppSettings/
COPY Servers/ConnectomeODataV4/*.csproj ./Servers/ConnectomeODataV4/

# Restore NuGet packages for .NET Framework projects
RUN nuget restore Everything.sln -ConfigFile NuGet.Config

# Copy all source files
COPY . .

# Build and publish ConnectomeODataV4
WORKDIR /src/Servers/ConnectomeODataV4
RUN msbuild ConnectomeODataV4.csproj /p:Configuration=Release /p:OutDir=C:\app\odata\ /t:Build

# ==============================================================================
# Stage 2: Build .NET 9 service (DataExport)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-windowsservercore-ltsc2022 AS build-core
WORKDIR /src

# Copy solution and project files
COPY Everything.sln .
COPY Directory.Build.props .
COPY NuGet.Config .

# Copy project files needed for DataExport
COPY AnnotationInterfaces/*.csproj ./AnnotationInterfaces/
COPY AnnotationVizLib/*.csproj ./AnnotationVizLib/
COPY AnnotationVizLibODataClient/*.csproj ./AnnotationVizLibODataClient/
COPY UnitsAndScale/*.csproj ./UnitsAndScale/
COPY Geometry/*.csproj ./Geometry/
COPY SqlGeometryUtils/*.csproj ./SqlGeometryUtils/
COPY SqlServerTypesLoader/*.csproj ./SqlServerTypesLoader/
COPY Utils/*.csproj ./Utils/
COPY Triangle.NET/*.csproj ./Triangle.NET/
COPY TriangleNetGeometryExtensions/*.csproj ./TriangleNetGeometryExtensions/
COPY GeometrySqlTypeMapper/*.csproj ./GeometrySqlTypeMapper/
COPY SqlGeometryAnnotationExtensions/*.csproj ./SqlGeometryAnnotationExtensions/
COPY Servers/WebAppSettings/*.csproj ./Servers/WebAppSettings/
COPY Servers/DataExport/*.csproj ./Servers/DataExport/

# Restore packages
RUN dotnet restore Servers/DataExport/DataExport.csproj

# Copy source files
COPY . .

# Build and publish DataExport
WORKDIR /src/Servers/DataExport
RUN dotnet publish DataExport.csproj -c Release -o /app/dataexport --no-restore

# ==============================================================================
# Stage 3: Runtime stage with IIS hosting both services
# ==============================================================================
FROM mcr.microsoft.com/dotnet/framework/aspnet:4.8-windowsservercore-ltsc2022 AS runtime

LABEL maintainer="Viking Development Team"
LABEL description="Viking Volume Annotation Services - ConnectomeODataV4, DataExport"
LABEL version="1.0"

# Install .NET 9 Runtime and ASP.NET Core Module (required for DataExport)
RUN powershell -Command `
    $ProgressPreference = 'SilentlyContinue'; `
    Write-Host 'Downloading .NET 9 Hosting Bundle...'; `
    Invoke-WebRequest -Uri 'https://download.visualstudio.microsoft.com/download/pr/90486d50-c0b0-44e2-b9fc-c380e8a320c3/092e86e5f803e6e3b726785e2e0bcdd4/dotnet-hosting-9.0.1-win.exe' -OutFile dotnet-hosting.exe -UseBasicParsing; `
    Write-Host 'Installing .NET 9 Hosting Bundle...'; `
    Start-Process dotnet-hosting.exe -ArgumentList '/quiet','/install','/norestart' -Wait; `
    Write-Host 'Cleaning up...'; `
    Remove-Item dotnet-hosting.exe -Force; `
    Write-Host '.NET 9 installed successfully'

# Enable required IIS features
RUN powershell -Command `
    Add-WindowsFeature Web-Server; `
    Add-WindowsFeature Web-Asp-Net45; `
    Add-WindowsFeature Web-Net-Ext45; `
    Add-WindowsFeature Web-ISAPI-Ext; `
    Add-WindowsFeature Web-ISAPI-Filter; `
    Add-WindowsFeature Web-Mgmt-Tools; `
    Add-WindowsFeature Web-Mgmt-Console

# Copy published applications from build stages
COPY --from=build-framework /app/odata /inetpub/wwwroot/odata
COPY --from=build-core /app/dataexport /inetpub/wwwroot/dataexport

# Copy Docker-specific configuration files
COPY Servers/ConnectomeODataV4/Web.config.docker /inetpub/wwwroot/odata/Web.config
COPY Servers/DataExport/appsettings.Docker.json /inetpub/wwwroot/dataexport/appsettings.json
COPY Servers/DataExport/web.config /inetpub/wwwroot/dataexport/web.config

# Create IIS applications with separate app pools for isolation
RUN powershell -Command `
    Import-Module WebAdministration; `
    `
    # Remove default website content `
    Remove-Item 'C:\inetpub\wwwroot\iisstart.*' -Force -ErrorAction SilentlyContinue; `
    `
    # Create application pools `
    New-WebAppPool -Name 'ConnectomeODataPool' -Force; `
    Set-ItemProperty IIS:\AppPools\ConnectomeODataPool -Name managedRuntimeVersion -Value 'v4.0'; `
    Set-ItemProperty IIS:\AppPools\ConnectomeODataPool -Name enable32BitAppOnWin64 -Value $false; `
    `
    New-WebAppPool -Name 'DataExportPool' -Force; `
    Set-ItemProperty IIS:\AppPools\DataExportPool -Name managedRuntimeVersion -Value ''; `
    Set-ItemProperty IIS:\AppPools\DataExportPool -Name enable32BitAppOnWin64 -Value $false; `
    `
    # Create IIS applications `
    New-WebApplication -Name 'odata' -Site 'Default Web Site' -PhysicalPath 'C:\inetpub\wwwroot\odata' -ApplicationPool 'ConnectomeODataPool' -Force; `
    New-WebApplication -Name 'dataexport' -Site 'Default Web Site' -PhysicalPath 'C:\inetpub\wwwroot\dataexport' -ApplicationPool 'DataExportPool' -Force

# Expose standard HTTP/HTTPS ports
EXPOSE 80
EXPOSE 443

# Health check - verifies IIS is responding
HEALTHCHECK --interval=30s --timeout=10s --retries=3 `
    CMD powershell -command `
    try { `
        $response = Invoke-WebRequest http://localhost -UseBasicParsing; `
        if ($response.StatusCode -eq 200) { exit 0 } else { exit 1 }; `
    } catch { exit 1 }

# Download and install ServiceMonitor for proper IIS container lifecycle management
ADD https://dotnetbinaries.blob.core.windows.net/servicemonitor/2.0.1.10/ServiceMonitor.exe /ServiceMonitor.exe

# Create startup diagnostic script
RUN powershell -Command `
    $script = @' `
Write-Host 'Starting Viking Volume Annotation Services...' `
Write-Host 'Services: ConnectomeODataV4, DataExport' `
Start-Service W3SVC `
Write-Host 'IIS Started' `
Get-Service W3SVC `
Write-Host 'Application Pools:' `
Import-Module WebAdministration; Get-ChildItem IIS:\AppPools `
'@; `
    $script | Out-File -FilePath C:\start.ps1 -Encoding ASCII

# Start IIS using ServiceMonitor
ENTRYPOINT ["C:\\ServiceMonitor.exe", "w3svc"]


