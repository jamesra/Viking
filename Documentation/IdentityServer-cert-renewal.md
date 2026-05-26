# Identity TLS Auto-Renewal (Identity-Only Stack)

This document describes TLS issuance and renewal for `identity-all-services` without any dependency on the separate `ReverseProxy` stack.

## Scope

- Uses only `Servers/IdentityServer/docker-compose-all.yml`.
- Does not require or modify `D:/Docker/Builds/ReverseProxy`.
- Uses Let's Encrypt via a dedicated `identity-certbot-renewer` service.

## How It Works

1. `identity-certbot-renewer` manages certificates in shared Docker volumes:
   - `/etc/letsencrypt`
   - `/var/lib/letsencrypt`
   - `/var/log/letsencrypt`
2. `identity-all-services` mounts:
   - `/etc/letsencrypt` (read-only cert files)
   - `/.well-known/acme-challenge` webroot content via a shared volume (legacy HTTP-01 fallback)
3. Certbot uses Cloudflare DNS-01 (`--dns-cloudflare`) by default, so host port 80 routing is not required for validation.
4. `identity-all-services` reads:
   - `IDENTITY_SSL_CERT_CONTAINER_PATH`
   - `IDENTITY_SSL_KEY_CONTAINER_PATH`
5. On successful renewal, the renewer restarts `identity-all-services` so .NET reloads the new certificate files.

## Environment Configuration

Populate these values in your deployed `.env.All` (and `.env.All.Docker` if needed):

- `LETSENCRYPT_EMAIL`
- `LETSENCRYPT_PRIMARY_DOMAIN`
- `LETSENCRYPT_ADDITIONAL_DOMAINS` (optional, comma-separated)
- `IDENTITY_SSL_CERT_CONTAINER_PATH`
- `IDENTITY_SSL_KEY_CONTAINER_PATH`

Optional controls:

- `CERTBOT_RENEW_INTERVAL_HOURS` (default `12`)
- `CERTBOT_AUTH_METHOD` (default `dns-cloudflare`; fallback `webroot`)
- `CF_DNS_API_CREDENTIALS_HOST_PATH` (default `D:/Docker/Builds/IdentityServer/cloudflare.ini`)
- `CF_DNS_API_CREDENTIALS_FILE` (default `/run/secrets/cloudflare.ini`)
- `CF_DNS_PROPAGATION_SECONDS` (default `60`)
- `CERTBOT_WEBROOT_PATH` (default `/var/www/certbot`, only for `webroot` mode)
- `CERTBOT_STAGING` (default `false`)
- `CERTBOT_KEY_TYPE` (default `rsa`)
- `IDENTITY_CONTAINER_NAME` (default `identity-all-services`)
- `DOCKER_SOCKET_PATH` (default `/var/run/docker.sock`)

Cloudflare credentials file format (`cloudflare.ini`):

```ini
dns_cloudflare_api_token=YOUR_TOKEN_HERE
```

Use an API token scoped to the target zone with at least **Zone:DNS Edit**.

Reference sample:

- `Servers/identity-all.auto-renew.sample`
- `Servers/cloudflare.ini.template`

## Start / Restart

From `Servers/IdentityServer/`:

```bash
docker compose -f docker-compose-all.yml \
  --env-file D:/Docker/Builds/IdentityServer/.env.All \
  --env-file D:/Docker/Builds/IdentityServer/.env.All.Docker \
  up -d --build
```

## Verification Checklist

1. Confirm renewer is running:

```bash
docker ps --filter name=identity-certbot-renewer
```

2. Check certificate dates from inside the identity container:

```bash
docker exec identity-all-services openssl x509 -in "$IDENTITY_SSL_CERT_CONTAINER_PATH" -noout -dates
```

3. Confirm public endpoint cert dates:

```bash
openssl s_client -connect identity.example.com:443 -servername identity.example.com < /dev/null 2>/dev/null | openssl x509 -noout -dates -issuer -subject
```

## Manual Renewal (Break-Glass)

To force an immediate renewal attempt:

```bash
docker exec identity-certbot-renewer certbot renew \
  --dns-cloudflare \
  --dns-cloudflare-credentials /run/secrets/cloudflare.ini \
  --dns-cloudflare-propagation-seconds 60 \
  --deploy-hook "/opt/identity-certbot/reload-identity.sh"
```

## Troubleshooting

- DNS challenge fails:
  - Confirm `cloudflare.ini` exists at `CF_DNS_API_CREDENTIALS_HOST_PATH` and is mounted to `/run/secrets/cloudflare.ini`.
  - Confirm token permission includes Zone DNS edit for the `LETSENCRYPT_PRIMARY_DOMAIN` zone.
  - Increase `CF_DNS_PROPAGATION_SECONDS` if TXT records take longer to propagate.
- HTTP-01 fallback (`CERTBOT_AUTH_METHOD=webroot`) returns 404/unauthorized:
  - Confirm `CERTBOT_WEBROOT_PATH` is `/var/www/certbot`.
  - Confirm `identity-acme-webroot` is mounted in both services.
  - Confirm the challenge path is reachable on the public domain over HTTP.
  - Confirm `identity-all-services` owns host port `80` (or `IDENTITY_ACME_HTTP_PORT=80`).
- Renewal succeeds but old cert is still served:
  - Verify `IDENTITY_CONTAINER_NAME` matches the running identity container.
  - Check renewer logs for deploy hook output.
- Cert path mismatch:
  - Ensure `IDENTITY_SSL_CERT_CONTAINER_PATH` and `IDENTITY_SSL_KEY_CONTAINER_PATH` point to the primary domain's live cert files.
- Let's Encrypt rate limits during testing:
  - Set `CERTBOT_STAGING=true` until configuration is validated.
