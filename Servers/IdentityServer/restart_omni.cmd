@echo off
rem Must be run from a workspace where Servers\IdentityServer is NOT a symbolic link (Docker build context does not follow symlinks).
set "IDENTITY_CONFIG_PATH=D:\Docker\mounted-configs\IdentityServer"
set "IDENTITY_BUILD_ENV_PATH=D:\Docker\Builds\IdentityServer"
set "SSL_CERT_PATH=%IDENTITY_CONFIG_PATH%\ssl_cert"
set "SSL_KEY_PATH=%IDENTITY_CONFIG_PATH%\ssl_key"
set "DUENDE_KEY_PATH=%IDENTITY_CONFIG_PATH%\Duende_License.key"

rem Build context must be Servers/ (absolute path, forward slashes for Docker)
cd /d "%~dp0\.."
set "IDENTITY_BUILD_CONTEXT=%CD:\=/%"

docker-compose down
docker-compose -f IdentityServer/docker-compose-all.yml --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Production" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Docker" config
docker-compose -f IdentityServer/docker-compose-all.yml --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Production" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Docker" up --build

cd /d "%~dp0"