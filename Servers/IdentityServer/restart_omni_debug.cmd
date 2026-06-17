set "IDENTITY_BUILD_ENV_PATH=D:\Docker\Builds\IdentityServer"
docker-compose -f docker-compose-all.yml down
docker-compose -f docker-compose-all.yml --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Development" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Docker" config
docker-compose -f docker-compose-all.yml --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Development" --env-file "%IDENTITY_BUILD_ENV_PATH%\.env.All.Docker" up --build