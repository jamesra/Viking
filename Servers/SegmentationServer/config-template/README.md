# Segmentation server config template

Deployment of the segmentation servers is the `viking-segmentation` stack
(`Servers/SegmentationServer/docker-compose.yml`, registry `deploy/stacks.psd1`; GPU compute host).
It replaces the per-service certbot renewer and the generated `docker-compose.models.yml`.

- Run env to fill in: `deploy/examples/example.viking-segmentation.run.env`
  (copy to `<root>/Run/viking-segmentation/viking-segmentation.run.env`).
- Models, profiles (`seg-v3`, `seg-v4`, `seg-sem-v1`, `models`), ports, TLS and caches:
  `Documentation/source/deployment/segmentation.rst`.
