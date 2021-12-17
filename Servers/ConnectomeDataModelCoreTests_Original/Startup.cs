using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace ConnectomeDataModelCoreTests
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        { 
            Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("AnnotationConnection");

            services.AddSingleton<IConfiguration>(Configuration);

            services.AddDbContext<Viking.DataModel.Annotation.AnnotationContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("AnnotationConnection"),
                                     options => options.UseNetTopologySuite())
                       .EnableDetailedErrors()
                       .EnableSensitiveDataLogging());
        }
         
    }
}
