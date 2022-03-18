#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:6000;https://+:6001
EXPOSE 6000
EXPOSE 6001

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["nuget.config", "."]
COPY ["IdentityServerStandalone/IdentityServerStandalone.csproj", "IdentityServerStandalone/"]
RUN dotnet restore "IdentityServerStandalone/IdentityServerStandalone.csproj"
COPY . .
WORKDIR "/src/IdentityServerStandalone"
RUN dotnet build "IdentityServerStandalone.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IdentityServerStandalone.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IdentityServerStandalone.dll"]