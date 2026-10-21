# GrpcAnnotationService config template

Deployment of the gRPC annotation service is the `viking-annotation` stack
(`Servers/GrpcAnnotationService/docker-compose.yml`, registry `deploy/stacks.psd1`).

- Run env to fill in: `deploy/examples/example.viking-annotation.run.env`
  (copy to `<root>/Run/viking-annotation/viking-annotation.run.env`).
- Operations, ports, TLS, `Deploy-AnnotationGrpc.ps1` and the compose variables:
  `Documentation/source/deployment/annotation_grpc.rst`.
- Local test stack (annotation-sql + gRPC): `Servers/GrpcAnnotationService/scripts/Start-AnnotationTestStack.ps1`,
  see `Servers/AnnotationDatabase/config-template/README.md`.

`build/docker-env-template.txt` stays here as the per-database env template that
`Servers/GrpcAnnotationService/scripts/Deploy-AnnotationGrpc.ps1` copies; never commit a filled-in copy.
