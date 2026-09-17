using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Viking.DataModel.Annotation
{
    /// <summary>
    /// Supplies a context to the dotnet-ef design-time tools, which cannot use the
    /// application's dependency injection container.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AnnotationContext>
    {
        /// <summary>Environment variable consulted when no connection string is passed on the command line.</summary>
        public const string ConnectionStringVariable = "ANNOTATION_CONNECTION";

        public AnnotationContext CreateDbContext(string[] args)
        {
            var connectionString = args.Length > 0
                ? args[0]
                : Environment.GetEnvironmentVariable(ConnectionStringVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"No annotation database connection string. Set {ConnectionStringVariable} or pass one as the " +
                    "first argument, for example: dotnet ef dbcontext info -- \"Server=.;Database=Annotation;Trusted_Connection=True\"");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AnnotationContext>();
            optionsBuilder.UseSqlServer(connectionString, config => config.UseNetTopologySuite())
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging();
            return new AnnotationContext(optionsBuilder.Options);
        }
    }
}
