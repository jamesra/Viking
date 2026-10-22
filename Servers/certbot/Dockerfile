# viking-certs: the single certificate renewer (see renew-loop.sh and docker-compose.yml here).
FROM alpine:3.20

RUN apk add --no-cache bash certbot certbot-dns-cloudflare docker-cli tzdata openssl dos2unix coreutils

# The path keeps its old name so `docker exec ... /opt/identity-certbot/...` in older tooling still works.
WORKDIR /opt/identity-certbot

COPY renew-loop.sh reload-identity.sh ./
RUN sed -i 's/\r$//' /opt/identity-certbot/*.sh && dos2unix /opt/identity-certbot/*.sh
RUN chmod +x /opt/identity-certbot/*.sh

# SOURCE_REVISION: the Viking commit, passed by the compose build (STACK_SOURCE_REVISION from stack.ps1).
ARG SOURCE_REVISION=
LABEL org.opencontainers.image.source="https://github.com/jamesra/Viking" \
      org.opencontainers.image.title="viking-certs" \
      org.opencontainers.image.revision="${SOURCE_REVISION}"

ENTRYPOINT ["bash", "/opt/identity-certbot/renew-loop.sh"]
