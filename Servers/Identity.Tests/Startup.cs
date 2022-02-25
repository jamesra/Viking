using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging; 

namespace TestIdentityModel
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.test.json")
                .AddEnvironmentVariables()
                .Build();
                services.AddTransient<IPasswordHasher<ApplicationUser>,PasswordHasher<ApplicationUser>>();
            services.AddSingleton<IConfiguration>(config);
            //services.ConfigureIdentityServerDataContext(config.GetRequiredSection("DataContext"));
        }

        public void ConfigureHost(IHostBuilder hostBuilder)
        {
        }
    }
}
