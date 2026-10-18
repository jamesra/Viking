# Deploying the gRPC server per annotation database

The goal: one command stands up the gRPC annotation service for one annotation database, with its own name, port, connection string, and identity settings, and one command removes it. Read by the scout and implementer on Deploy wakes.

## What exists

- `docker-compose.yml` plus `docker-compose.annotation-db.yml`: `grpc-annotation-service` with a local `annotation-sql` container.
- `Servers/GrpcAnnotationService/scripts/Start-AnnotationTestStack.ps1`: test stack with DevTest identity.
- `Servers/AnnotationDatabase/scripts/Apply-MinimalSchema.ps1` and `Start-AnnotationTestDab.ps1`.
- Templates in `Servers/AnnotationDatabase/config-template/` and `Servers/GrpcAnnotationService/config-template/`. Real env files live under `D:\Docker\Builds\` and are never committed.
- `Scripts/ValidateDockerSetup.ps1` and `Scripts/TestDockerImage.ps1`.

## Target

A single script, `Servers/GrpcAnnotationService/scripts/Deploy-AnnotationGrpc.ps1`, built in small candidates:

1. **Inputs:** `-Database <name>` (required), `-SqlServer`, `-Port` (default: next free), `-IdentityEndpoint`, `-Environment Test|Production`, `-EnvFile` (default `D:\Docker\Builds\GrpcAnnotationService\<Database>.env`).
2. **Per-database isolation:** compose project `viking-annotation-<database>`, with container and volume names derived from it, so several databases run side by side without port or name clashes.
3. **First run** creates the env file from the template and stops with a message listing what to fill in. **Later runs** are idempotent: build if the image is stale, start, wait for healthy.
4. **Verify** after start: gRPC health check, a metadata call, and a connection check against the named database. Print the endpoint a Viking client should use.
5. `-Remove` stops and removes the project; `-Status` lists deployed databases with endpoint and health.
6. **Safety:** refuse a production database unless `-Environment Production` and `-Confirm` are both given; never print or log secrets; never run schema changes against an existing database (schema is applied only to the throwaway test database).
7. Mixing a production Identity token with DevTest Identity is a 401, so identity settings come from `-Environment` only.

The script, the compose files, and the READMEs stay consistent: a change to one updates the others in the same commit.

## Gates specific to deploy

- Run `-WhatIf` and `Scripts/ValidateDockerSetup.ps1`. For behavior changes, deploy a throwaway database to local Docker, run the verify step, then `-Remove`.
- Do not change `deploy-viking-test` or the Velopack client feed; that is the client publish path.
- A deploy target beyond local Docker (IIS, a remote host) is a proposal.
