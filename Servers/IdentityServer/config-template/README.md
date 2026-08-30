# Identity Server config and build templates

This folder is a **template** for secrets, certificates, and .env files. Copy the subfolders to your local config/build locations and fill in real values. Do not commit filled-in copies.

## Template structure

| Folder | Contents | Copy to |
|--------|----------|---------|
| **mounted-config/** | ssl_cert, ssl_key, Duende_License.key, secrets.json, secrets.json.example | **D:\Docker\mounted-configs\IdentityServer** |
| **build/** | env.All.example, env.All.Docker.example, env.All.Production.example, env.All.Development.example | **D:\Docker\Builds\IdentityServer** (rename to `.env.All`, `.env.All.Docker`, etc.) |

## Workflow

1. Copy **config-template/mounted-config/** to **D:\Docker\mounted-configs\IdentityServer**. Replace template files with real certs, keys, and secrets.
2. Copy **config-template/build/env.All.example** (and the other `env.All.*.example` files) to **D:\Docker\Builds\IdentityServer**, rename them to `.env.All`, `.env.All.Docker`, `.env.All.Production`, and `.env.All.Development`, and fill in real values.
3. Do not commit the filled-in copies. Restart scripts and docker-compose read from those paths.

## Mounted config (D:\Docker\mounted-configs\IdentityServer)

Holds runtime certificates and secrets (mapped into the container):

- **ssl_cert** – TLS certificate (PEM). Replace with e.g. fullchain.pem contents.
- **ssl_key** – TLS private key (PEM). Replace with e.g. privkey.pem contents.
- **Duende_License.key** – Duende IdentityServer license key file.
- **secrets.json** – App configuration overrides (optional). See secrets.json.example for structure.

Restart scripts set `IDENTITY_CONFIG_PATH` to this folder so compose finds ssl_cert, ssl_key, Duende_License.key, and secrets.json.

## Builds (D:\Docker\Builds\IdentityServer)

Holds the .env files used when running `docker-compose` (build/up):

- **.env.All** – Shared defaults (SQL_* host/port/user/password, DB names, AUTHORITY, **IDENTITY_SERVER_SECRET**, **SBFSEM_TOOLS_CLIENT_SECRET**).
- **.env.All.Docker** – Docker overrides (SSL_CERT_PATH, SSL_KEY_PATH, DUENDE_KEY_PATH; optional ACME_*).
- **.env.All.Production** – Production overrides (used by restart_omni.cmd).
- **.env.All.Development** – Development overrides (used by restart_omni_debug.cmd).

Restart scripts set `IDENTITY_BUILD_ENV_PATH` to this folder. Compose uses it for `env_file` and .env volume mounts so the container receives the same values.

## Management app ports and SSL

The Management website listens on **port 80 (HTTP) and 443 (HTTPS)** by default inside the container. TLS is **pass-through**: the Management app terminates HTTPS itself using certificates loaded from files (or from a directory populated by a Let's Encrypt / certbot sidecar).

## Let's Encrypt (HTTP-01) and certbot sidecar

**Certbot is disabled by default.** The certbot service is in the `certbot` profile and is not started with a normal `docker-compose up`. To use Let's Encrypt:

1. Start with the certbot profile: `docker-compose --profile certbot up -d` (or include `--profile certbot` in your restart script).

2. Set in your build .env (e.g. `.env.All.Docker` in Builds):
   - **ACME_DOMAIN** – Your public DNS name (must resolve to the host; use DDNS if the IP is dynamic).
   - **ACME_EMAIL** – Email for Let's Encrypt expiry/account notices.
   - **ACME_STAGING** – Optional; set to `true` to use Let's Encrypt staging (no rate limits).
   - **CERTIFICATES_PATH** – Optional; directory where certbot writes certs (default `./certificates`).

3. Point Docker secrets at the same directory so the app loads the issued certs (e.g. set SSL_CERT_PATH and SSL_KEY_PATH in mounted-configs or in .env.All.Docker to the fullchain.pem and privkey.pem paths).

4. The **acme-challenge** volume is shared between the Management app and the certbot service. The Management app serves `/.well-known/acme-challenge/<token>` so Let's Encrypt can validate the domain.

5. After renewal, restart the identity server container so it picks up the new cert (Docker secrets are read at container start).

## secrets.json keys

The WebManagement app loads `secrets.json` last (optional). Use it to override sensitive values. See **mounted-config/secrets.json.example** for the structure. Main sections:

- **VikingIdentityServerOptions** – `Secret`, `Authority`, `MetadataAddress`, `ApiScopes`.

The Management app also expects **IDENTITY_SERVER_SECRET** for configuration substitution (used in appsettings for `VikingIdentityServerOptions:Secret` and `OAuth2IntrospectionOptions:ClientSecret`). Set it in your build .env (e.g. `.env.All`) so docker-compose passes it into the container. It should match the secret in secrets.json if you use both.
- **SSL** – `CertificatePath`, `KeyPath`, `Password`.
- **Email** – SMTP settings.
- **WebApiOptions** – `BaseUrl` for the Identity WebAPI.

Real values belong in your copied config folder, not in the repo.
