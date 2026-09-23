# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Servers/GrpcSectionCorrectionService/nuget.docker.config Servers/GrpcSectionCorrectionService/
COPY Servers/GrpcSectionCorrectionService/GrpcSectionCorrectionService.csproj Servers/GrpcSectionCorrectionService/
COPY SectionCorrectionServiceTypes.gRPC/SectionCorrectionServiceTypes.gRPC.csproj SectionCorrectionServiceTypes.gRPC/
COPY Servers/SectionCorrection/SectionCorrection.csproj Servers/SectionCorrection/
COPY Servers/SectionCorrectionBuilder/SectionCorrectionBuilder.csproj Servers/SectionCorrectionBuilder/
COPY Servers/ConnectomeDataModelCore/ConnectomeDataModelCore.csproj Servers/ConnectomeDataModelCore/
COPY AnnotationVizLib/AnnotationVizLib.csproj AnnotationVizLib/
COPY AnnotationInterfaces/AnnotationInterfaces.csproj AnnotationInterfaces/
COPY Clients/VolumeModel/VolumeModel.csproj Clients/VolumeModel/
COPY Clients/Common/Common.csproj Clients/Common/
COPY Geometry.Core/Geometry.Core.csproj Geometry.Core/
COPY Geometry/Geometry.csproj Geometry/
COPY GraphLib/GraphLib.csproj GraphLib/
COPY RTree/RTree.csproj RTree/
COPY UnitsAndScale/UnitsAndScale.csproj UnitsAndScale/
COPY Utils/Utils.csproj Utils/
COPY SqlGeometryUtils/SqlGeometryUtils.csproj SqlGeometryUtils/
COPY Triangle.NET/Triangle/Triangle.csproj Triangle.NET/Triangle/

WORKDIR /src/Servers/GrpcSectionCorrectionService
RUN --mount=type=cache,id=viking-nuget,target=/root/.nuget/packages \
    dotnet restore "GrpcSectionCorrectionService.csproj" --configfile nuget.docker.config

WORKDIR /src
COPY Servers/GrpcSectionCorrectionService/ Servers/GrpcSectionCorrectionService/
COPY SectionCorrectionServiceTypes.gRPC/ SectionCorrectionServiceTypes.gRPC/
COPY gRPC_Protos/SectionCorrection/ gRPC_Protos/SectionCorrection/
COPY Servers/SectionCorrection/ Servers/SectionCorrection/
COPY Servers/SectionCorrectionBuilder/ Servers/SectionCorrectionBuilder/
COPY Servers/ConnectomeDataModelCore/ Servers/ConnectomeDataModelCore/
COPY AnnotationVizLib/ AnnotationVizLib/
COPY AnnotationInterfaces/ AnnotationInterfaces/
COPY Clients/VolumeModel/ Clients/VolumeModel/
COPY Clients/Common/ Clients/Common/
COPY Geometry.Core/ Geometry.Core/
COPY Geometry/ Geometry/
COPY GraphLib/ GraphLib/
COPY RTree/ RTree/
COPY UnitsAndScale/ UnitsAndScale/
COPY Utils/ Utils/
COPY SqlGeometryUtils/ SqlGeometryUtils/
COPY Triangle.NET/Triangle/ Triangle.NET/Triangle/

WORKDIR /src/Servers/GrpcSectionCorrectionService
RUN --mount=type=cache,id=viking-nuget,target=/root/.nuget/packages \
    dotnet publish "GrpcSectionCorrectionService.csproj" -c Release -o /app/publish --configfile nuget.docker.config

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "GrpcSectionCorrectionService.dll"]
