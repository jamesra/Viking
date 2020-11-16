At the Marc lab this site runs under the OpR-Marc-VikingID user.

This project was outside my comfort zone, and I only deal with this beast rarely.  Here is a review:

The databases are genereted by objects in the assemblies.  There are three databases.
 
  * VikingIdentity: Stores the users, roles, and groups.  Used by Viking to control access
  * IdentityPersistedGrants: Stores the tokens issued so they do not expire if I reset the web server or app
  * IdentityConfig: The configuration for the identity server.  At this time I have not ported the configuration 
    from code into the SQL database.  I may not get there. 

To Initially populate the database follow these steps:

  * Delete the database from SQL if it exists
   
    1. dotnet ef database drop --no-build --context PersistedGrantDbContext
    2. dotnet ef database drop --no-build --context ApplicationDBContext
    3. dotnet ef database drop --no-build --context ConfigurationDbContext

  * Delete the migrations from the project for the database to be recreated
  * Create an initial migration.  (Note at this time I precompile the project because of a dependency issue.) These are the three commands:
        
    1. dotnet ef migrations add InitialIdentityServerPersistedGrantDbMigration -c PersistedGrantDbContext -o Data/Migrations/IdentityServer/PersistedGrantDb -v --no-build
    2. dotnet ef migrations add InitialApplicationDatabaseMigration -c ApplicationDbContext -o Data/Migrations/Application -v --no-build
    3. dotnet ef migrations add InitialIdentityServerConfigurationDbMigration -c ConfigurationDbContext -o Data/Migrations/IdentityServer/ConfigurationDb --no-build

  * Recompile the code
  * Populate the database
   
    1. dotnet ef database update --no-build -v --context PersistedGrantDbContext
    2. dotnet ef database update --no-build -v --context ApplicationDbContext
    3. dotnet ef database update --no-build -v --context ConfigurationDbContext

SSL
---

Install the SSL certificate on the server.  When I did this I needed to put the certificate in my personal store, but I do suspect it could have worked in the machine store with more research on my part. 
Ensure that the identities (active directory users) for the IIS application pool running the Identity Server have access to the private SSL key.
    
       * Certificate manager (certlm) allows one to browse certificates on the machine.  Right click the certificate, select "All Tasks -> Manage Private Keys" from the context menu to edit access rights to the key.
       * Ensure the IIS website has an SSL binding that points to the certificate you want to use. 
       * Disable HTTP access to the identity website to prevent unencrypted communication.
       * Ensure the appsettings.json file refers to the correct SSL key.  The serial number is stored under "SSL -> SerialNumber"

Deploy to IIS
-------------

The Visual Studio project is current configured to deploy to IIS.  To set this up on a new server:

    * Install Web Deploy 3.6 on the Server via IIS Manager's Web Platform Installer
    * Configure Web Deploy via "Deploy -> Configure Web Deploy" context menu option of main website in IIS Manager
    * Within the visual studio project, select "publish" from the projects context menu and specify the IIS Server.



Debugging
=========
    
    The appsettings.Development.json file can be editted to apply debug only settings.  One use of this could be to create a debug version of the IdentityViking database using a different connection string.
Then develop against that and only migrate production after changes have been tested.  I haven't setup the debug database yet because this is the first deployment.

    Install the visual studio remote debugging package on the server.  Run the remote debugger on the server as an administrator when you wish to debug. 
