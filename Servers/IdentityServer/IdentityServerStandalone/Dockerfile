#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:6000;https://+:6001
EXPOSE 6000
EXPOSE 6001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src 
 
COPY Identity.DataContext/ Identity.DataContext/
COPY Identity.Models/ Identity.Models/
COPY IdentityServer/Identity.Configuration/ IdentityServer/Identity.Configuration/
COPY IdentityServer/IdentityServerStandalone/ IdentityServer/IdentityServerStandalone/ 

WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN dotnet restore "IdentityServerStandalone.csproj"  
RUN dotnet build "IdentityServerStandalone.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IdentityServerStandalone.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Note: appsettings.json files are mounted as volumes in docker-compose.yml
# This allows configuration changes without rebuilding the image
ENTRYPOINT ["./Viking.Identity.Server.Standalone"]
#ENTRYPOINT ["/bin/bash"]