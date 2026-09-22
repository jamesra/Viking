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

Docker publishes **40080:80** for cleartext gRPC and **40443:443** for TLS. Host ports 80 and 443 belong to the reverse proxy. Point the router’s `segmentation.codepharm.net:443` forward at host port 40443.

Enrollment:

```bash
docker compose --env-file D:/Docker/Builds/SegmentationServer/.env --profile letsencrypt up -d
```

Without `--profile letsencrypt`, `segmentation-server` still starts and serves cleartext on host port 40080. TLS on container port 443 binds only after the certificate files exist.

## Fine-tuned checkpoint

Compose bind-mounts the host folder `D:\Docker\Run\segmentation-server` at `/models` (override with `SEGMENTATION_MODEL_HOST`). The service loads `SAM2_CHECKPOINT`, default `/models/best_TEM_model.pt`.

That file must be Meta `build_sam2` format (`{"model": state_dict}`), not the trainer's raw `best_model.pt`. Produce it with `sam2-em-export-serve` in Sam2SegmentationTrainer, then recreate `segmentation-server`. Weights load only at process start.
