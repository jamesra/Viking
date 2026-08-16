# GrpcAnnotationService docker environment template

This folder is a **template** for the `.env.Docker` file consumed by the
`grpc-annotation-service` entry in the repo's root `docker-compose.yml`.
Copy it to your local build location and fill in real values. Do not commit
the filled-in copy.

## Workflow

1. Copy **config-template/build/docker-env-template.txt** to
   **D:\Docker\Builds\GrpcAnnotationService\.env.Docker** (renamed on copy;
   `.env*` files are repo-gitignored so the template can't use that name here).
2. For the local annotation SQL + DevTest stack (recommended for tests):
   - Also set up **D:\Docker\Builds\AnnotationSql\.env** from
     `Servers/AnnotationDatabase/config-template/` (see that README).
   - Run:

     ```powershell
     .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -ApplySchema -Build
     ```

     `docker-compose.annotation-db.yml` points SQL at `annotation-sql`.
     `docker-compose.identity-devtest.yml` (included by the start script) points
     Identity at DevTest. Do not use the identity overlay when Viking is logged
     in against identity.codepharm.net — that JWT's issuer will not match DevTest
     and gRPC calls return HTTP 401.
3. Without the annotation-db override, set the SQL connection string yourself:
   - `annotation-sql,1433` when the SQL container shares `viking-network`, or
   - `host.docker.internal,1433` when SQL is published on the host only, or
   - an external host + `VikingDirect` (or other) SQL login.
4. For Identity Server, either:
   - **Test instance (default/recommended)** — leave the template's DevTest
     values and bring up `identity-devtest` (see
     `Servers/IdentityServer/DevTest/Config.cs` for scopes/clients/test user), or
   - **Real identity.codepharm.net instance** — comment out DevTest and
     uncomment the production block, filling in a confidential introspection
     client registered there for the `Viking.Annotation` scope.

## Why an external env file?

`Servers/GrpcAnnotationService/appsettings.json` only contains safe local
defaults (Windows-integrated SQL auth, blank Identity client credentials).
Those don't work from inside the Linux container (no domain/Kerberos ticket)
or against a remote Identity Server, so the real values are supplied as
environment variables, which override configuration keys of the same name
using ASP.NET Core's `__` (double-underscore) section-path convention. See
`Startup.cs` for the configuration keys read: `ConnectionStrings:AnnotationConnection`,
`IdentityServer:Endpoint`, `IdentityServer:ClientId`, `IdentityServer:ClientSecret`,
`IdentityServer:AllowHttpMetadata`.

## Ports

The compose file maps:

- container **80** → host **5010** (HTTP/2 cleartext, tests and .NET 5+)
- container **443** → host **5011** (HTTPS with a localhost dev cert)

Viking is .NET 4.8 and uses `WinHttpHandler`, which **cannot** do unencrypted
HTTP/2. Point VikingXML `Endpoint` at `https://localhost:5011` (not `:5010`).
(5000/5001 are already used by the identity-server stack on this host.) If
you change the HTTP port, also update the test endpoints in
`gRPC_Tests/LocationTests.cs` and
`Clients/WebAnnotationModel.gRPC.Tests/appsettings.json`.

Identity DevTest listens on host **5020**. Annotation SQL (override compose)
publishes **1433**.
