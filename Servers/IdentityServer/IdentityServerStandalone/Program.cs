using Duende.IdentityServer.Endpoints;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityModel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Viking.Identity;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.SSL;
using DotNetEnv;
using ConfigurationSubstitution;

namespace Viking.Identity.Server.Standalone
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IdentityModelEventSource.ShowPII = true; // Enable detailed error messages for development

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("IdentityServerApiLogs.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            var envFile= ".env";
            Env.TraversePath().Load(envFile);

            var buildEnvFile = $".env.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}";
            Env.TraversePath().Load(buildEnvFile);


            try
            {
                Log.Information("Starting IdentityServer...");
            CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((ctx, builder) =>
                {
                    builder.EnableSubstitutionsWithDelimitedFallbackDefaults("${", "}", ":", UnresolvedVariableBehaviour.Throw);
                })
                .UseSerilog()
                .ConfigureWebHostDefaults(webBuilder =>
                {                    
                    webBuilder.ConfigureServices((context, services) => ConfigureServices(services, context.Configuration));
                    webBuilder.Configure((context, app) => Configure(app, context.HostingEnvironment));
                    webBuilder.ConfigureKestrel(options =>
                    {
                        var configuration = new ConfigurationBuilder()
                                .SetBasePath(Directory.GetCurrentDirectory())
                                .AddJsonFile("appsettings.json", optional: true)
                                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                                .AddEnvironmentVariables()
                                .EnableSubstitutions()
                                .Build();

                        var sslOptions = configuration.GetSection("SSL").Get<SSLOptions>();

                        var http_port = configuration.GetValue<int>("IDENTITY_STANDALONE_CONTAINER_HTTP_PORT");
                        var https_port = configuration.GetValue<int>("IDENTITY_STANDALONE_CONTAINER_HTTPS_PORT");

                        // Configure HTTP and HTTPS endpoints
                        options.ListenAnyIP(http_port); // HTTP port
                        options.ListenAnyIP(https_port, listenOptions => // HTTPS port
                        {
                            // Configure HTTPS with custom certificate 
                            var cert = Certs.LoadSSLCertificate(sslOptions);
                            if(cert != null)
                            {
                                try
                                {
                                    Log.Information("Certificate found");
                                    listenOptions.UseHttps(cert);
                                    Log.Information("Successfully configured Kestrel HTTPS with certificate: {Subject}", cert.Subject);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to load certificate into Kestrel.");
                                    listenOptions.UseHttps(); // Fallback to development certificate
                                }
                            } 
                            else
                            {
                                Log.Warning("SSL configuration not found or certificate path is empty - using development certificate");
                                listenOptions.UseHttps(); // Use development certificate
                            }
                        });
                    });
                });

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            var https_port = configuration.GetValue<int>("IDENTITY_STANDALONE_CONTAINER_HTTPS_PORT");
            var SSLOptions = configuration.GetSection("SSL").Get<Viking.SSL.SSLOptions>();
            var sslCert = Certs.LoadSSLCertificate(SSLOptions);

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = https_port;
            });
            services.AddRazorPages();

            services.ConfigureIdentityServerDataContext(configuration);

            var persistedGrantConnectionString = configuration.GetConnectionString("PersistedGrantConnection");
            Log.Information($"Grant Connection String: {persistedGrantConnectionString}");
            var migrationsAssembly = typeof(Program).GetTypeInfo().Assembly.GetName().Name;

            var serverOptions = configuration.GetSection(nameof(VikingIdentityServerOptions)).Get<VikingIdentityServerOptions>();

            // Add detailed logging for configuration
            Log.Information("=== IDENTITYSERVER CONFIGURATION DEBUG ===");
            Log.Information("serverOptions is null: {IsNull}", serverOptions == null);

            if (serverOptions != null)
            {
                Log.Information("Authority: {Authority}", serverOptions.Authority);
                Log.Information("Secret: {Secret}", serverOptions.Secret);
                var apiscopes = serverOptions.ApiScopes ?? Array.Empty<ApiScope>();
                Log.Information("ApiScopes count: {Count}", apiscopes.Length);
                foreach (var scope in apiscopes)
                {
                    Log.Information("  Scope: {Name} - {Description}", scope.Name, scope.Description);
                }
            }
            else
            {
                Log.Error("VikingIdentityServerOptions is NULL! Check appsettings configuration.");
            }

            services.Configure<VikingIdentityServerOptions>(
                configuration.GetSection(nameof(VikingIdentityServerOptions)));
              
            services.AddIdentity<ApplicationUser, ApplicationRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
             
            var builder = services.AddIdentityServer(options =>
            {                
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.KeyManagement.Enabled = false;
                options.EmitStaticAudienceClaim = true;
                 
                if (serverOptions != null)
                {
                    options.IssuerUri = serverOptions.Authority;
                    Log.Information("Set IdentityServer IssuerUri to: {IssuerUri}", options.IssuerUri);
                }
                else
                {
                    Log.Error("Cannot set IssuerUri - serverOptions is null!");
                }
            }); 

            // Add logging for each configuration step
            Log.Information("Adding InMemory ApiScopes...");
            var apiScopes = Config.GetApiScopes(serverOptions);
            Log.Information("ApiScopes count: {Count}", apiScopes?.Count() ?? 0);
            builder.AddInMemoryApiScopes(apiScopes);

            Log.Information("Adding InMemory ApiResources...");
            var apiResources = Config.GetApiResources(serverOptions);
            Log.Information("ApiResources count: {Count}", apiResources?.Count() ?? 0);
            builder.AddInMemoryApiResources(apiResources);

            Log.Information("Adding InMemory Clients...");
            var clients = Config.GetClients(serverOptions);
            Log.Information("Clients count: {Count}", clients?.Count() ?? 0);
            builder.AddInMemoryClients(clients);

            Log.Information("Adding InMemory IdentityResources...");
            var identityResources = Config.GetIdentityResources();
            Log.Information("IdentityResources count: {Count}", identityResources?.Count() ?? 0);
            builder.AddInMemoryIdentityResources(identityResources);

            Log.Information("Adding Resource Store...");
            builder.AddResourceStore<IdentityServerCustomResourceStore>();

            Log.Information("Adding Client Store...");
            builder.AddClientStore<IdentityServerVikingClientStore>();

            Log.Information("Adding Operational Store...");
            builder.AddOperationalStore(options =>
            {
                options.ConfigureDbContext = builder =>
                    builder.UseSqlServer(persistedGrantConnectionString,
                        sql => sql.MigrationsAssembly(migrationsAssembly));
                options.EnableTokenCleanup = true;
                options.TokenCleanupInterval = 3600;
            });

            Log.Information("Adding remaining services...");
            builder.AddInMemoryCaching()
                .AddAspNetIdentity<ApplicationUser>()
                .AddDefaultEndpoints();

            Log.Information("=== END IDENTITYSERVER CONFIGURATION DEBUG ===");

            // Configure Data Protection with Docker-compatible settings
            var dataProtectionKeysPath = @"./DataProtectionKeys/";
            var dataProtectionBuilder = builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
                .SetApplicationName("VikingIdentityServer");

            // Only use certificate-based key encryption if not in Docker environment
            var isDockerEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Docker";

            if (!isDockerEnvironment)
            {
                if (sslCert != null)
                {
                    try
                    {
                        dataProtectionBuilder.ProtectKeysWithCertificate(sslCert);
                        Log.Information("Data Protection configured with certificate encryption");
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to configure Data Protection with certificate, using file system protection only");
                    }
                }
                else
                {
                    Log.Warning("No certificate found for Data Protection, using file system protection only");
                }
            }
            else
            {
                Log.Information("Docker environment detected - Data Protection configured with file system protection only");
            }

            // Load SSL certificate and configure IdentityServer 
            if (sslCert != null)
            {
                try
                {
                    builder.AddSigningCredential(sslCert);
                    Log.Information("Successfully configured IdentityServer with certificate. Subject: {Subject}, Thumbprint: {Thumbprint}",
                        sslCert.Subject, sslCert.Thumbprint);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to configure IdentityServer signing credentials with certificate");
                    builder.AddDeveloperSigningCredential();
                }
            }
            else
            {
                Log.Warning("No valid certificate found. Using developer signing credential for IdentityServer.");
                builder.AddDeveloperSigningCredential();
            }

            // Configure email options
            services.Configure<EmailOptions>(configuration.GetSection("Email"));

            // Register EmailSender for Identity email functionality
            services.AddTransient<IEmailSender<ApplicationUser>, EmailSender>();
        }

        private static void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Log.Information("Starting IdentityServer configuration...");

            // this will do the initial DB population and required migrations
            InitializeDatabase(app);

            if (env.IsDevelopment() || env.EnvironmentName == "Docker")
            {
                Log.Information("Using Developer Exception Pages...");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                Log.Information("Using Error page for exceptions...");
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseSerilogRequestLogging();
            app.UseForwardedHeaders();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            Log.Information("Adding IdentityServer middleware...");
            app.UseIdentityServer();
            Log.Information("IdentityServer middleware added successfully.");
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages().RequireAuthorization();
                
                // Default homepage
                endpoints.MapGet("/", async context =>
                {
                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>Identity Server</title>
                        <style>
                            body { font-family: Arial, sans-serif; text-align: center; margin-top: 100px; background-color: #f5f5f5; }
                            h1 { color: #333; font-size: 3em; margin-bottom: 20px; }
                            p { color: #666; font-size: 1.2em; }
                        </style>
                    </head>
                    <body>
                        <h1>Identity Server</h1>
                        <p>Welcome to the Viking Identity Server</p>
                    </body>
                    </html>");
                });

                endpoints.MapRazorPages();
                Log.Information("Endpoints mapped successfully.");


                // Log endpoints immediately
                var endpointDataSource = endpoints.ServiceProvider.GetRequiredService<EndpointDataSource>();
                Log.Information("Immediate endpoint count: {Count}", endpointDataSource.Endpoints.Count);

                // Schedule delayed endpoint logging
                var lifetime = endpoints.ServiceProvider.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>();
                lifetime.ApplicationStarted.Register(() =>
                {
                    Task.Delay(5000).ContinueWith(_ => // Wait 5 seconds after startup
                    {
                        var delayedEndpointDataSource = endpoints.ServiceProvider.GetRequiredService<EndpointDataSource>();
                        Log.Information("Delayed endpoint count: {Count}", delayedEndpointDataSource.Endpoints.Count);

                        foreach (var endpoint in delayedEndpointDataSource.Endpoints)
                        {
                            Log.Information("Delayed Endpoint: {DisplayName}", endpoint.DisplayName);
                        }
                    });
                });
            });
        }

        private static IApplicationBuilder InitializeDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                // Only initialize the main application database
                serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
                // Skip PersistedGrants database initialization for now
                Log.Information("Skipping PersistedGrants database initialization - using in-memory operational store");
            }

            return app;
        }
    }
}
