# Identity Server config template

Deployment of Identity Server is the `viking-identity` stack
(`Servers/IdentityServer/docker-compose-all.yml`, registry `deploy/stacks.psd1`).

- Run env (SQL, client secrets, ports): `deploy/examples/example.viking-identity.run.env`
  -> `<root>/Run/viking-identity/viking-identity.run.env`.
- `secrets.json` (SMTP): `deploy/examples/example.secrets.json`
  -> `<root>/Run/viking-identity/secrets/secrets.json`.
- Duende license -> `<root>/Run/viking-identity/secrets/Duende_License.key`.
- TLS: the shared certificate from the `viking-certs` stack (`<root>/Certs/<TLS_CERT_NAME>/`).
  `ssl_cert` / `ssl_key` here are no longer used by the compose file.
- Operations: `Documentation/source/deployment/identity.rst`.

Never commit filled-in copies.
