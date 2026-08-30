=============================================
Viking Identity Server - Complete Guide
=============================================

.. contents:: Table of Contents
   :depth: 3
   :local:

========
Overview
========

Viking Identity Server is a comprehensive authentication and authorization system built on ASP.NET Identity and IdentityServer. It provides role-based access control, organizational unit management, and fine-grained permission management for the Viking application ecosystem.

The system consists of three main components:

- **IdentityServerStandalone** (Ports 6000/6001) - Core Identity Server providing OAuth2/OIDC authentication
- **Viking.Identity.Server.WebApi** (Ports 5000/5001) - REST API for permission and identity management
- **Viking.Identity.Server.WebManagement** (Ports 4000/4001) - Web-based management interface for users, roles, and permissions

=================
Project Structure
=================

::

    Server/
    ├── Identity.DataContext/        # Entity Framework data context and migrations
    ├── Identity.Models/              # Domain models and data structures
    ├── IdentityServer/
    │   ├── Identity.Configuration/   # Configuration and policy definitions
    │   ├── Viking.Identity.Server.WebManagement/ # Web management interface
    │   ├── IdentityServerStandalone/ # Core authentication server
    │   ├── Viking.Identity.Server.Extensions/ # Custom extensions and services
    │   └── Viking.Identity.Server.WebApi/     # REST API
    └── Identity.Tests/              # Unit and integration tests

==================
Volume Permissions
==================

The system defines three levels of access for Volumes:

**Read**
    Read-only access to the volume. Users can view volume data but cannot make modifications.

**Annotate**
    Annotate/modify access to the volume. Users can create and edit annotations within the volume.

**Review**
    Full review access to the volume. Includes administrative operations such as merge/split operations
    and other dangerous operations that require elevated privileges.

=================================
Roles and Authorization Policies
=================================

System Roles
------------

**Administrator** (``Special.Roles.Admin``)
    - Full system access
    - Can manage users, roles, and permissions
    - Can access all resources regardless of explicit permissions
    - Automatically assigned to the first user who registers
    - Role ID: ``cdf2b676-7edc-4d96-9ebb-8d1968734482``
    - Role Name: "Administrator"

Authorization Policies
----------------------

The system uses policy-based authorization for fine-grained access control:

**OrgUnitAdmin** (``Policy.OrgUnitAdmin``)
    - Permission: ``Special.Permissions.OrgUnit.Admin``
    - Allows administration of Organizational Units
    - Can create, edit, and delete resources within the organizational unit
    - Can manage group memberships

**GroupAccessManager** (``Policy.GroupAccessManager``)
    - Permission: ``Special.Permissions.Group.AccessManager``
    - Allows adding and removing group members
    - Required for managing user and group assignments

Resource Permissions
--------------------

**Group Permissions:**
    - ``Access Manager``: Add/Remove group members

**OrganizationalUnit Permissions:**
    - ``Administrator``: Full administrative access to the organizational unit and its children

**Volume Permissions:**
    - ``Read``: Read-only access
    - ``Annotate``: Annotation and modification access
    - ``Review``: Full administrative access including dangerous operations

Authorization Logic
-------------------

1. **Site Administrators** (users in the Administrator role) always have full access to all resources
2. **Organizational Unit Administrators** have access to manage all resources within their organizational unit
3. **Explicit Permissions** are granted at the user or group level for specific resources
4. **Group Permissions** are inherited by all members of the group
5. **Hierarchical Access**: OrgUnit admins can manage child resources

=============
Authentication
=============

Clients
-------

The system defines several OAuth2/OIDC clients for different use cases:

**api** - API Resource Client
    - Client ID: ``api``
    - Allowed Grant Types: Client Credentials
    - Used for: Token introspection and API-to-API communication
    - Redirect URIs: ``{Authority}/signin-oidc``
    - Post Logout URIs: ``{Authority}/signout-callback-oidc``

**mvc** - Management Website Client
    - Client ID: ``mvc``
    - Client Name: "Management Website Client"
    - Allowed Grant Types: Authorization Code
    - Used for: The IdentityServer management website
    - Requires Consent: No (trusted first-party client)
    - Allows Offline Access: Yes (refresh tokens supported)
    - Redirect URIs: ``{Authority}/signin-oidc``
    - Post Logout URIs: ``{Authority}/signout-callback-oidc``

**viking** - Viking Application
    - Used by the main Viking annotation application
    - Supports dynamic volume-based scopes

**ro.viking** - Read-Only Client
    - Read-only client for annotation data
    - May support anonymous users

**sbfsem-tools** - Third-party web application (sbfsem-tools.com)
    - Client ID: ``sbfsem-tools``
    - Allowed Grant Types: Authorization Code with PKCE (required)
    - Used for: External web tool whose Python backend logs users in and reads volume permissions
    - Requires Consent: No
    - Allows Offline Access: Yes (refresh tokens)
    - Scopes: ``openid``, ``profile``, ``Viking.Annotation`` (no volume scopes; the Permissions API
      authorizes on the user, not the scope)
    - Access Token Type: Reference (validated by the WebApi through introspection)
    - Redirect URIs: ``VikingIdentityServerOptions:SbfsemToolsRedirectUris``
      (default ``https://sbfsem-tools.com/auth/callback``)
    - Post Logout URIs: ``VikingIdentityServerOptions:SbfsemToolsPostLogoutRedirectUris``
      (default ``https://sbfsem-tools.com/``)
    - Secret: its **own** value from ``SBFSEM_TOOLS_CLIENT_SECRET``, never the shared first-party
      secret. When the variable is empty the client is not served at all.
    - Integration guide for the third party: ``Documentation/source/server/Identity/sbfsem-tools.rst``

**Client Secret**
    The first-party clients share a secret loaded from ``IDENTITY_SERVER_SECRET`` (or the matching
    app setting). Third-party clients such as ``sbfsem-tools`` get a separate secret so that an
    external site never receives the first-party value.
    **Important:** Do not commit these values. Store them in environment variables, user-secrets, or Docker secrets.

Scopes
------

**Standard OpenID Connect Scopes:**
    - ``openid``: OpenID Connect authentication
    - ``profile``: User profile information

**API Scopes:**
    - ``Viking.Annotation``: Access to the Viking annotation API

**Volume Permissions (Dynamic Scopes)**
    Dynamic scopes based on volume names:
    
    - ``{VolumeName}.Read`` - Read access to the specified volume
    - ``{VolumeName}.Annotate`` - Annotate access to the specified volume  
    - ``{VolumeName}.Reviewer`` - Full reviewer access to the specified volume

Example: For a volume named "RC1", the scopes would be: ``RC1.Read``, ``RC1.Annotate``, ``RC1.Reviewer``

API Resources
-------------

**Viking.Annotation**
    - Display Name: "Viking Annotation API"
    - User Claims: Role, ID, Name
    - Requires API Secret for access
    - Primary API resource for the Viking ecosystem

==============
Database Setup
==============

Prerequisites
-------------

- .NET 9.0 SDK
- SQL Server (local or remote)
- Entity Framework Core Tools

Databases
---------

The system uses three separate databases:

**VikingIdentity**
    Stores users, roles, groups, resources, and permissions. This is the main database used by Viking to control access.

**IdentityPersistedGrants**
    Stores the tokens issued so they do not expire if the web server or application is reset.

**IdentityConfig**
    Configuration for the identity server. Currently stores client and API resource configurations.

Connection String Configuration
--------------------------------

The connection strings are configured in ``appsettings.json``:

.. code-block:: json

    {
      "ConnectionStrings": {
        "IdentityConnection": "Server=YourServer;Database=IdentityViking;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True",
        "ConfigConnection": "Server=YourServer;Database=IdentityConfig;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True",
        "PersistedGrantConnection": "Server=YourServer;Database=IdentityPersistedGrants;Trusted_Connection=False;User ID=YourUser;Password=YourPassword;MultipleActiveResultSets=true;TrustServerCertificate=True"
      }
    }

Initial Database Population
---------------------------

To initially populate the databases, follow these steps:

1. **Delete existing databases** (if they exist)::

    dotnet ef database drop --no-build --context PersistedGrantDbContext
    dotnet ef database drop --no-build --context ApplicationDBContext
    dotnet ef database drop --no-build --context ConfigurationDbContext

2. **Delete migrations** from the project for databases to be recreated

3. **Create initial migrations**::

    dotnet ef migrations add InitialIdentityServerPersistedGrantDbMigration -c PersistedGrantDbContext -o Data/Migrations/IdentityServer/PersistedGrantDb -v --no-build
    dotnet ef migrations add InitialApplicationDatabaseMigration -c ApplicationDbContext -o Data/Migrations/Application -v --no-build
    dotnet ef migrations add InitialIdentityServerConfigurationDbMigration -c ConfigurationDbContext -o Data/Migrations/IdentityServer/ConfigurationDb --no-build

4. **Recompile the code**

5. **Populate the databases**::

    dotnet ef database update --no-build -v --context PersistedGrantDbContext
    dotnet ef database update --no-build -v --context ApplicationDbContext
    dotnet ef database update --no-build -v --context ConfigurationDbContext

Entity Framework Migrations
----------------------------

**Creating Migrations**::

    # Navigate to the Identity.DataContext project directory
    cd Identity.DataContext
    
    # Create a new migration
    dotnet ef migrations add YourMigrationName

**Applying Migrations**::

    # Apply all pending migrations
    dotnet ef database update
    
    # Apply to a specific migration
    dotnet ef database update YourMigrationName

**Removing Migrations**::

    # Remove the last migration (if not applied)
    dotnet ef migrations remove

**Rolling Back Migrations**::

    dotnet ef database update InitialApplicationDatabaseMigration -c ApplicationDbContext -v --no-build

Administrator Role Assignment
-----------------------------

**Important:** The first user created in the system automatically becomes the administrator.

1. The system creates an "Administrator" role during database initialization
2. When the first user registers, they are automatically assigned the Administrator role
3. This ensures there's always at least one administrator in the system
4. Subsequent users need to be manually assigned roles by an administrator

Default Data Seeded
--------------------

- **Administrator Role**: Created with a fixed GUID
- **Resource Types**: Resource, OrganizationalUnit, Group, Volume
- **Volume Permissions**: Read, Annotate, Reviewer
- **Everyone Group**: A default group that all users can be assigned to

====================
Deployment to IIS
====================

At the Marc lab, this site runs under the OpR-Marc-VikingID user.

IIS Setup
---------

To deploy to IIS on a new server:

1. Install Web Deploy 3.6 via IIS Manager's Web Platform Installer
2. Configure Web Deploy via "Deploy -> Configure Web Deploy" context menu option in IIS Manager
3. Within Visual Studio, select "publish" from the project's context menu and specify the IIS Server

SSL Certificate Configuration
------------------------------

**Initial Setup**

Install the SSL certificate on the server. When installing:

- The certificate can be placed in your personal store or machine store
- Ensure the IIS application pool identity has access to the private SSL key

To grant access to the private SSL key:

1. Open Certificate Manager (certlm) to browse certificates on the machine
2. Right-click the certificate, select "All Tasks -> Manage Private Keys"
3. Note: In recent Windows versions, the certificate must be in the personal store to edit permissions
4. If needed, drag it to personal store, edit permissions, then drag back to machine store

**IIS Configuration**

- Ensure the IIS website has an SSL binding pointing to the correct certificate
- Disable HTTP access to prevent unencrypted communication
- Update ``appsettings.json`` to refer to the correct SSL key serial number under ``SSL -> SerialNumber``
- Verify the IIS application pool identity has access to the certificate's private key

**SSL Certificate Turnover**

IT requires annual SSL certificate replacement:

1. Replace bindings on the Default Web Site to point to the new SSL certificate
2. Update ``appsettings.json`` with the new certificate's serial number
3. Fix web deployment SSL: Run ``netsh http delete sslcert ipport=0.0.0.0:8172`` as administrator
4. In IIS Manager, open "Management Service" and update the web deploy certificate

=================
API Endpoints
=================

The system provides the following custom endpoints:

**ResourceTypes/List**
    Returns all resource types including available permissions

**Resources/UserPermissions/{id}?user={username}**
    Returns all permissions the specified username has on the given resource

**permissions/CurrentUser**
    Returns the username of the currently authenticated user

**permissions/CurrentUserId**
    Returns the user ID of the currently authenticated user

**permissions/{userId}/resource/{resourceName}**
    Returns permissions for a specific user on a specific resource

**permissions/resource/{resourceName}**
    Returns permissions for the currently authenticated user on a specific resource

=================
Docker Deployment
=================

Quick Start - All Services
---------------------------

To run all three Identity Server components in a single container:

**Option 1: Using PowerShell Script (Recommended)**::

    # Start all services in foreground
    .\start-all-services.ps1
    
    # Start all services in background
    .\start-all-services.ps1 -Detach
    
    # Start without rebuilding
    .\start-all-services.ps1 -Build:$false

**Option 2: Using Docker Compose**::

    # Build and start all services
    docker-compose -f docker-compose-all.yml up --build
    
    # Start in background
    docker-compose -f docker-compose-all.yml up -d
    
    # View logs
    docker-compose -f docker-compose-all.yml logs -f
    
    # Stop services
    docker-compose -f docker-compose-all.yml down

Docker Compose All-in-One Configuration
----------------------------------------

The ``docker-compose-all.yml`` file provides a complete all-in-one deployment configuration that runs all three services in a single container using Supervisor for process management.

**Complete docker-compose-all.yml Configuration:**

.. code-block:: yaml

    services:
      identity-all-services:
        env_file:
          - .env.All
          - .env.All.Docker
        build:
          context: ..
          dockerfile: IdentityServer\Dockerfile
          args:
            IDENTITY_STANDALONE_HTTP_PORT: ${IDENTITY_STANDALONE_HTTP_PORT:-5000}
            IDENTITY_STANDALONE_HTTPS_PORT: ${IDENTITY_STANDALONE_HTTPS_PORT:-5001}
            IDENTITY_WEBAPI_HTTP_PORT: ${IDENTITY_WEBAPI_HTTP_PORT:-6000}
            IDENTITY_WEBAPI_HTTPS_PORT: ${IDENTITY_WEBAPI_HTTPS_PORT:-6001}
            IDENTITY_MANAGEMENT_HTTP_PORT: ${IDENTITY_MANAGEMENT_HTTP_PORT:-4000}
            IDENTITY_MANAGEMENT_HTTPS_PORT: ${IDENTITY_MANAGEMENT_HTTPS_PORT:-4001}
        ports:
          # IdentityServerStandalone ports
          - "${IDENTITY_STANDALONE_HTTP_PORT:-5000}:${IDENTITY_STANDALONE_CONTAINER_HTTP_PORT:-5000}"
          - "${IDENTITY_STANDALONE_HTTPS_PORT:-5001}:${IDENTITY_STANDALONE_CONTAINER_HTTPS_PORT:-5001}"
          # WebApi ports  
          - "${IDENTITY_WEBAPI_HTTP_PORT:-6000}:${IDENTITY_WEBAPI_CONTAINER_HTTP_PORT:-6000}"
          - "${IDENTITY_WEBAPI_HTTPS_PORT:-6001}:${IDENTITY_WEBAPI_CONTAINER_HTTPS_PORT:-6001}"
          # IdentityServer (Management) ports
          - "${IDENTITY_MANAGEMENT_HTTP_PORT:-4000}:${IDENTITY_MANAGEMENT_CONTAINER_HTTP_PORT:-4000}"
          - "${IDENTITY_MANAGEMENT_HTTPS_PORT:-4001}:${IDENTITY_MANAGEMENT_CONTAINER_HTTPS_PORT:-4001}"
        environment:
          - ASPNETCORE_ENVIRONMENT=Docker
          - ASPNETCORE_URLS=https://+:4001;https://+:5001;https://+:6001;http://+:4000;http://+:5000;http://+:6000;
          # Database connections
          - ConnectionStrings__IdentityConnection=Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=${SQL_SERVER_IDENTITY_DB};...
          - ConnectionStrings__ConfigConnection=Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=${SQL_SERVER_CONFIG_DB};...
          - ConnectionStrings__PersistedGrantConnection=Server=${SQL_SERVER_HOST},${SQL_SERVER_PORT};Database=${SQL_SERVER_GRANTS_DB};...
        volumes:
          # Configuration files for each service
          - ./IdentityServerStandalone/appsettings.json:/app/identity-standalone/appsettings.json:ro
          - ./IdentityServerStandalone/appsettings.Docker.json:/app/identity-standalone/appsettings.Docker.json:ro
          - ./Viking.Identity.Server.WebApi/appsettings.json:/app/identity-webapi/appsettings.json:ro
          - ./Viking.Identity.Server.WebApi/appsettings.Docker.json:/app/identity-webapi/appsettings.Docker.json:ro
          - ./IdentityServer/appsettings.json:/app/identity-server/appsettings.json:ro
          - ./IdentityServer/appsettings.Docker.json:/app/identity-server/appsettings.Docker.json:ro
          # Environment files
          - ./.env.All:/app/.env.All:ro
          - ./.env.All.Docker:/app/.env.All.Docker:ro
          # Log files (host to container)
          - ./logs/identity-standalone:/var/log/supervisor/identity-standalone
          - ./logs/identity-webapi:/var/log/supervisor/identity-webapi
          - ./logs/identity-server:/var/log/supervisor/identity-server
          # Data Protection Keys (shared across services)
          - ./DataProtectionKeys:/app/DataProtectionKeys:rw
          # Duende License (if available)
          - ${DUENDE_KEY_PATH:-/dev/null}:/app/Duende_License.key:ro
        secrets:
          - ssl_cert
          - ssl_key
        networks:
          - identity-network
        healthcheck:
          test: ["CMD", "/app/health-check.sh"]
          interval: 30s
          timeout: 10s
          retries: 3
          start_period: 60s

    secrets:
      ssl_cert:
        file: ${SSL_CERT_PATH}
      ssl_key:
        file: ${SSL_KEY_PATH}
      duende_key:
        file: ${DUENDE_KEY_PATH}

    networks:
      identity-network:
        driver: bridge

**Key Features of the All-in-One Deployment:**

- **Supervisor Process Management**: Uses Supervisor to manage all three .NET applications in a single container
- **Shared Data Protection Keys**: All services share the same Data Protection Keys for consistent authentication
- **Color-Coded Logging**: Each service logs with color codes for easy identification ([IS] = Standalone, [API] = WebApi, [MGMT] = Management)
- **Health Checks**: Built-in health check script verifies all three services are running
- **Volume Mounts**: Separate configuration files and logs for each service
- **Docker Secrets**: Secure management of SSL certificates and sensitive data

Service Endpoints
-----------------

Once running:

**IdentityServerStandalone** (Authentication Server)
    - HTTP: http://localhost:6000
    - HTTPS: https://localhost:6001
    - Discovery: https://localhost:6001/.well-known/openid-configuration

**Viking.Identity.Server.WebApi** (REST API)
    - HTTP: http://localhost:5000
    - HTTPS: https://localhost:5001
    - Swagger: https://localhost:5001/swagger (Development only)
    - Health: https://localhost:5001/health

**IdentityServer** (Management Website)
    - HTTP: http://localhost:4000
    - HTTPS: https://localhost:4001

Building Individual Services
-----------------------------

**IdentityServerStandalone**

Build from the **Server folder** (parent of IdentityServer)::

    docker build -f IdentityServer/IdentityServerStandalone/Dockerfile -t identityserver-standalone .

Run::

    docker run -d --name identityserver-standalone -p 6000:6000 -p 6001:6001 identityserver-standalone:latest

**Viking.Identity.Server.WebApi**

Build from the **Server folder**::

    docker build -f IdentityServer/Viking.Identity.Server.WebApi/Dockerfile -t identity-webapi .

Run::

    docker run -d --name identity-webapi -p 5000:5000 -p 5001:5001 identity-webapi:latest

Environment Variables and .env Files
------------------------------------

The system uses environment files to configure Docker deployments. Create these files from the provided example:

**Create .env.All file (copy from env.example):**

.. code-block:: bash

    # Database Configuration
    SQL_SERVER_HOST=localhost
    SQL_SERVER_PORT=1433
    SQL_SERVER_USER=sa
    SQL_SERVER_PASSWORD=YourPassword123!
    SQL_SERVER_IDENTITY_DB=IdentityViking
    SQL_SERVER_CONFIG_DB=IdentityConfig
    SQL_SERVER_GRANTS_DB=IdentityPersistedGrants

    # SSL Certificate Configuration
    SSL_CERT_PATH=/path/to/certificate.crt
    SSL_KEY_PATH=/path/to/private.key

    # Authority URL (IdentityServerStandalone endpoint)
    AUTHORITY=https://your-domain.com:6001/

    # Duende IdentityServer License (optional)
    DUENDE_KEY_PATH=/path/to/Duende_License.key

**Port Configuration (default values shown):**

.. code-block:: bash

    # IdentityServerStandalone (Core Authentication)
    IDENTITY_STANDALONE_HTTP_PORT=5000
    IDENTITY_STANDALONE_HTTPS_PORT=5001
    IDENTITY_STANDALONE_CONTAINER_HTTP_PORT=5000
    IDENTITY_STANDALONE_CONTAINER_HTTPS_PORT=5001

    # Viking.Identity.Server.WebApi (REST API)
    IDENTITY_WEBAPI_HTTP_PORT=6000
    IDENTITY_WEBAPI_HTTPS_PORT=6001
    IDENTITY_WEBAPI_CONTAINER_HTTP_PORT=6000
    IDENTITY_WEBAPI_CONTAINER_HTTPS_PORT=6001

    # IdentityServer (Management Website)
    IDENTITY_MANAGEMENT_HTTP_PORT=4000
    IDENTITY_MANAGEMENT_HTTPS_PORT=4001
    IDENTITY_MANAGEMENT_CONTAINER_HTTP_PORT=4000
    IDENTITY_MANAGEMENT_CONTAINER_HTTPS_PORT=4001

**Create .env.All.Docker file (Docker-specific overrides):**

This file contains Docker-specific settings that override the base .env.All file when running in Docker.

.. code-block:: bash

    # Docker-specific database host (use service name from docker-compose)
    SQL_SERVER_HOST=sqlserver
    
    # Docker internal URLs
    AUTHORITY=https://identity-standalone:5001/

**Setup Steps:**

1. Copy ``env.example`` to ``.env.All``
2. Update ``.env.All`` with your actual values
3. Create ``.env.All.Docker`` for Docker-specific overrides
4. Never commit these files to version control (they contain secrets)

Individual Service Docker Compose
----------------------------------

For running services separately, use ``docker-compose.yml`` which defines individual containers:

.. code-block:: yaml

    services:
      identity-standalone:
        env_file:
          - .env
          - .env.Docker
        build:
          context: ..
          dockerfile: IdentityServer/IdentityServerStandalone/Dockerfile
        ports:
          - "${IDENTITY_STANDALONE_HTTP_PORT}:80"
          - "${IDENTITY_STANDALONE_HTTPS_PORT}:443"
        image: identity-standalone
        secrets:
          - ssl_cert
          - ssl_key
        environment:
          - ASPNETCORE_URLS=http://+:80;https://+:443
          - ASPNETCORE_ENVIRONMENT=Docker
        volumes:
          - ./IdentityServerStandalone/appsettings.json:/app/appsettings.json:ro
          - ./IdentityServerStandalone/appsettings.Docker.json:/app/appsettings.Docker.json:ro
        networks:
          - identity-network

      identity-webapi:
        env_file:
          - .env
          - .env.docker    
        build:
          context: ..
          dockerfile: IdentityServer/Viking.Identity.Server.WebApi/Dockerfile
        ports:
          - "${IDENTITY_WEBAPI_HTTP_PORT}:80"
          - "${IDENTITY_WEBAPI_HTTPS_PORT}:443"
        depends_on:
          - identity-standalone
        image: identity-webapi
        environment:
          - ASPNETCORE_URLS=http://+:80;https://+:443
          - ASPNETCORE_ENVIRONMENT=Docker
        volumes:
          - ./Viking.Identity.Server.WebApi/appsettings.json:/app/appsettings.json:ro
          - ./Viking.Identity.Server.WebApi/appsettings.Docker.json:/app/appsettings.Docker.json:ro
        networks:
          - identity-network

**Usage:**

.. code-block:: bash

    # Start individual services
    docker-compose up -d identity-standalone
    docker-compose up -d identity-webapi
    
    # Or start both
    docker-compose up -d

Docker Compose Example
----------------------

Complete setup with custom configuration:

.. code-block:: yaml

    version: '3.8'
    services:
      identity-standalone:
        build:
          context: .
          dockerfile: IdentityServer/IdentityServerStandalone/Dockerfile
        ports:
          - "6000:6000"
          - "6001:6001"
        environment:
          - ASPNETCORE_ENVIRONMENT=Production
          - ConnectionStrings__IdentityConnection=Server=your-db;Database=IdentityViking;User ID=user;Password=pass;MultipleActiveResultSets=true;TrustServerCertificate=True
        volumes:
          - ./custom-appsettings.json:/app/appsettings.Production.json
        networks:
          - identity-network
      
      identity-webapi:
        build:
          context: .
          dockerfile: IdentityServer/Viking.Identity.Server.WebApi/Dockerfile
        ports:
          - "5000:5000"
          - "5001:5001"
        environment:
          - ASPNETCORE_ENVIRONMENT=Production
          - ConnectionStrings__IdentityConnection=Server=your-db;Database=IdentityViking;User ID=user;Password=pass;MultipleActiveResultSets=true;TrustServerCertificate=True
          - JwtBearerOptions__Authority=https://localhost:6001/
        volumes:
          - ./custom-appsettings-webapi.json:/app/appsettings.Production.json
        networks:
          - identity-network
        depends_on:
          - identity-standalone
    
    networks:
      identity-network:
        driver: bridge

Volume Mounting for Configuration
----------------------------------

**Important:** Docker can only mount single files if they already exist on the host.

**Option 1: Mount existing files**::

    docker run -d \
      --name identityserver-standalone \
      -p 6000:6000 -p 6001:6001 \
      -v /path/to/appsettings.json:/app/appsettings.json \
      -v /path/to/appsettings.Production.json:/app/appsettings.Production.json \
      identityserver-standalone:latest

**Option 2: Mount configuration directory (recommended)**::

    docker run -d \
      --name identityserver-standalone \
      -p 6000:6000 -p 6001:6001 \
      -v /path/to/config:/app/config \
      identityserver-standalone:latest

====================
Configuration Details
====================

Key Configuration Sections
--------------------------

The application uses these main configuration sections:

**ConnectionStrings**
    Database connections for Identity, Config, and PersistedGrants databases

**VikingIdentityServerOptions**
    IdentityServer configuration including:
    
    - ``Authority``: The base URL of the IdentityServerStandalone service
    - ``Secret``: Shared secret for client authentication (change in production!)
    - ``ApiScopes``: Array of API scopes with names and descriptions

**JwtBearerOptions** (WebApi only)
    JWT Bearer token authentication configuration:
    
    - ``Authority``: URL of the IdentityServerStandalone service
    - ``RequireHttpsMetadata``: Whether to require HTTPS (true for production)
    - ``TokenValidationParameters``: Token validation settings including audience, issuer, and lifetime validation

**OAuth2IntrospectionOptions** (Management Website only)
    OAuth2 token introspection configuration:
    
    - ``ClientId``: Client identifier (mvc)
    - ``ClientSecret``: Client secret for authentication
    - ``Authority``: URL of the IdentityServerStandalone service
    - ``EnableCaching``: Cache introspection results for performance

**SSL**
    Certificate configuration:
    
    - ``SerialNumber``: SSL certificate serial number (IIS deployments)
    - ``DnsName``: DNS name for the certificate
    - ``CertificatePath``: Path to certificate file (optional, for Docker)

Email Configuration
-------------------

All services use a unified ``Email`` configuration section with modern, clean property names. This harmonized approach eliminates duplication and follows .NET configuration best practices.

**Unified Email Configuration:**

.. code-block:: json

    {
      "Email": {
        "Server": "smtp.your-domain.com",
        "Port": 587,
        "Username": "your-username",
        "Password": "your-password",
        "FromEmail": "noreply@your-domain.com",
        "FromName": "Viking Identity Server",
        "EnableSsl": true,
        "UseHtml": true,
        "Timeout": 10,
        "EnableSending": false
      }
    }

**Configuration Properties:**

- ``Server``: SMTP server hostname (e.g., smtp.gmail.com, smtp.office365.com)
- ``Port``: SMTP port number (typically 587 for TLS, 25 for non-TLS, 465 for SSL)
- ``Username``: SMTP authentication username (optional, leave empty for anonymous)
- ``Password``: SMTP authentication password (optional)
- ``FromEmail``: Email address shown as sender
- ``FromName``: Display name shown as sender
- ``EnableSsl``: Enable SSL/TLS encryption (recommended: true)
- ``UseHtml``: Send HTML formatted emails (true) or plain text (false)
- ``Timeout``: Connection timeout in seconds (default: 10)
- ``EnableSending``: Master switch to enable/disable email sending

**Email Features:**

- User registration notifications to administrators
- Email confirmation for new users
- Claim request notifications to OrgUnit administrators
- Password reset emails and codes

**Development vs. Production:**

- **Development**: Set ``EnableSending`` to ``false`` to log emails without sending
- **Production**: Set ``EnableSending`` to ``true`` and configure valid SMTP credentials

**Common SMTP Server Examples:**

.. code-block:: json

    // Gmail
    {
      "Server": "smtp.gmail.com",
      "Port": 587,
      "EnableSsl": true
    }

    // Office 365
    {
      "Server": "smtp.office365.com",
      "Port": 587,
      "EnableSsl": true
    }

    // SendGrid
    {
      "Server": "smtp.sendgrid.net",
      "Port": 587,
      "Username": "apikey",
      "Password": "your-sendgrid-api-key",
      "EnableSsl": true
    }

=========
Debugging
=========

Development Environment
-----------------------

The ``appsettings.Development.json`` file can be edited to apply debug-only settings. Use cases:

- Create a debug version of the IdentityViking database with a different connection string
- Develop against the debug database and only migrate production after testing

Remote Debugging
----------------

1. Install Visual Studio remote debugging package on the server
2. Run the remote debugger as administrator when debugging
3. Attach to the IIS worker process or the running application

Docker Debugging
----------------

To run Docker containers in debug mode with verbose logging:

**Set environment to Development**::

    export ASPNETCORE_ENVIRONMENT=Development
    docker-compose -f docker-compose-all.yml up

**Access container shell**::

    # Get container ID
    docker ps
    
    # Access shell
    docker exec -it <container-id> /bin/bash

View Logs::

    # View all service logs
    docker-compose -f docker-compose-all.yml logs -f
    
    # View specific service logs
    docker-compose -f docker-compose-all.yml logs -f identity-standalone

===============
Troubleshooting
===============

Common Issues
-------------

**Database Connection Errors**
    - Verify connection strings in ``appsettings.json``
    - Ensure SQL Server is running and accessible
    - Check firewall rules for database access
    - Verify user credentials and database permissions

**Migration Conflicts**
    - Consider resetting migration history for development databases
    - Always backup production databases before major changes
    - Use ``dotnet ef migrations list`` to check migration status

**SSL Certificate Issues**
    - Verify certificate is installed in correct store
    - Check that application pool identity has private key access
    - Ensure ``appsettings.json`` has correct certificate serial number
    - Verify IIS binding points to correct certificate

**Port Conflicts (Docker)**
    - Ensure ports 4000, 4001, 5000, 5001, 6000, 6001 are available
    - Use ``netstat -ano | findstr <port>`` to check port usage
    - Modify docker-compose.yml to use different ports if needed

**Authentication Failures**
    - Verify IdentityServerStandalone is running and accessible
    - Check Authority URL in WebApi configuration matches IdentityServerStandalone URL
    - Ensure both services can communicate (same Docker network or accessible URLs)
    - Verify client secrets match between client and server configurations

**Identity Server signing key / Data Protection "key not found in key ring" (Docker)**
    - This occurs when the Data Protection key ring used to protect Identity Server signing keys is not available (e.g. the ``DataProtectionKeys`` volume was empty or from another environment). The all-in-one image now seeds the volume from the image when it is empty on startup.
    - If the error persists, the persisted signing key was created with a different key ring. Delete that key so Identity Server can create a new one: in the **persisted-grant database** (connection ``PersistedGrantConnection`` / ``SQL_SERVER_GRANTS_DB``), run: ``DELETE FROM Keys WHERE Id = '<kid from the log>';`` (e.g. ``Id = '789096348DD6F6B9228A5B84437D5A73'``). Then restart the container.

Useful Commands
---------------

**Entity Framework**::

    # Check migration status
    dotnet ef migrations list
    
    # Generate SQL script for migrations
    dotnet ef migrations script
    
    # Generate SQL between specific migrations
    dotnet ef migrations script FromMigration ToMigration
    
    # Check database connection (dry run)
    dotnet ef database update --dry-run

**Docker**::

    # View running containers
    docker ps
    
    # View all containers (including stopped)
    docker ps -a
    
    # Stop all identity containers
    docker stop $(docker ps -q --filter "name=identity")
    
    # Remove all identity containers
    docker rm $(docker ps -aq --filter "name=identity")
    
    # View container logs
    docker logs <container-name>
    
    # Follow container logs in real-time
    docker logs -f <container-name>

=======================
Security Considerations
=======================

SSL/TLS
-------

- Always use proper SSL certificates in production
- Never use development certificates in production environments
- Regularly update certificates before expiration
- Ensure private keys are secured with appropriate permissions

Database Security
-----------------

- Use strong passwords for database connections
- Never commit connection strings with credentials to version control
- Use environment variables or user secrets for production credentials
- Regularly backup databases
- Test migrations on copies of production data

Secrets Management
------------------

- Use ASP.NET Core User Secrets for development
- Use environment variables or secure vaults (Azure Key Vault, etc.) for production
- Never hardcode secrets in source code or configuration files
- Rotate secrets regularly

Docker Security
---------------

- Use Docker secrets for sensitive configuration in production
- Keep base images updated
- Scan images for vulnerabilities
- Use network isolation between services
- Limit container privileges

============
Contributing
============

We welcome contributions to the Viking Identity Server project!

General Feedback and Discussions
---------------------------------

Please start a discussion on the main repo issue tracker.

Filing Issues
-------------

Before filing a bug:

1. Read the documentation thoroughly
2. Include a code snippet demonstrating the issue
3. Provide exact steps to reproduce the problem
4. Include version numbers and environment details

Contributing Code
-----------------

Requirements:

1. Sign the Contributor License Agreement
2. Ensure code builds without errors
3. Familiarize yourself with the project workflow and coding conventions
4. Submit pull requests to the dev branch only

**Code Contribution Guidelines:**

- Provide tests for bugs and features
- Follow existing code style and conventions
- Write clear commit messages
- Reference issue numbers in commits

**Commit Format**::

    Summary of changes (Less than 80 chars)
     - Detail 1
     - Detail 2
    
    #issuenumber

**Testing:**

- Tests required for every bug/feature
- Tests only needed for issues requiring QA verification
- Discuss with team if scenario is too complex to test

=======
License
=======

Copyright © Brock Allen & Dominick Baier. All rights reserved.
Licensed under the Apache License, Version 2.0.

===============
Version History
===============

**Current Version**
    - Renamed Volume "Admin" permission to "Reviewer"
    - Updated to .NET 9.0
    - Added comprehensive documentation
    - Improved Docker support
    - Enhanced security features

======
Support
======

For questions, issues, or support:

1. Check this documentation first
2. Review existing issues on the repository
3. Contact system administrators for Viking-specific questions
4. For IdentityServer core issues, consult the IdentityServer documentation

==================
Additional Notes
==================

This documentation consolidates information from multiple README files and deployment guides.
It reflects the current state of the Viking Identity Server system as of October 2025.

For the most up-to-date information about specific components, refer to:

- IdentityServer official documentation: https://identityserver.io
- ASP.NET Identity documentation: https://docs.microsoft.com/aspnet/identity
- Entity Framework Core documentation: https://docs.microsoft.com/ef/core

