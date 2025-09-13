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
            // Build configuration from appsettings.json
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Get connection string
            var connectionString = configuration.GetConnectionString("IdentityConnection");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'IdentityConnection' not found. " +
                    "Please ensure appsettings.json exists in the Identity.DataContext project " +
                    "and contains the IdentityConnection connection string.");
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
