# Segmentation server TLS environment template

This folder is a **template** for the Let's Encrypt env file. Copy it to the local build path and fill in real values. Do not commit the copy or `cloudflare.ini`.

| Folder | Contents | Copy to |
|--------|----------|---------|
| **build/** | docker-env-template.txt | **D:\Docker\Builds\SegmentationServer\.env** |

1. Copy **config-template/build/docker-env-template.txt** to **D:\Docker\Builds\SegmentationServer\.env**.
2. Set `LETSENCRYPT_EMAIL`, `LETSENCRYPT_PRIMARY_DOMAIN`, and the two `SEGMENTATION_SSL_*` paths so the domain matches.
3. Cloudflare DNS credentials are the shared file `D:\Docker\Builds\cloudflare.ini` (`CF_DNS_API_CREDENTIALS_HOST_PATH`). Do not add a per-service copy.

`CERTBOT_AUTH_METHOD` defaults to `dns-cloudflare`.

PEM paths inside the container are `/etc/letsencrypt/live/<domain>/fullchain.pem` and `privkey.pem`.

Docker publishes **40443:443** for gRPC over TLS. There is no cleartext gRPC port. Host ports 80 and 443 belong to the reverse proxy. Point the router’s `segmentation.codepharm.net:443` forward at host port 40443.

The optional point-prompt page listens on container port **8443** (host **40444**) only when `SEGMENTATION_DEMO_SITE=1`. It uses the same PEM files as gRPC TLS. While the flag is off nothing accepts connections on 8443. Forward `segmentation.codepharm.net:40444` only if that page should be reachable. `segmentation-certbot-renewer` (profile `letsencrypt`) enrolls the certificate and restarts `segmentation-server` when it renews.

Enrollment:

```bash
docker compose --env-file D:/Docker/Builds/SegmentationServer/.env --profile letsencrypt up -d
```

`segmentation-server` only listens with TLS, so it needs the certificate files. At start it waits up to `SEGMENTATION_TLS_WAIT_SECONDS` (default 120) for `SSL_CERT_PATH` and `SSL_KEY_PATH` to appear, then exits with a message naming the missing files; `restart: unless-stopped` starts it again. Bring it up together with the renewer (`--profile letsencrypt`) the first time, or provide the PEM files some other way. For a local run without Let's Encrypt, generate a self-signed pair with `python -m segmentation_server.dev_cert --out ./dev-cert` and point `SSL_CERT_PATH` and `SSL_KEY_PATH` at it.

## Fine-tuned checkpoint

Compose bind-mounts the host folder `D:\Docker\Run\segmentation-server` at `/models` (override with `SEGMENTATION_MODEL_HOST`). The service loads `SAM2_CHECKPOINT`, default `/models/best_TEM_model.pt`.

That file must be Meta `build_sam2` format (`{"model": state_dict}`), not the trainer's raw `best_model.pt`. Produce it with `sam2-em-export-serve` in Sam2SegmentationTrainer, then recreate `segmentation-server`. Weights load only at process start.
