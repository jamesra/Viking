#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

# Install curl for health checks and debugging
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
# Use the existing 'app' user (UID 1654) that comes with the .NET base image


#Install the project dependencies
WORKDIR /src 
 
COPY Identity.DataContext/ Identity.DataContext/
COPY Identity.Models/ Identity.Models/
COPY IdentityServer/Identity.Configuration/ IdentityServer/Identity.Configuration/
COPY IdentityServer/IdentityServerStandalone/ IdentityServer/IdentityServerStandalone/  

WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN dotnet restore "IdentityServerStandalone.csproj"
RUN dotnet build "IdentityServerStandalone.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
WORKDIR "/src/IdentityServer/IdentityServerStandalone"
RUN echo "Current directory contents:" && ls -la
RUN echo "Attempting to publish..." && dotnet publish "IdentityServerStandalone.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --runtime linux-x64 --self-contained false --verbosity detailed
#RUN echo "Publish directory contents:" && ls -la /app/publish/

# Generate Data Protection keys during build
FROM build AS keygenerator
WORKDIR /app
# Copy the KeyGenerator project
COPY IdentityServer/KeyGenerator/ IdentityServer/KeyGenerator/
# Build and run the KeyGenerator to create Data Protection keys
WORKDIR /app/IdentityServer/KeyGenerator
RUN dotnet restore
RUN dotnet build -c Release
RUN dotnet run /app/DataProtectionKeys VikingIdentityServer

RUN mkdir /app/wwwroot/

#Assemble the final image
FROM base AS final
WORKDIR /app

#Copy the built app
COPY --from=publish /app/publish .
#RUN echo "Files copied from publish stage:" && ls -la /app/

# # Conditionally copy duende_key secret file
# ARG DUENDE_KEY_PATH
# RUN echo "Duende License Key Path:$ENV DUENDE_KEY_PATH"
# RUN if [ -n "$DUENDE_KEY_PATH" ]; then \
#         echo "Copying duende_key from: $DUENDE_KEY_PATH" && \
#         cp "$DUENDE_KEY_PATH" /app/ && \
#         echo "Successfully copied duende_License.key file"; \
#     else \
#         echo "WARNING: DUENDE_KEY_PATH not defined - duende_key file will not be available"; \
#         echo "To include duende_key, set build arg: --build-arg DUENDE_KEY_PATH=/path/to/duende_key"; \
#     fi

# Copy pre-generated Data Protection keys
COPY --from=keygenerator /app/DataProtectionKeys /app/DataProtectionKeys

RUN mkdir -p /app/keys 

# Debug: List files to verify executable exists
#RUN ls -la /app/

# Set proper ownership and permissions for all files
RUN chown -R app:app /app
RUN chmod +x /app/Viking.Identity.Server.Standalone.dll


# Switch to non-root user
USER 1654

# Note: appsettings.json files are mounted as volumes in docker-compose.yml
# This allows configuration changes without rebuilding the image
ENTRYPOINT ["dotnet", "./Viking.Identity.Server.Standalone.dll"]
#ENTRYPOINT ["/bin/bash"]