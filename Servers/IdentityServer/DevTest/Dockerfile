# DevTest identity server for the sign-in tests (Clients/WebAnnotationModel.gRPC.Tests): in-memory clients, the
# test user from Config.cs, HTTPS only. The container makes a self-signed certificate when it starts (a key
# generated at build time would ship in the image) and keeps it in /https, a volume shared with the annotation
# test stack so that service can trust it. Build context: Servers/IdentityServer/DevTest.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY DevTest.csproj .
RUN dotnet restore DevTest.csproj
COPY . .
RUN dotnet publish DevTest.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
RUN apt-get update && apt-get install -y --no-install-recommends openssl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=https://+:443
COPY --chmod=755 <<'SCRIPT' /usr/local/bin/devtest-entrypoint.sh
#!/bin/sh
set -e
dir=/https
mkdir -p "$dir"
if [ ! -f "$dir/cert.pem" ]; then
    openssl req -x509 -newkey rsa:2048 -nodes -days 825 -subj /CN=host.docker.internal \
        -addext "subjectAltName=DNS:host.docker.internal,DNS:localhost,DNS:identity-devtest,IP:127.0.0.1" \
        -keyout "$dir/key.pem" -out "$dir/cert.pem" 2>/dev/null
    chmod 644 "$dir/key.pem"
fi
export ASPNETCORE_Kestrel__Certificates__Default__Path="$dir/cert.pem"
export ASPNETCORE_Kestrel__Certificates__Default__KeyPath="$dir/key.pem"
exec dotnet /app/DevTest.dll "$@"
SCRIPT
RUN sed -i 's/\r$//' /usr/local/bin/devtest-entrypoint.sh
EXPOSE 443
ENTRYPOINT ["/usr/local/bin/devtest-entrypoint.sh"]
