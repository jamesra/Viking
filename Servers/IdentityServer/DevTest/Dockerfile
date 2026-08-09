# Throwaway Identity Server used only to run the gRPC annotation integration
# tests locally (see Config.cs). Not for any real deployment.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
WORKDIR "/src/Servers/IdentityServer/DevTest"
RUN dotnet restore "DevTest.csproj" --configfile ../../GrpcAnnotationService/nuget.docker.config
RUN dotnet build "DevTest.csproj" -c Release -o /app/build --no-restore

FROM build AS publish
RUN dotnet publish "DevTest.csproj" -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "DevTest.dll"]
