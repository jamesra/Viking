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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Viking.Identity;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server;
using Viking.SSL;
using DotNetEnv;
using ConfigurationSubstitution;

namespace Viking.Identity.Server.Standalone
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var envFile = ".env";
            Env.TraversePath().Load(envFile);

            var aspnetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var hostingEnv = Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") ?? "Local";
            IdentityModelEventSource.ShowPII = string.Equals(aspnetCoreEnv, "Development", StringComparison.OrdinalIgnoreCase);
            
            var buildEnvFile = $".env.{aspnetCoreEnv}";
            Env.TraversePath().Load(buildEnvFile);
            
            var hostingEnvFile = $".env.{hostingEnv}";
            Env.TraversePath().Load(hostingEnvFile);


            try
            {
                var builder = WebApplication.CreateBuilder(args);

                // Enable environment variable substitution in the main configuration 
                builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                               .AddJsonFile($"appsettings.{aspnetCoreEnv}.json", optional: true)
                               .AddJsonFile($"appsettings.{hostingEnv}.json", optional: true)
                               .AddEnvironmentVariables()
                               .AddUserSecrets<Program>(optional: true)
                               .EnableSubstitutions("${", "}", UnresolvedVariableBehaviour.Throw);

                // Configure Serilog
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext() 
                    .WriteTo.Console() 
                    .WriteTo.File("IdentityServerApiLogs.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                );

                Log.Information("Starting IdentityServer...");
                
                // Configure services
                ConfigureServices(builder.Services, builder.Configuration);

                // Configure Kestrel
                ConfigureKestrel(builder.WebHost, builder.Configuration);

                var app = builder.Build();

                // Configure the HTTP request pipeline
                Configure(app, builder.Environment);

                app.Run();
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

        private static void ConfigureKestrel(IWebHostBuilder webHostBuilder, IConfiguration configuration)
        {
            webHostBuilder.ConfigureKestrel(options =>
            {  
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
                        catch (CryptographicException ex)
                        {
                            Log.Error(ex, "Failed to load certificate into Kestrel due to cryptographic error.");
                            listenOptions.UseHttps(); // Fallback to development certificate
                        }
                        catch (ArgumentException ex)
                        {
                            Log.Error(ex, "Failed to load certificate into Kestrel due to invalid certificate format.");
                            listenOptions.UseHttps(); // Fallback to development certificate
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to load certificate into Kestrel due to unexpected error.");
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
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            var https_port = configuration.GetValue<int>("IDENTITY_STANDALONE_CONTAINER_HTTPS_PORT");
            var SSLOptions = configuration.GetSection("SSL").Get<Viking.SSL.SSLOptions>();
            var sslCert = Certs.LoadSSLCertificate(SSLOptions);

            // Configure basic services
            ConfigureBasicServices(services, https_port);
            
            // Configure Identity Server
            ConfigureIdentityServer(services, configuration, sslCert);
            
            // Configure Data Protection (shared with WebManagement)
            SharedIdentityDataProtection.AddSharedIdentityDataProtection(
                services, sslCert, AllowDeveloperSigningCredential());
            
            // Configure Email services
            ConfigureEmailServices(services, configuration);
        }

        private static void ConfigureBasicServices(IServiceCollection services, int httpsPort)
        {
            if (httpsPort == 0)
                httpsPort = 5001; // Default HTTPS port
            
            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = httpsPort;
            });
            services.AddControllersWithViews();
            services.AddRazorPages();
        }

        private static void ConfigureIdentityServer(IServiceCollection services, IConfiguration configuration, X509Certificate2 sslCert)
        {
            services.ConfigureIdentityServerDataContext(configuration);

            var persistedGrantConnectionString = configuration.GetConnectionString("PersistedGrantConnection");
            Log.Information("PersistedGrant connection string configured: {HasConnectionString}",
                !string.IsNullOrEmpty(persistedGrantConnectionString));
            var migrationsAssembly = typeof(Program).GetTypeInfo().Assembly.GetName().Name;

            var serverOptions = configuration.GetSection(nameof(VikingIdentityServerOptions)).Get<VikingIdentityServerOptions>();

            // Add detailed logging for configuration
            Log.Information("=== IDENTITYSERVER CONFIGURATION DEBUG ===");
            Log.Information("serverOptions is null: {IsNull}", serverOptions == null);

            if (serverOptions != null)
            {
                Log.Information("Authority: {Authority}", serverOptions.Authority);
                Log.Information("Secret configured: {HasSecret}", !string.IsNullOrEmpty(serverOptions.Secret));
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
              
            services.AddIdentity<ApplicationUser, ApplicationRole>(config =>
                {
                    config.SignIn.RequireConfirmedEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.ConfigureSharedApplicationCookie();
            services.AddTransient<Microsoft.AspNetCore.Authentication.IClaimsTransformation, MapNameIdentifierToSubClaimsTransformation>();

            string licenseKey = null;
            var hostingEnv = configuration.GetValue<string>("HOSTING_ENVIRONMENT") ?? "Local";
            if (hostingEnv == "Local")
            { 
                var licenseKeyPath = configuration.GetValue<string>("DUENDE_KEY_PATH");
                if (licenseKeyPath != null)
                { 
                    try{
                        licenseKey = File.ReadAllText(licenseKeyPath);
                    }
                    catch (IOException ex)
                    {
                        Log.Error(ex, "Failed to read license key from file. {LicenseKeyPath}", licenseKeyPath);
                    }
                }
            }
             
            var builder = services.AddIdentityServer(options =>
            {                
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                // KeyManagement is disabled to prevent automatic key rotation issues
                // When disabled, you must ensure signing credentials are stable across restarts
                // options.KeyManagement.Enabled = false; // Commented out - will use default behavior or proper operational store
                options.EmitStaticAudienceClaim = true;
                options.LicenseKey = licenseKey;
                options.UserInteraction.AllowOriginInReturnUrl = true;
                options.UserInteraction.ErrorUrl = "/Home/Error";
                options.Authentication.CookieAuthenticationScheme = IdentityConstants.ApplicationScheme;
                 
                if (serverOptions != null)
                {
                    options.IssuerUri = serverOptions.Authority;
                    options.UserInteraction.LoginUrl = serverOptions.GetManagementLoginUrl();
                    Log.Information("Set IdentityServer IssuerUri to: {IssuerUri}", options.IssuerUri);
                    Log.Information("Set IdentityServer LoginUrl to: {LoginUrl}", options.UserInteraction.LoginUrl);
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
                .AddExtensionGrantValidator<VikingUserTokenGrantValidator>()
                .AddDefaultEndpoints();

            services.AddTransient<Duende.IdentityServer.Validation.ICustomTokenRequestValidator, Viking.Identity.Server.WebManagement.Extensions.UserScopeTokenRequestValidator>();

            Log.Information("=== END IDENTITYSERVER CONFIGURATION DEBUG ===");

            // Configure SSL certificate and signing credentials
            ConfigureSigningCredentials(builder, sslCert);
        }

        /// <summary>
        /// Developer signing credentials (tempkey.jwk) are allowed only for local Development-style
        /// hosts. Docker and Production must have a usable SSL signing certificate.
        /// </summary>
        private static bool AllowDeveloperSigningCredential()
        {
            var hostingEnv = Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") ?? "Local";
            if (string.Equals(hostingEnv, "Docker", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var aspnetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            return string.Equals(aspnetCoreEnv, "Development", StringComparison.OrdinalIgnoreCase)
                || string.Equals(aspnetCoreEnv, "DevelopmentTest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(aspnetCoreEnv, "Local", StringComparison.OrdinalIgnoreCase);
        }

        private static void ConfigureSigningCredentials(IIdentityServerBuilder builder, X509Certificate2 sslCert)
        {
            if (TryAddCertificateSigningCredential(builder, sslCert))
            {
                return;
            }

            if (AllowDeveloperSigningCredential())
            {
                Log.Warning("No usable signing certificate. Using developer signing credential (Development only).");
                builder.AddDeveloperSigningCredential();
                return;
            }

            throw new InvalidOperationException(
                "IdentityServer signing certificate is missing or unusable. Refusing to start with developer signing credentials outside local Development.");
        }

        /// <returns>True when the certificate was installed as the signing credential.</returns>
        private static bool TryAddCertificateSigningCredential(IIdentityServerBuilder builder, X509Certificate2 sslCert)
        {
            if (sslCert == null)
            {
                Log.Warning("No valid certificate found for IdentityServer signing.");
                return false;
            }

            if (!sslCert.HasPrivateKey)
            {
                Log.Warning("Certificate does not have a private key. Cannot use for signing. Subject: {Subject}, Thumbprint: {Thumbprint}.",
                    sslCert.Subject, sslCert.Thumbprint);
                return false;
            }

            try
            {
                using (var rsa = sslCert.GetRSAPrivateKey())
                {
                    if (rsa == null)
                    {
                        Log.Warning("Certificate private key is not accessible or not RSA. Subject: {Subject}, Thumbprint: {Thumbprint}.",
                            sslCert.Subject, sslCert.Thumbprint);
                        return false;
                    }
                }

                builder.AddSigningCredential(sslCert);
                Log.Information("Successfully configured IdentityServer with certificate signing credential. Subject: {Subject}, Thumbprint: {Thumbprint}, HasPrivateKey: {HasPrivateKey}",
                    sslCert.Subject, sslCert.Thumbprint, sslCert.HasPrivateKey);
                return true;
            }
            catch (CryptographicException ex)
            {
                Log.Error(ex, "Failed to configure IdentityServer signing credentials with certificate due to cryptographic error.");
                return false;
            }
            catch (ArgumentException ex)
            {
                Log.Error(ex, "Failed to configure IdentityServer signing credentials with certificate due to invalid certificate format.");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure IdentityServer signing credentials with certificate due to unexpected error.");
                return false;
            }
        }

        private static void ConfigureEmailServices(IServiceCollection services, IConfiguration configuration)
        {
            // Configure email options
            services.Configure<EmailOptions>(configuration.GetSection("Email"));

            // Register EmailSender for Identity email functionality
            services.AddTransient<IEmailSender<ApplicationUser>, EmailSender>();
        }

        private static void Configure(WebApplication app, IWebHostEnvironment env)
        {
            Log.Information("Starting IdentityServer configuration...");

            // this will do the initial DB population and required migrations
            InitializeDatabase(app);

            var aspnetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? env.EnvironmentName;
            if (env.IsDevelopment()
                || string.Equals(aspnetCoreEnv, "Local", StringComparison.OrdinalIgnoreCase))
            {
                Log.Information("Using Developer Exception Pages...");
                app.UseDeveloperExceptionPage();
            }
            else
            {
                Log.Information("Using Error page for exceptions...");
                app.UseExceptionHandler("/Home/Error");
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

            // Protocol UI (Logout / Error). Do not put these under MapRazorPages().RequireAuthorization().
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Modern top-level route registrations (replaces UseEndpoints)
            app.MapRazorPages().RequireAuthorization();
            
            // Default homepage
            app.MapGet("/", async context =>
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
