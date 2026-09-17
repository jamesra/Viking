=====================
Installation Guide
=====================

Quick Start
===========

For complete documentation, see ``README.rst``.

This guide covers the basic installation steps for deploying the Viking Annotation Service to IIS.

Prerequisites
=============

Software Requirements
---------------------

* Windows Server 2016 or later
* IIS 10.0 or later with the following features:

  - ASP.NET 4.8
  - WCF HTTP Activation
  - WebSocket Protocol (optional)
  
* .NET Framework 4.8 Runtime
* SQL Server 2012 or later (with spatial types support)
* Valid SSL/TLS certificate

NuGet Packages
--------------

The project uses NuGet for dependency management. Restore packages before deployment::

    nuget restore AnnotationService.csproj

Or use Visual Studio's automatic package restore.

Installation Steps
==================

1. IIS Configuration
--------------------

Create the Website Structure
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

1. Open IIS Manager
2. Create a new website or use an existing one
3. Create a folder structure for your volume::

    YourVolume/
      ├── Annotation/     (This service)
      ├── OData/          (OData service)
      └── Export/         (Export service)

Configure SSL
~~~~~~~~~~~~~

1. In IIS Manager, select your website
2. Open "Bindings" from the Actions pane
3. Add HTTPS binding:

   - Type: ``https``
   - Port: ``443``
   - SSL Certificate: Select your certificate
   
4. (Optional) Enforce HTTPS by removing HTTP binding or adding redirect rules

Configure Authentication
~~~~~~~~~~~~~~~~~~~~~~~~

1. Select the Annotation application in IIS Manager
2. Open "Authentication"
3. Enable:

   - **Anonymous Authentication**: Enabled
   - **Windows Authentication**: Disabled (unless needed for debugging)
   - **Forms Authentication**: Enabled
   
4. Disable all other authentication methods

Configure Application Pool
~~~~~~~~~~~~~~~~~~~~~~~~~~

1. Create a new Application Pool or use existing:

   - .NET CLR Version: ``.NET CLR Version v4.0``
   - Managed Pipeline Mode: ``Integrated``
   - Identity: ``ApplicationPoolIdentity`` or specific service account with database access
   
2. Assign the Application Pool to your Annotation application

2. Database Configuration
-------------------------

Create/Update Connection Strings
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

Edit ``web.config`` in the deployed Annotation folder::

    <connectionStrings>
        <add name="ConnectomeEntities" 
             connectionString="metadata=res://*/;
                              provider=System.Data.SqlClient;
                              provider connection string=&quot;
                              data source=YOUR-SQL-SERVER;
                              initial catalog=YOUR-DATABASE;
                              integrated security=True;
                              multipleactiveresultsets=True;
                              Type System Version=SQL Server 2012;
                              application name=AnnotationService;
                              Connection Timeout=300;
                              Command Timeout=300&quot;" 
             providerName="System.Data.EntityClient" />
        <add name="VikingApplicationServices" 
             connectionString="Server=YOUR-SQL-SERVER,1433;
                              Database=UserAccounts;
                              Integrated Security=true" />
    </connectionStrings>

Grant Database Permissions
~~~~~~~~~~~~~~~~~~~~~~~~~~

Ensure the IIS Application Pool identity has:

* ``db_datareader`` and ``db_datawriter`` roles on the connectome database
* ``EXECUTE`` permission on stored procedures
* Access to the UserAccounts database (for legacy authentication)

3. Identity Server Configuration
---------------------------------

Update ``web.config`` with your IdentityServer endpoint::

    <appSettings>
        <!-- IdentityServer Configuration -->
        <add key="IdentityServer:authority" value="https://identity.example.com:5001/" />
        <add key="IdentityServer:audience" value="Viking.Annotation.API" />
        
        <!-- Volume Configuration -->
        <add key="VolumeName" value="YourVolumeName" />
        <add key="DatabaseName" value="YourDatabaseName" />
        
        <!-- Service URLs -->
        <add key="EndpointURL" value="https://your-server.com/YourVolume/Annotation/Annotate.svc" />
        <add key="VolumeURL" value="http://your-server.com/YourVolume" />
    </appSettings>

**Important**: The ``VolumeName`` must match the prefix used in JWT token scopes (e.g., ``TemporalMonkey`` for scopes like ``TemporalMonkey.Read``).

4. Service Certificate Configuration
-------------------------------------

Update the certificate settings in ``web.config``::

    <serviceCredentials>
        <serviceCertificate 
            findValue="your-cert-name" 
            storeLocation="LocalMachine" 
            x509FindType="FindBySubjectName"/>
    </serviceCredentials>

Replace ``your-cert-name`` with:

* The subject name of your certificate (e.g., ``example.com``)
* Or change ``x509FindType`` to ``FindByThumbprint`` and use the certificate thumbprint

5. Deploy the Service
---------------------

Using Deployment Scripts
~~~~~~~~~~~~~~~~~~~~~~~~

From Visual Studio:

1. Build the project in Release mode
2. Run the deployment script::

    DeployProduction.cmd \\YOUR-IIS-SERVER\c$\inetpub\wwwroot

Or manually copy files:

1. Copy all files from ``bin\Release\`` to the IIS Annotation folder
2. Ensure ``web.config`` is copied and configured
3. Copy the ``SqlServerTypes`` folder with native DLLs

Using Visual Studio Publish
~~~~~~~~~~~~~~~~~~~~~~~~~~~~

1. Right-click the project in Solution Explorer
2. Select "Publish"
3. Choose "Folder" or "IIS, FTP, etc." profile
4. Configure the target location
5. Click "Publish"

6. Verify Installation
-----------------------

Check Service Metadata
~~~~~~~~~~~~~~~~~~~~~~

Navigate to the service metadata endpoint::

    https://your-server.com/YourVolume/Annotation/Annotate.svc

You should see the WCF service page with links to WSDL.

Check Service Health
~~~~~~~~~~~~~~~~~~~~

Use the WCF Test Client:

1. Open Visual Studio Developer Command Prompt
2. Run::

    WcfTestClient.exe https://your-server.com/YourVolume/Annotation/Annotate.svc

3. Test the ``CanRead()`` operation (requires authentication)

Test with Viking Client
~~~~~~~~~~~~~~~~~~~~~~~

1. Open the Viking client application
2. Configure the server URL
3. Attempt to connect with valid credentials
4. Verify you can view and create annotations

7. Configure Logging (Optional)
--------------------------------

Enable WCF Tracing
~~~~~~~~~~~~~~~~~~

Uncomment or add to ``web.config``::

    <system.diagnostics>
        <sources>
            <source name="System.ServiceModel" 
                    switchValue="Information,ActivityTracing" 
                    propagateActivity="true">
                <listeners>
                    <add name="traceListener" 
                         type="System.Diagnostics.XmlWriterTraceListener" 
                         initializeData="C:\Logs\AnnotationService.svclog" />
                </listeners>
            </source>
        </sources>
    </system.diagnostics>

Ensure the log directory exists and IIS has write permissions.

Security Audit Logging
~~~~~~~~~~~~~~~~~~~~~~

The service automatically logs security events to Windows Event Log. Enable in ``web.config``::

    <serviceSecurityAudit 
        auditLogLocation="Application" 
        serviceAuthorizationAuditLevel="SuccessOrFailure" 
        messageAuthenticationAuditLevel="SuccessOrFailure"/>

Troubleshooting
===============

Service Won't Start
-------------------

**Check Event Viewer**:

1. Open Windows Event Viewer
2. Check "Application" log for errors
3. Look for messages from "ASP.NET" or "System.ServiceModel"

**Common Issues**:

* Missing .NET Framework 4.8
* Incorrect Application Pool configuration
* Database connection string errors
* Missing SQL Server Types DLLs

HTTP 500 Errors
---------------

**Enable Detailed Errors**:

Edit ``web.config``::

    <system.web>
        <customErrors mode="Off" />
    </system.web>

**Check**:

* Database connectivity
* Certificate configuration
* Application Pool identity permissions

Authentication Failures
-----------------------

**JWT Token Issues**:

* Verify IdentityServer is accessible
* Check ``IdentityServer:authority`` URL is correct
* Ensure clock synchronization between servers
* Verify SSL certificate chain is trusted

**Legacy Authentication**:

* Check ``VikingApplicationServices`` connection string
* Verify SQL membership database exists
* Ensure ASP.NET providers are configured

403 Forbidden / Authorization Failures
---------------------------------------

**Check**:

* User has required roles in JWT token
* Volume name matches between token scope and configuration
* Role names are correct (Read, Write, Annotate, Review)
* Principal permission attributes are satisfied

SQL Server Connection Issues
-----------------------------

**Verify**:

* SQL Server allows remote connections
* Firewall allows traffic on SQL port (1433)
* Application Pool identity has database permissions
* Connection string syntax is correct
* SQL Server Browser service is running (for named instances)

Upgrading
=========

From Previous Versions
-----------------------

1. Back up current ``web.config``
2. Deploy new binaries
3. Merge configuration changes
4. Test with a non-production volume first
5. Monitor Event Log for errors

Database Schema Updates
-----------------------

If Entity Framework model changes require schema updates:

1. Generate migration script
2. Test in development environment
3. Schedule maintenance window
4. Apply schema changes
5. Deploy updated service binaries

Next Steps
==========

After successful installation:

1. Configure user roles in IdentityServer
2. Set up volume metadata in database
3. Configure structure types and permitted links
4. Test with Viking client application
5. Set up monitoring and log aggregation
6. Document volume-specific configuration

See ``README.rst`` for detailed information about:

* Service architecture
* API operations
* Security configuration
* Performance tuning
* Development guidelines

Additional Resources
====================

* **Service Documentation**: ``README.rst``
* **Docker Configuration**: ``../../../Docker-Configuration-README.md``
* **Viking Client**: ``../../../Clients/Viking/``
* **Database Schema**: ``../../../Database/``

Support
=======

For installation issues:

1. Check Windows Event Viewer
2. Review WCF trace logs (if enabled)
3. Verify all prerequisites are met
4. Consult ``README.rst`` troubleshooting section
5. Test connectivity with WCF Test Client
