# GrpcSectionCorrectionService docker environment template

This folder is a **template** for `.env` files and optional mounted config.
Copy the subfolders to your local config/build locations and fill in real values.
Do not commit the filled-in copies.

## Template structure

| Folder | Contents | Copy to |
|--------|----------|---------|
| **mounted/** | appsettings.overrides.json; create `corrections/` and `cache/` | **D:\Docker\mounted-configs\SectionCorrectionService** |
| **build/** | docker-env-template.txt → `.env.Docker` | **D:\Docker\Builds\SectionCorrectionService** |

## Workflow

1. Copy **config-template/build/docker-env-template.txt** to
   **D:\Docker\Builds\SectionCorrectionService\.env.Docker** and fill in SQL mappings.
2. Copy **config-template/mounted/appsettings.overrides.json** to
   **D:\Docker\mounted-configs\SectionCorrectionService\** if you prefer file
   overrides instead of env vars.
3. Create the runtime data folders (bind-mounted into the container):
   - `D:\Docker\mounted-configs\SectionCorrectionService\corrections`
   - `D:\Docker\mounted-configs\SectionCorrectionService\cache`

Compose reads `.env.Docker` from Builds (`SECTION_CORRECTION_BUILD_ENV_PATH`).
Corrections and the stos cache are under mounted-configs (`SECTION_CORRECTION_CONFIG_PATH`).

## Ports

The compose file maps:

- container **80** → host **41080** (HTTP/2 cleartext)
- container **443** → host **41443** (HTTPS). Port 443 binds only after the certificate files exist.

5010/5011 are GrpcAnnotationService.

## Let's Encrypt

The service uses the shared `Servers/certbot` image with its own volume and renewer.
It does not share Segmentation's certificate: that lineage is `segmentation.codepharm.net`,
and that renewer only restarts `segmentation-server`.

1. Add the Let's Encrypt keys from **docker-env-template.txt** to
   **D:\Docker\Builds\SectionCorrectionService\.env.Docker**.
2. Set `LETSENCRYPT_EMAIL` and `LETSENCRYPT_PRIMARY_DOMAIN`. Keep
   `SECTION_CORRECTION_SSL_CERT_CONTAINER_PATH` and `SECTION_CORRECTION_SSL_KEY_CONTAINER_PATH`
   on `/etc/letsencrypt/live/<domain>/fullchain.pem` and `privkey.pem`.
3. Cloudflare DNS credentials are the shared file `D:\Docker\Builds\cloudflare.ini`
   (`CF_DNS_API_CREDENTIALS_HOST_PATH`). Do not add a second copy for this service.

The certbot renewer starts with the service. `docker compose up -d section-correction-service` brings it up. The first successful enroll restarts the app onto the certificate. Until then the app serves cleartext on host port 41080.

To leave the renewer stopped:

```bash
docker compose up -d --no-deps section-correction-service
docker compose stop section-correction-certbot-renewer
```

`rebuild-and-start.ps1 -DisableCertbot` does the same.

## Rebuild

`Rebuild__Cron` is a 5-field cron in UTC. Default `0 2 * * 0` is Sunday 02:00.
`Rebuild__RunOnStartup=true` publishes every Identity volume that has a VikingXML
Endpoint and an `AnnotationConnections__{VolumeName}` SQL mapping.
