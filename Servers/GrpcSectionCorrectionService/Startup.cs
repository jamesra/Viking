using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Viking.SectionCorrection;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Volume catalog, rebuild hosted service, and AnnotateSectionCorrections. Auth is optional:
    /// when IdentityServer:Endpoint is empty, gRPC is anonymous so in-process tests and local Docker work.
    /// </summary>
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddGrpc(options =>
            {
#if DEBUG
                options.EnableDetailedErrors = true;
#endif
            });

            string correctionsRoot = Configuration["Corrections:RootDirectory"];
            services.AddSingleton(_ =>
            {
                var catalog = new VolumeCorrectionCatalog(correctionsRoot);
                catalog.Load();
                catalog.Watch();
                return catalog;
            });

            services.AddSingleton(AnnotationConnectionResolver.FromConfiguration(Configuration));
            services.AddSingleton<IIdentityVolumeSource>(sp =>
            {
                string identity = Configuration.GetConnectionString("IdentityConnection");
                return new SqlIdentityVolumeSource(identity);
            });
            services.AddSingleton<IVolumeCorrectionPublisher, VolumeCorrectionPublisherAdapter>();
            services.AddSingleton<RebuildStatusStore>();
            services.AddSingleton<CorrectionRebuildRunner>();
            services.AddHostedService<CorrectionRebuildHostedService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<SectionCorrectionsService>();
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync(
                        "Communication with gRPC endpoints must be made through a gRPC client.");
                });
            });
        }
    }
}
