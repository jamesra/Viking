# AGENTS.md

## Cursor Cloud specific instructions

### Codebase overview

Viking is an annotation platform for connectomes. The actively-developed server component is the **IdentityServer subsystem** under `Servers/IdentityServer/`, which is a .NET 9.0 solution (`IdentityServer.sln`) containing:

- **IdentityServerStandalone** — Duende IdentityServer (OAuth2/OIDC auth server) on ports 5000/5001
- **Viking.Identity.Server.WebApi** — REST API for permissions management on ports 6000/6001
- **Viking.Identity.Server.WebManagement** — Admin web UI on ports 4000/4001
- Supporting libraries: `Identity.DataContext`, `Identity.Models`, `Identity.Configuration`, `Viking.Identity.Server.Extensions`

Legacy components (WCF services, desktop clients, OData) target .NET Framework 4.x and are **Windows-only** — they cannot build or run on Linux.

### Prerequisites

- **.NET 9.0 SDK** (installed to `$HOME/.dotnet`; `DOTNET_ROOT` and `PATH` configured in `~/.bashrc`)
- **Docker** with fuse-overlayfs storage driver and iptables-legacy (for nested container support)
- **SQL Server** — run as a Docker container: `mcr.microsoft.com/mssql/server:2022-latest`

### Running services

**SQL Server** must be running before starting any .NET service. Start it with:
```bash
sudo docker start sqlserver || sudo docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Glutamate88" -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest
```

Required databases (`IdentityViking`, `IdentityConfig`, `IdentityPersistedGrants`) are created by EF Core migrations on first startup — no manual DB creation needed after initial setup.

**IdentityServerStandalone** (must start before WebApi):
```bash
cd Servers/IdentityServer/IdentityServerStandalone
ASPNETCORE_ENVIRONMENT=DevelopmentTest HOSTING_ENVIRONMENT=Local dotnet run
```

**Viking.Identity.Server.WebApi**:
```bash
cd Servers/IdentityServer/Viking.Identity.Server.WebApi
ASPNETCORE_ENVIRONMENT=Development HOSTING_ENVIRONMENT=Local dotnet run
```

### Build and test

```bash
# Build the full Identity solution
cd Servers/IdentityServer && dotnet build IdentityServer.sln

# Run Identity.Tests (needs SQL Server running)
DataContext__ConnectionStrings__IdentityConnection="Server=localhost;Database={0};Trusted_Connection=False;User ID=sa;Password=Glutamate88;MultipleActiveResultSets=true;TrustServerCertificate=True" \
  dotnet test Servers/Identity.Tests/Identity.Tests.csproj
```

### Non-obvious notes

- The `.env` files in `Servers/IdentityServer/` contain configuration substitution values (e.g. `${SQL_SERVER_HOST}`). The apps use the `ConfigurationSubstitutor` NuGet package to resolve `${...}` placeholders in `appsettings.json` at runtime.
- **User secrets** store connection strings for local dev. Secrets IDs are in each project's `.csproj`. Connection strings point to `localhost,1433` with `sa`/`Glutamate88`.
- The `appsettings.DevelopmentTest.json` profile uses `Authority: "https://localhost:5001/"` which is correct for local dev.
- SSL certificate errors on Linux are expected and harmless — the servers fall back to the ASP.NET dev certificate.
- The `Identity.Tests` project uses `EnsureCreated()` (not `Migrate()`), so `HasData` seed data applied via migrations is not populated. This causes the `DatabaseComesPopulatedWithDefaults` test to fail — this is a pre-existing issue, not an environment problem.
- The WebApi redirects HTTP to HTTPS, but when no SSL cert is loaded both Kestrel ports serve HTTP. Use `curl -sk` against the HTTPS endpoints or call port 6001 directly when testing locally.
- Duende IdentityServer runs without a license key in dev mode (a warning is logged). This is expected for development.
