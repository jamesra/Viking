# GrpcSectionCorrectionService config template

Deployment of the section-correction service is the `viking-corrections` stack
(`Servers/GrpcSectionCorrectionService/docker-compose.yml`, registry `deploy/stacks.psd1`).

- Run env to fill in: `deploy/examples/example.viking-corrections.run.env`
  (copy to `<root>/Run/viking-corrections/viking-corrections.run.env`).
- TLS comes from the shared `viking-certs` renewer; the per-service certbot renewer is gone.
- Operations (rebuild schedule, `correction-menu`, ports, data folders):
  `Documentation/source/deployment/corrections.rst`.

`mounted/appsettings.overrides.json` is an optional file-based alternative to the env vars.
