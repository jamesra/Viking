---
name: docker-letsencrypt-sidecar
description: >-
  Give a Docker service in this repo HTTPS from the single viking-certs renewer
  (Servers/certbot). Use when adding HTTPS, TLS, Let's Encrypt, ACME/EAB, or
  certificate auto-renewal to a container, or when changing how certificates
  are issued, published or reloaded.
---

# Shared TLS from viking-certs

There is one renewer per host, the `viking-certs` stack (`Servers/certbot/docker-compose.yml`,
`renew-loop.sh`). It keeps one certificate lineage, `TLS_CERT_NAME`, that covers every
`VIKING_HOSTNAME_*` (or `TLS_HOSTNAMES`), and publishes it to the host's `DOCKER_CERTS_DIR`:

```text
${DOCKER_CERTS_DIR}/${TLS_CERT_NAME}/fullchain.pem  privkey.pem  cert.pem  chain.pem  cert.pfx  .renewed-at
```

Do not add a per-service certbot renewer, a per-service `*-letsencrypt` volume, or a copy of
`renew-loop.sh`. Do not serve ACME challenges from the app: HTTP-01 is answered by `viking-proxy`
from the webroot the renewer writes (`<root>/Data/viking-certs/webroot`).

Modes (`TLS_MODE`): `letsencrypt` (HTTP-01 webroot), `acme-eab` (`TLS_ACME_SERVER`, `TLS_EAB_KID`,
`TLS_EAB_HMAC_KEY[_FILE]`, e.g. InCommon via Pitt IT), `manual` (PEMs dropped in by
`stack.ps1 certs manual` or copied from the web host; the loop only derives `cert.pfx` and reloads).
`renew-loop.sh --once` is the one-shot used by `stack.ps1 certs letsencrypt|acme-eab`. Old
`LETSENCRYPT_*` / `CERTBOT_*` / `ACME_*` names still work with a deprecation warning.

## Add TLS to a service

1. Mount the published certificates read-only: `- ${DOCKER_CERTS_DIR:?...}:/certs:ro`.
2. Point the app at the PEM pair: `SSL_CERT_PATH=/certs/${TLS_CERT_NAME:-viking}/fullchain.pem`,
   `SSL_KEY_PATH=/certs/${TLS_CERT_NAME:-viking}/privkey.pem` (Identity: `SSL__CertificatePath` /
   `SSL__KeyPath`). .NET services load them with a `DockerTlsCertificate.TryLoad` helper
   (`Servers/GrpcSectionCorrectionService`, `Servers/GrpcAnnotationService`): null when absent, so the
   app keeps a cleartext or dev-cert listener instead of failing.
3. Label the service `viking.tls-reload: "true"`. The renewer restarts every running container with
   that label when the published bytes change; apps read the PEMs only at start.
4. Add the service's hostname as `VIKING_HOSTNAME_<SERVICE>` (single label under `VIKING_DOMAIN`, so a
   `*.<domain>` certificate also covers it) and its upstream to `Servers/proxy` (map line in
   `templates/stream.conf.template`, env default in `Servers/proxy/docker-compose.yml`). Add the
   hostname default to the `x-viking-hostnames` block in both `Servers/proxy` and `Servers/certbot`.
5. Register the stack in `deploy/stacks.psd1` (`TLS_CERT_NAME` in `Required`) and add a run env
   example under `deploy/examples/`.
6. A non-root container on port 443 needs `NET_BIND_SERVICE`, or map host 443 to a high container port.
   The files are world-readable (644) so any uid can read them.

## Hosts

- Web host: `viking-certs` in `letsencrypt` or `acme-eab`, beside `viking-proxy`.
- Compute host (segmentation) and other hosts: copy the web host's `Certs/<TLS_CERT_NAME>/` and run
  `viking-certs` with `TLS_MODE=manual`; it restarts the local labelled containers on change.
- IIS host: import `cert.pfx` with `deploy/Install-IisCertificate.ps1`.

Docs: `Documentation/source/deployment/tls.rst`.
