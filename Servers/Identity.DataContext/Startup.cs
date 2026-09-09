using System;
using Viking.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class Startup
    {
        public static IServiceCollection ConfigureIdentityServerDataContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connString = configuration.GetConnectionString("IdentityConnection");
            var isDevelopment = string.Equals(
                configuration["ASPNETCORE_ENVIRONMENT"],
                "Development",
                StringComparison.OrdinalIgnoreCase);

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(connString).EnableDetailedErrors();
                if (isDevelopment)
                    options.EnableSensitiveDataLogging();
            });

            return services;
        }
    }
}
