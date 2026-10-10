IdentityManager AspNetIdentity
===========================================
[![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/IdentityManager/IdentityManager?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

## Overview ##

IdentityManager AspNetIdentity is an IdentityManagerService implementation for IdentityManager that uses ASP.NET Identity as the identity management system. In other words, you're using IdentityManager and you want to use ASP.NET Identity as your database for users, then this is the project you're looking for.

More details can be found on the [IdentityManager wiki](https://github.com/IdentityManager/IdentityManager/wiki).

---

## Configuration and Secrets Management

**Important**: This solution uses .NET User Secrets to store sensitive configuration data like database connection strings with credentials. These secrets are stored outside the repository and are automatically ignored by git.

### User Secrets Overview

User secrets are stored in user-specific directories:
- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`
- **Linux/Mac**: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`

### Projects Using User Secrets

The following projects are configured to use user secrets:

#### 1. IdentityServerStandalone
- **User Secrets ID**: `330c09e3-6f93-4770-9ebc-69a8329329a3`
- **Required Secrets**:
  - `ConnectionStrings:IdentityConnection` - Database connection string with credentials
  - `ConnectionStrings:PersistedGrantConnection` - Persisted grants database connection string
  - `ConnectionStrings:ConfigConnection` - Configuration database connection string

```bash
cd IdentityServerStandalone
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

#### 2. Viking.Identity.Server.WebApi
- **User Secrets ID**: `aspnet-Viking.Identity.Server.WebApi-489B09E1-41D8-42F9-9F75-CD8530619CD5`
- **Required Secrets**:
  - `ConnectionStrings:IdentityConnection` - Database connection string with credentials
  - `VikingIdentityServerOptions:Secret` - Identity Server client secret

```bash
cd Viking.Identity.Server.WebApi
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
dotnet user-secrets set "VikingIdentityServerOptions:Secret" "your-identity-server-secret"
```

#### 3. Viking.Identity.Server.WebManagement
- **User Secrets ID**: `aspnet-IdentityServer-5FE82F6F-0884-4871-829E-BCFE700497A2`
- **Required Secrets**:
  - `ConnectionStrings:IdentityConnection` - Database connection string with credentials
  - `OAuth2IntrospectionOptions:ClientSecret` - OAuth2 client secret (if not using environment variable substitution)

```bash
cd Viking.Identity.Server.WebManagement
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

#### 4. Identity.DataContext (For Design-Time Operations)
- **User Secrets ID**: `aspnet-Identity.DataContext-9A8B7C6D-5E4F-3A2B-1C0D-9E8F7A6B5C4D`
- **Required Secrets**:
  - `ConnectionStrings:IdentityConnection` - Database connection string for Entity Framework Core tools (migrations, etc.)

```bash
cd ../Identity.DataContext
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
```

### Generic Connection String Examples

**SQL Server with Integrated Security (Windows Authentication)**:
```
Server=YourServer;Database=IdentityViking;Trusted_Connection=True;Integrated Security=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

**SQL Server with SQL Authentication**:
```
Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True
```

**SQL Server with Custom Port**:
```
Server=YourServer,1433;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True
```

### Managing User Secrets

```bash
# List all secrets for a project
dotnet user-secrets list

# Get a specific secret
dotnet user-secrets get "ConnectionStrings:IdentityConnection"

# Set a secret
dotnet user-secrets set "ConnectionStrings:IdentityConnection" "your-connection-string"

# Remove a secret
dotnet user-secrets remove "ConnectionStrings:IdentityConnection"

# Clear all secrets for a project
dotnet user-secrets clear
```

### Security Best Practices

1. **Never commit credentials** to version control
2. **Use User Secrets for local development** - they are stored outside the repository
3. **Use environment variables or secrets managers** for production deployments
4. **The `.gitignore` file** already excludes `secrets.json` files from version control
5. **User secrets are automatically loaded** by `WebApplication.CreateBuilder()` when `UserSecretsId` is configured in the `.csproj` file

### Git Ignore Coverage

The following patterns are already ignored by `.gitignore`:
- `secrets.json` - Any secrets.json files in the project directories
- User secrets directories are automatically excluded (they're in user-specific system directories outside the repository)

## Annotation servers, image sets, and mirrors

Since 1.1.0 a `Volume` links one **Volume** (VikingXML images, one or more mirrors) to one **AnnotationServer** (one annotation SQL database). Read / Annotate / Review are granted on the annotation server and apply to every volume that shares it; Annotate or Review also imply Read. Volume grants still count (image-only volumes, rollback), and volume-name scopes such as `RC1-Internal.Annotate` keep working.

Mirrors of one image set must be exact clones; clients may load-balance across enabled mirrors (lower priority preferred). A different build of the images is a separate volume pointing at the same annotation server. `Volume.Endpoint` always holds the preferred mirror, so clients that read only `Endpoint` are unaffected.

### Configuration

| Key | Purpose |
|-----|---------|
| `VikingXmlCatalog:AllowedInternalHosts` | Host names or IPs on private networks the catalog sync may fetch VikingXML from (e.g. a VPN-only image host). Set with `VikingXmlCatalog__AllowedInternalHosts__0=10.226.68.238`. Other private and loopback hosts are refused. |
| `VikingXmlCatalog:CatalogMaxResponseBytes` | Size limit for whole-volume VikingXML fetches (default 64 MiB). |

### Upgrade order

1. Deploy IdentityServerStandalone (applies the `AddAnnotationServersAndVolumes` migration), WebApi, and WebManagement on 1.1.0. Check `GET /version` or the `X-Service-Version` header on each. Older builds cannot read annotation server rows, so finish this step before step 2.
2. WebManagement → Administration → **Volume Catalog Sync** → *Sync all volumes*. Review volumes that did not link (fetch failures, image-only volumes).
3. Same page → *Copy volume grants to annotation servers*. Review the widened-access report: anyone granted one volume now reaches the other volumes that share its annotation database.
4. A later release removes the copied Volume grant rows (image-only volumes keep theirs).

### Additional Resources

- See project-specific README files for detailed configuration:
  - `Identity.DataContext/README.md` - Database and migrations configuration
  - `Viking.Identity.Server.WebApi/README.md` - WebApi configuration and Docker setup
  - `README-Docker-All.md` - Docker deployment configuration