# viking-certs: the single certificate renewer (see renew-loop.sh and docker-compose.yml here).
FROM alpine:3.20

RUN apk add --no-cache bash certbot certbot-dns-cloudflare docker-cli tzdata openssl dos2unix coreutils

# The path keeps its old name so `docker exec ... /opt/identity-certbot/...` in older tooling still works.
WORKDIR /opt/identity-certbot

COPY renew-loop.sh reload-identity.sh ./
RUN sed -i 's/\r$//' /opt/identity-certbot/*.sh && dos2unix /opt/identity-certbot/*.sh
RUN chmod +x /opt/identity-certbot/*.sh

ENTRYPOINT ["bash", "/opt/identity-certbot/renew-loop.sh"]
