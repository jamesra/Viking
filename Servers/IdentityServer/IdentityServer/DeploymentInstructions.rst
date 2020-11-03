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


Debugging
=========
    
    The appsettings.Development.json file can be editted to apply debug only settings.  One use of this could be to create a debug version of the IdentityViking database using a different connection string.
Then develop against that and only migrate production after changes have been tested.  I haven't setup the debug database yet because this is the first deployment.

