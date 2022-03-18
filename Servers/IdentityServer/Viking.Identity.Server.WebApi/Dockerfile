#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:5000;https://+:5001
EXPOSE 5000
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["Viking.Identity.Server.WebApi/Viking.Identity.Server.WebApi.csproj", "Viking.Identity.Server.WebApi/"]
RUN dotnet restore "Viking.Identity.Server.WebApi/Viking.Identity.Server.WebApi.csproj"
COPY . .
WORKDIR "/src/Viking.Identity.Server.WebApi"
RUN dotnet build "Viking.Identity.Server.WebApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Viking.Identity.Server.WebApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Viking.Identity.Server.WebApi.dll"]