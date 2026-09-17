# Annotation SQL (local test database)

Templates for a throwaway SQL Server used by gRPC annotation integration tests.
Do **not** commit filled-in copies. Real env files live under `D:\Docker\Builds\`.

## Workflow

1. Create `D:\Docker\Builds\AnnotationSql\` if needed.
2. Copy **config-template/build/env-template.txt** to
   **D:\Docker\Builds\AnnotationSql\.env** and set `MSSQL_SA_PASSWORD`.
3. Start the stack (preferred):

   ```powershell
   .\Servers\GrpcAnnotationService\scripts\Start-AnnotationTestStack.ps1 -ApplySchema
   ```

   Or manually:

   ```powershell
   docker compose --env-file D:/Docker/Builds/AnnotationSql/.env `
     -f docker-compose.yml -f docker-compose.annotation-db.yml `
     -f docker-compose.identity-devtest.yml `
     up --build -d identity-devtest annotation-sql grpc-annotation-service
   ```

4. Apply the minimal test schema (if you did not pass `-ApplySchema`):

   ```powershell
   .\Servers\AnnotationDatabase\scripts\Apply-MinimalSchema.ps1
   ```

## SA password requirements

SQL Server rejects weak SA passwords. Use at least 8 characters with upper,
lower, digit, and symbol. Example shape (change it): `AnnotationT3st!SaPass`.

## Schema application

### Option A — Minimal schema (recommended for gRPC smoke tests)

`Servers/AnnotationDatabase/scripts/minimal-schema.sql` creates just enough
tables for CRUD smoke tests:

- `StructureType`, `Structure`, `Location`, `LocationLink`, `StructureLink`
- `DeletedLocations`, `PermittedStructureLink`

It skips SSDT extras (spatial indexes, morphology UDFs, `StructureSpatialCache`
triggers, stored procedures). Geometry columns use SQL Server `geometry` so
EF Core + NetTopologySuite still work.

### Option B — Full AnnotationDatabase SSDT / sqlproj

`Servers/AnnotationDatabase/AnnotationDatabase.sqlproj` is the full SSDT project
(Sql150). Building/publishing needs Visual Studio SQL Server Data Tools (or
`SqlPackage` + a built `.dacpac`). From Visual Studio: Build the project, then
Publish to `localhost,1433` / database `AnnotationTest` with SA credentials.

Full publish is heavier and not automated by the start script yet.

### Option C — EF Core EnsureCreated

`ConnectomeDataModelCore` is a reverse-engineered model (computed geometry
columns, views, keyless `StructureLink`). `EnsureCreated` / migrations are not
a supported deploy path for this database.

## Connection strings

| Caller | Server host | Notes |
|--------|-------------|-------|
| Host-side tools / tests hitting SQL directly | `localhost,1433` | Port published by compose |
| `grpc-annotation-service` in compose | `annotation-sql,1433` | Compose service name (set by override) |
| Other containers on the host network | `host.docker.internal,1433` | When SQL is only published on the host |

`docker-compose.annotation-db.yml` injects the compose-service connection string
into `grpc-annotation-service` so you do not have to edit
`D:\Docker\Builds\GrpcAnnotationService\.env.Docker` for the test stack.
