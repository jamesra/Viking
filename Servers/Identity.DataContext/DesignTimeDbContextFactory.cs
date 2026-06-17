using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Viking.Identity.Data
{
    /// <summary>
    /// Design-time factory for ApplicationDbContext.
    /// This allows Entity Framework Core Tools to create the DbContext at design time.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddUserSecrets<DesignTimeDbContextFactory>(optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("IdentityConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'IdentityConnection' is missing or empty. " +
                    "For EF Core tools (e.g. dotnet ef database update), provide the full SQL Server connection string via user secrets or an environment variable (not only appsettings.json if it is templated). " +
                    "User secrets: dotnet user-secrets set \"ConnectionStrings:IdentityConnection\" \"<connection string>\" --project <path to Identity.DataContext.csproj>. " +
                    "Environment variable: ConnectionStrings__IdentityConnection=<connection string>. " +
                    "If AddJsonFile fails to find appsettings.json, run the command with the project directory as the current directory or use --project so tooling resolves the project folder.");
            }

            if (connectionString.Contains("${", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Connection string 'IdentityConnection' still contains '${...}' placeholders. " +
                    ".NET configuration does not substitute those; appsettings.json is only a template. " +
                    "Set the full connection string in user secrets (ConnectionStrings:IdentityConnection) or environment variable ConnectionStrings__IdentityConnection.");
            }

            // Create DbContext options
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            // Create and return the DbContext
            // Note: We pass null for passwordHasher and log since they're not needed for design-time operations
            return new ApplicationDbContext(optionsBuilder.Options, null, null);
        }
    }
}
