using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Extensions.Logging;

namespace ConnectomeDataModelCoreTest
{
    
    public class Startup
    {
        public void ConfigureHost(IHostBuilder hostBuilder) =>
            hostBuilder
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.AddJsonFile("appsettings.json", optional: false);
                });

        public void ConfigureServices(IServiceCollection services, HostBuilderContext context)
        {
            var configuration = context.Configuration;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File("logs/test.log", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            services.AddLogging(builder => builder.AddSerilog(Log.Logger));

            var connectionString = configuration.GetConnectionString("AnnotationConnection");

            services.AddDbContext<Viking.DataModel.Annotation.AnnotationContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TestConnection"),
                        options => options.UseNetTopologySuite())
                    .EnableDetailedErrors()
                    .EnableSensitiveDataLogging());
        } 
    }
}