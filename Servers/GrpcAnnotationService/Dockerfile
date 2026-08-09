#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# The project graph (ConnectomeDataModelCore, AnnotationServiceTypes.gRPC, and their
# transitive references under AnnotationInterfaces/Geometry/etc.) is deep enough that
# copying the whole source tree before restore is simpler and more reliable than
# hand-maintaining a COPY line per csproj in the graph.
COPY . .
WORKDIR "/src/Servers/GrpcAnnotationService"
# The repo-root NuGet.Config points at Windows-only local package sources that don't
# exist in this container, so restore/build/publish use a container-only config here.
RUN dotnet restore "GrpcAnnotationService.csproj" --configfile nuget.docker.config
RUN dotnet build "GrpcAnnotationService.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "GrpcAnnotationService.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create volume mount points for configuration files
VOLUME ["/app/appsettings.json", "/app/appsettings.Development.json"]

ENV ASPNETCORE_URLS=http://+:80;https://+:443

ENTRYPOINT ["dotnet", "GrpcAnnotationService.dll"]
