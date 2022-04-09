using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Viking.DataModel.Annotation.Tests
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup()
        { 
            Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions<ContextBuilderOptions<AnnotationContext>>()
                .Configure(o =>
                {
                    o.ConnectionStringName = "AnnotationConnection";
                });

            services.AddSingleton<IConfiguration>(Configuration);

            services.AddTransient<IContextBuilder<AnnotationContext>, ContextBuilder<AnnotationContext>>();

            services.AddTransient<AnnotationContext>();
        }
    }
}
