---
name: docker-letsencrypt-sidecar
description: >-
  Enroll and renew Let's Encrypt certificates for a Docker service with the
  shared Servers/certbot sidecar. Use when adding HTTPS, TLS, Let's Encrypt,
  or certificate auto-renewal to a container in this repo.
---

# Docker Let's Encrypt sidecar

Reuse `Servers/certbot`. Do not copy `renew-loop.sh` into the service. One renewer container and one letsencrypt volume per service. Do not share Identity Server's volume or domain.

Worked examples: `Servers/IdentityServer/docker-compose-all.yml` (`identity-certbot-renewer`) and root `docker-compose.yml` (`segmentation-certbot-renewer`).

## Add it to a service

1. Build the existing image from `Servers/certbot`. Set `CERTBOT_RESTART_CONTAINER` to the compose service name (fallback is `IDENTITY_CONTAINER_NAME`, then `identity-all-services`).
2. Give the renewer its own writable volume mounted at `/etc/letsencrypt`, plus certbot lib and log volumes, the Docker socket, and a read-only mount of the shared Cloudflare credentials file `D:\Docker\Builds\cloudflare.ini`. Do not copy that file per service.
3. Mount that letsencrypt volume read-only into the app. The app reads `SSL_CERT_PATH` and `SSL_KEY_PATH` at process start. The sidecar restarts the app container only when the canonical PEM bytes change.
4. Parameterize with `LETSENCRYPT_EMAIL`, `LETSENCRYPT_PRIMARY_DOMAIN`, and `CERTBOT_AUTH_METHOD` (default `dns-cloudflare`).
5. Put the service env in `D:\Docker\Builds\<Service>` from a repo `config-template`. Never commit the filled env file. The Cloudflare token stays in the shared `D:\Docker\Builds\cloudflare.ini`. PEM paths inside the container are `/etc/letsencrypt/live/<domain>/fullchain.pem` and `privkey.pem`.
6. Start the renewer with the service. Offer a disable path (`docker compose up -d --no-deps <service>` and `docker compose stop <renewer>`, or a script switch). Use a Compose profile only when the renewer must stay off unless asked for. Document `docker compose --env-file D:/Docker/Builds/<Service>/.env up -d`.
7. Port 443 inside a non-root container needs `NET_BIND_SERVICE` (and file capability on the listener binary), or map host 443 to an unprivileged container port.

If the certificate files are missing, the app keeps its non-TLS listener and does not block startup. The first successful enroll restarts it onto the certificate.
