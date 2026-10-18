# Scope: tracks, repos, and builds

Read by the scout and the implementer. Protected areas are in [protected.md](protected.md).

## Tracks

Each wake works one track. The scout rotates tracks as well as categories (see [categories.md](categories.md)) and prefers `STEERING.md` `focus:`.

| Track | Repo / branch | Paths | Build and test |
|-------|---------------|-------|----------------|
| **gRPC client** | `d:\src\git\Viking` / `dev` | `Clients/WebAnnotationModel.gRPC`, `Clients/WebAnnotationModel.gRPC.Tests`, client-side consumers of `gRPC_Protos` | `Clients/Viking.sln` and the gRPC test project |
| **Server** | `d:\src\git\Viking` / `dev` | `Servers/GrpcAnnotationService`, `Servers/GrpcSectionCorrectionService`, `Servers/DataExport`, `Servers/ConnectomeDataModelCore`, their tests and Dockerfiles | `gRPC.sln`, `Servers/GrpcAnnotationService.sln`, covering test projects |
| **SQL** | `d:\src\git\Viking` / `dev` | `Servers/AnnotationDatabase` (`dbo`, `graph`, `Storage`, `scripts`, `AnnotationDatabaseTest`); `Servers/SQL` is read-only reference | `AnnotationDatabaseTest` against the throwaway SQL container; `.sqlproj` build |
| **Parity** | `d:\src\git\Viking` / `dev`; `d:\src\git\VikingLegacy` / `Legacy` read-only | the matrix and benchmarks in [parity.md](parity.md) | paired benchmarks |
| **Deploy** | `d:\src\git\Viking` / `dev` | `Servers/GrpcAnnotationService/scripts`, `docker-compose*.yml`, config templates; see [deploy.md](deploy.md) | `-WhatIf` run, `Scripts/ValidateDockerSetup.ps1` |
| **Docs** | `d:\src\git\Viking` / `dev` | see [docs.md](docs.md) | documented commands actually run |

## Rules

- Confirm the branch before the first edit. Edit only `dev`, never `main` or `master`.
- Do not edit generated code, comment code out to compile, or push. Read `d:\src\git\VikingLegacy` only for parity; never edit it, since the client loop owns it.
- Stage by path only.
- **`.proto` files** live only in `gRPC_Protos`. A field number, name, type, or enum value change is a wire-format change: proposal only.
- **Config and secrets.** Connection strings and credentials come from configuration (`appsettings`, `.env` under `D:\Docker\Builds\`). Never write secrets, filled-in env files, or `secrets.json` into a commit, the ledger, or a report.
- **No production data.** SQL and server tests run only against the throwaway SQL container (`docker-compose.annotation-db.yml`, database `AnnotationTest`), never against `OpR-Marc-DB2` or any real annotation database.
