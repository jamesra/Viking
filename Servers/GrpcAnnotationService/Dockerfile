#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["Servers/GrpcAnnotationService/gRPCAnnotationService.csproj", "Servers/GrpcAnnotationService/"]
COPY ["gRPCAnnotationServiceTypes/AnnotationServiceTypes.gRPC.csproj", "gRPCAnnotationServiceTypes/"]
COPY ["Geometry/Geometry.csproj", "Geometry/"]
COPY ["Clients/Common/Common.csproj", "Clients/Common/"]
COPY ["Utils/Utils.csproj", "Utils/"]
COPY ["UnitsAndScale/UnitsAndScale.csproj", "UnitsAndScale/"]
COPY ["RTree/RTree.csproj", "RTree/"]
COPY ["AnnotationInterfaces/AnnotationInterfaces.csproj", "AnnotationInterfaces/"]
COPY ["Servers/ConnectomeDataModelCore/ConnectomeDataModelCore.csproj", "Servers/ConnectomeDataModelCore/"]
COPY ["Servers/Libs/NetTopologySuite.IO.SqlServerBytes/src/NetTopologySuite.IO.SqlServerBytes/NetTopologySuite.IO.SqlServerBytes.csproj", "Servers/Libs/NetTopologySuite.IO.SqlServerBytes/src/NetTopologySuite.IO.SqlServerBytes/"]
COPY ["Servers/Libs/EntityFrameworkExtras/EntityFrameworkExtras.EF6/EntityFrameworkExtras.EF6.csproj", "Servers/Libs/EntityFrameworkExtras/EntityFrameworkExtras.EF6/"]
RUN dotnet restore "Servers/GrpcAnnotationService/gRPCAnnotationService.csproj"
COPY . .
WORKDIR "/src/Servers/GrpcAnnotationService"
RUN dotnet build "gRPCAnnotationService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "gRPCAnnotationService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "gRPCAnnotationService.dll"]