using System;
using System.Linq;
using Viking.Identity.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

///
///The tests build and destroy the database frequently.  We could randomly generate the
/// database name for each test, but simpler to disable parallelism.  These tests are fast
/// so far.  By default all tests in this assembly are in the same collection and will run
/// serially
/// 
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Microsoft.Extensions.DependencyInjection
{
    public static class Startup
    {
        public static IServiceCollection ConfigureIdentityServerDataContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connString = configuration.GetConnectionString("IdentityConnection");
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connString).EnableDetailedErrors().EnableSensitiveDataLogging());
             
            return services;
        }

        public static void InitializeDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
                /*
                Special.AdminRoleId = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.FirstOrDefault(ur => ur.Name == Special.Roles.Admin)?.Id;

                if (Special.Roles.Admin is null)
                    throw new InvalidOperationException(
                        "Admin role \"{Special.Roles.Admin}\" appears to be missing in the identity database");
                */
            }
        }
    }
}