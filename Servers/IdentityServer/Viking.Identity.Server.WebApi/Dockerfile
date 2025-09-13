#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

RUN mkdir IdentityServer

# Copy only the projects that exist in the current workspace
COPY Identity.DataContext/ Identity.DataContext/
COPY Identity.Models/ Identity.Models/
COPY IdentityServer/Identity.Configuration/ IdentityServer/Identity.Configuration/
COPY IdentityServer/Viking.Identity.Server.Extensions/ IdentityServer/Viking.Identity.Server.Extensions/
COPY IdentityServer/Viking.Identity.Server.WebApi/ IdentityServer/Viking.Identity.Server.WebApi/

# Restore packages

WORKDIR "/src/IdentityServer/Viking.Identity.Server.WebApi"
RUN dotnet restore "Viking.Identity.Server.WebApi.csproj"

RUN dotnet build "Viking.Identity.Server.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Viking.Identity.Server.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Note: appsettings.json files are mounted as volumes in docker-compose.yml
# This allows configuration changes without rebuilding the image
ENTRYPOINT ["./Viking.Identity.Server.WebApi"]
#ENTRYPOINT ["/bin/bash"]