@echo off
rem Must be run from a workspace where Servers\IdentityServer is NOT a symbolic link (Docker build context does not follow symlinks).
rem Folder holding the .env.All* files (passed with --env-file below)
set "ENV_DIR=D:\Docker\Builds\IdentityServer"
set "DUENDE_KEY_PATH=D:\Docker\mounted-configs\IdentityServer\Duende_License.key"

rem Build context must be Servers/ (absolute path, forward slashes for Docker)
cd /d "%~dp0\.."
set "IDENTITY_BUILD_CONTEXT=%CD:\=/%"

docker-compose down
docker-compose -f IdentityServer/docker-compose-all.yml --env-file "%ENV_DIR%\.env.All" --env-file "%ENV_DIR%\.env.All.Development" --env-file "%ENV_DIR%\.env.All.Docker" up --build