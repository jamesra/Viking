docker-compose down
docker-compose -f docker-compose-all.yml --env-file .env.All --env-file .env.All.Docker config
docker-compose -f docker-compose-all.yml --env-file .env.All --env-file .env.All.Docker up --build 