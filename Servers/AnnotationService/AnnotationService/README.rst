=====================
Annotation Service
=====================

Overview
========

The Viking Annotation Service is a WCF (Windows Communication Foundation) web service that provides a comprehensive API for managing connectome annotations. It handles the creation, modification, and retrieval of neuronal structures, locations, and their relationships in volumetric datasets.

Features
========

Core Functionality
------------------

* **Structure Management**: Create, update, delete, and query neuronal structures
* **Location Management**: Manage spatial locations and location links within structures
* **Structure Type Management**: Define and manage types of neuronal structures
* **Graph Operations**: Query connectivity and relationships between structures
* **Change Tracking**: Full audit trail with change logs for all modifications
* **Spatial Queries**: Bounding box and region-based queries with SQL Server spatial types

Authentication & Authorization
-------------------------------

* **JWT Token Authentication**: Integrated with IdentityServer for modern OAuth2/OpenID Connect authentication
* **Role-Based Access Control**: Fine-grained permissions with the following roles:
  
  - ``Read``: View-only access to annotations
  - ``Annotate``: Create and modify annotations
  - ``Write``: Extended write permissions
  - ``Modify``: (Deprecated) Equivalent to Write role
  - ``Review``: Full administrative access including merge, split, and structure type management

* **Volume-Specific Permissions**: Roles can be scoped to specific volumes (e.g., ``TemporalMonkey.Annotate``)
* **Legacy Authentication**: Backwards compatible with ASP.NET Membership authentication

Technical Details
=================

Technology Stack
----------------

* **.NET Framework**: 4.8
* **WCF**: Windows Communication Foundation for service hosting
* **Entity Framework**: 6.5.1 for data access
* **SQL Server**: Spatial data types for geometric operations
* **Protocol Buffers**: Efficient binary serialization via protobuf-net
* **JWT**: Modern token-based authentication with Microsoft.IdentityModel

Dependencies
------------

Key NuGet Packages:

* ``Microsoft.IdentityModel.Tokens`` (8.14.0) - JWT token validation
* ``Microsoft.IdentityModel.Protocols.OpenIdConnect`` (8.14.0) - OIDC integration
* ``EntityFramework`` (6.5.1) - Database access
* ``protobuf-net`` (3.2.56) - Binary serialization
* ``Microsoft.SqlServer.Types`` (160.1000.6) - Spatial types
* ``Duende.IdentityModel`` (7.1.0) - IdentityServer client

Service Interfaces
==================

IAnnotateStructures
-------------------

Manages neuronal structures including creation, updates, merging, splitting, and queries.

**Key Operations**:

* ``CreateStructure()`` - Create new structures
* ``UpdateStructures()`` - Batch update operations
* ``GetStructureByID()`` - Retrieve structures by ID
* ``GetStructuresForSection()`` - Query structures by section
* ``Merge()`` - Merge two structures
* ``Split()`` - Split a structure at a location

IAnnotateLocations
------------------

Manages spatial locations within structures.

**Key Operations**:

* ``CreateLocation()`` - Add new locations
* ``UpdateLocations()`` - Batch update locations
* ``GetLocationByID()`` - Retrieve location details
* ``GetLocationsForStructure()`` - Get all locations in a structure
* ``GetLocationsForSection()`` - Query locations by section

IAnnotateStructureTypes
-----------------------

Manages structure type definitions.

**Key Operations**:

* ``CreateStructureType()`` - Define new structure types
* ``UpdateStructureTypes()`` - Modify type definitions
* ``GetStructureTypes()`` - Retrieve all types
* ``GetStructureTypeByID()`` - Get specific type

IAnnotatePermittedStructureLinks
---------------------------------

Manages permitted connectivity patterns between structure types.

**Key Operations**:

* ``GetPermittedStructureLinks()`` - Query allowed connections
* ``CreatePermittedStructureLink()`` - Define new connection rules
* ``UpdatePermittedStructureLinks()`` - Modify connection rules

IVolumeMeta
-----------

Provides volume metadata and scale information.

Installation & Deployment
==========================

Prerequisites
-------------

1. **Windows Server** with IIS (Internet Information Services)
2. **SQL Server** with spatial types support
3. **SSL Certificate** for HTTPS
4. **.NET Framework 4.8** runtime
5. **SQL Server Types** (included in package)

IIS Configuration
-----------------

1. Install the Microsoft.AspNet.WebApi package::

    nuget install Microsoft.AspNet.WebApi

2. Create SSL binding on the website
3. Assign a valid SSL certificate
4. Enable Web Forms Authentication
5. Enable .NET Roles
6. Create folder structure for each volume:

   * Annotation (this service)
   * OData
   * Export

Web.config Configuration
-------------------------

Authentication Settings
~~~~~~~~~~~~~~~~~~~~~~~

The service uses ``UserNameOverTransport`` security with custom JWT validation::

    <serviceCredentials>
        <userNameAuthentication 
            userNamePasswordValidationMode="Custom" 
            customUserNamePasswordValidatorType="Annotation.Identity.IdentityValidator,AnnotationService" />
        <serviceCertificate 
            findValue="your-cert-name" 
            storeLocation="LocalMachine" 
            x509FindType="FindBySubjectName"/>
    </serviceCredentials>

Authorization Settings
~~~~~~~~~~~~~~~~~~~~~~

Custom authorization with JWT-based roles::

    <serviceAuthorization 
        principalPermissionMode="Custom" 
        serviceAuthorizationManagerType="Annotation.Identity.RoleAuthorizationManager, AnnotationService"/>

Identity Server Configuration
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Configure the IdentityServer endpoint in ``<appSettings>``::

    <appSettings>
        <add key="IdentityServer:authority" value="https://identity.example.com/" />
        <add key="IdentityServer:audience" value="Viking.Annotation.API" />
        <add key="VolumeName" value="YourVolumeName" />
        <add key="DatabaseName" value="YourDatabaseName" />
    </appSettings>

Connection Strings
~~~~~~~~~~~~~~~~~~

Configure Entity Framework connection::

    <connectionStrings>
        <add name="ConnectomeEntities" 
             connectionString="metadata=res://*/;provider=System.Data.SqlClient;
                              provider connection string=&quot;
                              data source=YOUR-SERVER;
                              initial catalog=YOUR-DATABASE;
                              integrated security=True;
                              multipleactiveresultsets=True;
                              Type System Version=SQL Server 2012;
                              Connection Timeout=300;
                              Command Timeout=300&quot;" 
             providerName="System.Data.EntityClient" />
    </connectionStrings>

Deployment Scripts
------------------

Use the provided deployment scripts:

**Production Deployment**::

    DeployProduction.cmd [target-path]

**Debug Deployment**::

    DeployDebug.cmd [target-path]

These scripts copy the compiled binaries and configuration to the target IIS directory.

Docker Deployment
=================

While this is a legacy WCF service, the related gRPC services can be deployed via Docker. See ``Docker-Configuration-README.md`` in the repository root for details.

Architecture
============

Authentication Pipeline
-----------------------

1. **Transport Authentication**: Username/password via WCF ``UserNameOverTransport``
2. **Lenient Validation**: Initial validation allows request to proceed (``IdentityValidator``)
3. **JWT Extraction**: JWT token extracted from HTTP headers (``JwtMessageInspector``)
4. **Token Validation**: Token validated against IdentityServer's signing keys
5. **Claims Transformation**: JWT claims converted to WCF-compatible roles
6. **Authorization**: Role-based checks via ``RoleAuthorizationManager``
7. **Permission Enforcement**: ``[PrincipalPermission]`` attributes enforce access control

JWT Token Processing
--------------------

The ``JwtMessageInspector`` handles:

* Token extraction from ``Authorization: Bearer`` header
* Token validation against IdentityServer discovery endpoint
* Automatic signing key rotation
* Claims extraction and role mapping
* Volume-specific role parsing (e.g., ``VolumeName.RoleName``)
* Thread principal and operation context setup

Role Mapping
~~~~~~~~~~~~

* Volume-specific ``admin`` scope → ``Review`` role (e.g., ``TemporalMonkey.admin`` → ``Review``)
* Volume-specific scopes (e.g., ``TemporalMonkey.Read``) → individual roles
* Global ``Administrator`` role preserved as-is (separate from annotation service roles)
* Standard JWT roles preserved and validated

Data Model
----------

The service uses Entity Framework to interact with the following key entities:

* **Structure**: Neuronal structures with type, parent relationships, and attributes
* **Location**: Spatial points with X, Y, Z coordinates and radii
* **LocationLink**: Connections between locations within structures
* **StructureLink**: Connections between different structures
* **StructureType**: Type definitions with color and naming
* **PermittedStructureLink**: Rules for valid structure connections

Performance Considerations
--------------------------

* **Database Timeouts**: Configurable command timeout (default: 300 seconds)
* **Batch Operations**: Support for bulk create/update operations
* **Spatial Indexing**: SQL Server spatial indexes for efficient region queries
* **Binary Protocol**: protobuf-net reduces payload size vs XML
* **Concurrent Access**: ``ConcurrencyMode.Multiple`` for parallel request handling

Security
========

Transport Security
------------------

* **HTTPS Required**: All communication over TLS/SSL
* **Certificate Validation**: Server certificate must be valid and trusted
* **Strong Encryption**: Modern cipher suites required

Authentication Security
-----------------------

* **JWT Token Validation**: Full validation of issuer, audience, lifetime, and signature
* **Automatic Key Rotation**: Signing keys retrieved from OIDC discovery endpoint
* **Clock Skew Tolerance**: 5-minute tolerance for time synchronization issues
* **Token Expiration**: Enforced token lifetime validation

Authorization Security
----------------------

* **Declarative Security**: ``[PrincipalPermission]`` attributes on all operations
* **Audit Logging**: Security audit events logged to Windows Event Log
* **Change Tracking**: All modifications logged with user identity

Troubleshooting
===============

Common Issues
-------------

JWT Authentication Failures
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

**Symptoms**: ``SecurityException: Request for principal permission failed``

**Solutions**:

1. Verify IdentityServer is accessible from the service
2. Check that ``IdentityServer:authority`` matches the token issuer
3. Ensure ``IdentityServer:audience`` matches the token audience
4. Verify the user has the required roles in their JWT token
5. Check that volume-specific scopes are correctly formatted (``VolumeName.RoleName``)

Missing Roles
~~~~~~~~~~~~~

**Symptoms**: User authenticated but cannot access operations

**Solutions**:

1. Verify JWT token contains the required role claims
2. Check volume name matches between config and token scopes
3. Ensure role names match expected values (Read, Write, Annotate, Review)
4. For volume-specific admin access, ensure token has ``VolumeName.admin`` scope (maps to Review)

SQL Server Spatial Types
~~~~~~~~~~~~~~~~~~~~~~~~~

**Symptoms**: ``Could not load file or assembly 'Microsoft.SqlServer.Types'``

**Solutions**:

1. Ensure SqlServerTypes folder is deployed with the service
2. Check that x86/x64 native DLLs are present
3. Verify IIS application pool matches the DLL architecture
4. The service automatically loads these on startup

Connection Timeouts
~~~~~~~~~~~~~~~~~~~

**Symptoms**: Operations fail with timeout errors

**Solutions**:

1. Increase ``Command Timeout`` in connection string
2. Add spatial indexes to large tables
3. Optimize queries for section-based retrieval
4. Consider batch size for bulk operations

Development
===========

Building the Project
--------------------

**Prerequisites**:

* Visual Studio 2019 or later
* .NET Framework 4.8 SDK
* SQL Server Data Tools

**Build Commands**::

    # Debug build
    msbuild AnnotationService.csproj /p:Configuration=Debug

    # Release build
    msbuild AnnotationService.csproj /p:Configuration=Release

Testing
-------

Use tools like:

* **WCF Test Client**: Built-in Visual Studio tool for testing WCF services
* **Postman**: For testing HTTP endpoints with JWT tokens
* **Custom Client**: Use the Viking client application for integration testing

Debugging
---------

1. Enable detailed WCF tracing in ``web.config``::

    <system.diagnostics>
        <sources>
            <source name="System.ServiceModel" switchValue="Verbose,ActivityTracing" />
        </sources>
    </system.diagnostics>

2. Check trace files at the configured location (``C:\Temp\WCFAnnotationBinary3.svclog``)
3. Use Service Trace Viewer (``SvcTraceViewer.exe``) to analyze traces

Migration Notes
===============

From Legacy Authentication
--------------------------

The service supports both legacy ASP.NET Membership and modern JWT authentication simultaneously. To migrate:

1. Configure IdentityServer with user accounts
2. Update client applications to obtain JWT tokens
3. Include JWT token in ``Authorization: Bearer`` header
4. Legacy username/password authentication still validated for compatibility

API Versioning
--------------

This service maintains backwards compatibility. Changes to the data contracts should be additive only to prevent breaking existing clients.

Related Services
================

* **GrpcAnnotationService**: Modern gRPC-based annotation service
* **OData Service**: REST/OData interface for querying annotations
* **Export Service**: Data export and transformation services
* **IdentityServer**: Authentication and token issuing service

Contributing
============

When modifying the service:

1. Maintain backwards compatibility with existing clients
2. Add appropriate ``[PrincipalPermission]`` attributes to new operations
3. Update this README with any configuration changes
4. Test with JWT authentication
5. Verify queries with production-scale data

License
=======

Copyright © James Anderson 2008

Support
=======

For issues or questions:

* Review trace logs in ``C:\Temp\WCFAnnotationBinary<#>.svclog``
* Check Windows Event Viewer for security audit events
* Enable debug logging in web.config
* Consult ``Installation.rst`` for deployment guidance

Version History
===============

**1.0.0** (Current)
  - JWT authentication integration
  - IdentityServer support
  - Modern token validation with automatic key rotation
  - Volume-specific role mapping
  - Enhanced security audit logging
  - Protocol Buffers serialization
  - SQL Server 2012+ spatial types support

