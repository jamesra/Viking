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
    /// Correction catalog, rebuild hosted service, and AnnotateSectionCorrections. The contexts to correct come from
    /// Identity (<see cref="IdentityAssignmentClient"/>) when IdentityServer settings and CorrectionService:Name are
    /// configured, else from the Identity database directly. gRPC itself is anonymous; callers are not authenticated.
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
                options.Interceptors.Add<CorrectionRequestLoggingInterceptor>();
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
            // Identity is the source of truth for the contexts assigned to this service. Without Identity settings
            // (local runs, tests) fall back to reading the Identity database directly.
            IdentityAssignmentOptions identityOptions = IdentityAssignmentOptions.FromConfiguration(Configuration);
            services.AddSingleton(identityOptions);
            if (identityOptions.IsConfigured)
            {
                services.AddHttpClient<IIdentityVolumeSource, IdentityAssignmentClient>((http, sp) =>
                    new IdentityAssignmentClient(
                        http,
                        identityOptions,
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IdentityAssignmentClient>>()));
            }
            else
            {
                services.AddSingleton<IIdentityVolumeSource>(sp =>
                {
                    string identity = Configuration.GetConnectionString("IdentityConnection");
                    return new SqlIdentityVolumeSource(identity);
                });
            }
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
