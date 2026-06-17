FROM alpine:3.20

RUN apk add --no-cache bash certbot certbot-dns-cloudflare docker-cli tzdata openssl

WORKDIR /opt/identity-certbot

COPY renew-loop.sh reload-identity.sh ./
RUN sed -i 's/\r$//' /opt/identity-certbot/*.sh
RUN chmod +x /opt/identity-certbot/*.sh

ENTRYPOINT ["/opt/identity-certbot/renew-loop.sh"]
