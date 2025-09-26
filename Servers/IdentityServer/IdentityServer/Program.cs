using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IdentityModel;
using IdentityModel.AspNetCore.OAuth2Introspection;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.WebManagement.Extensions;
using Viking.Identity.Server.Services;
using Viking.SSL;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.IdentityModel.Logging;
using Microsoft.AspNetCore.Hosting;
using Viking.Identity;
using DotNetEnv;
using ConfigurationSubstitution;

namespace Viking.Identity.Server.WebManagement
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IdentityModelEventSource.ShowPII = true; // Enable detailed error messages for development

            var envFile = ".env";
            Env.TraversePath().Load(envFile);

            var buildEnvFile = $".env.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}";
            Env.TraversePath().Load(buildEnvFile);

            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("IdentityManagerLogs.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Starting IdentityManagementWebsite...");
                var builder = WebApplication.CreateBuilder(args);

                // Configure Serilog
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
#if DEBUG
                    .WriteTo.Console()
#endif
                    .WriteTo.File("IdentityServerManagement.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                );

                // Configure services
                ConfigureServices(builder.Services, builder.Configuration);

                // Configure Kestrel
                ConfigureKestrel(builder.WebHost);

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

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Add CORS policy service
            services.AddSingleton<ICorsPolicyService>((container) => {
                var logger = container.GetRequiredService<ILogger<DefaultCorsPolicyService>>();
                return new DefaultCorsPolicyService(logger) {
                    AllowAll = true
                };
            });

            // Configure Identity Server Data Context
            services.ConfigureIdentityServerDataContext(configuration);

            // Add controllers and views
            services.AddControllers();
            services.AddControllersWithViews();

            var migrationsAssembly = typeof(Program).GetTypeInfo().Assembly.GetName().Name;
            var persistedGrantConnectionString = configuration.GetConnectionString("PersistedGrantConnection");

            // Add logging
            services.AddLogging(loggingBuilder =>
                loggingBuilder.AddConsole()
            );

            // Add antiforgery
            services.AddAntiforgery();

            // Configure JWT Bearer options
            JwtBearerOptions jwtOptions = configuration.GetSection(nameof(JwtBearerOptions)).Get<JwtBearerOptions>();
            services.Configure<JwtBearerOptions>(configuration.GetSection(nameof(JwtBearerOptions)));

            // Configure Viking Identity Server options
            var serverOptions = configuration.GetSection(nameof(VikingIdentityServerOptions)).Get<VikingIdentityServerOptions>();
            services.Configure<VikingIdentityServerOptions>(
                configuration.GetSection(nameof(VikingIdentityServerOptions)));

            // Configure OAuth2 Introspection options
            var OAuth2ConfigurationSection = configuration.GetSection(nameof(OAuth2IntrospectionOptions));
            if (OAuth2ConfigurationSection is null)
                throw new ArgumentException(
                    $"{nameof(OAuth2IntrospectionOptions)} section missing from appsettings.json configuration");

            OAuth2IntrospectionOptions OAuth2Options = OAuth2ConfigurationSection.Get<OAuth2IntrospectionOptions>();
            OAuth2Options.Validate();

            // Configure authentication
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddCookie()
                .AddOAuth2Introspection("Introspection", options =>
                {
                    OAuth2ConfigurationSection.Bind(options);
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    configuration.Bind(nameof(JwtBearerOptions), options);
                    options.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;
                });

            // Add custom token request validator
            services.AddTransient<Duende.IdentityServer.Validation.ICustomTokenRequestValidator, UserScopeTokenRequestValidator>();

            // Add authorization handlers
            services.AddScoped<IAuthorizationHandler, ResourceIdPermissionsAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, ResourcePermissionsAuthorizationHandler>();

            // Configure SSL and HTTPS redirection
            var sslOptions = configuration.GetSection("SSL").Get<Viking.SSL.SSLOptions>();
            var https_port = configuration.GetValue<int>("https_port", 443);

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = https_port;
            });

            // Configure Identity
            services.AddIdentity<ApplicationUser, ApplicationRole>(config =>
             { 
                 config.SignIn.RequireConfirmedEmail = true;  
             })
                .AddEntityFrameworkStores<ApplicationDbContext>() 
                .AddDefaultTokenProviders();

            // Add application services
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IPermissionsViewModelHelper, PermissionsViewModelHelper>();

            // Add HTTP context accessor (commented out in original but commonly needed)
            services.AddHttpContextAccessor();

            // Add profile service
            services.AddTransient<Duende.IdentityServer.Services.IProfileService, IdentityWithExtendedClaimsProfileService>();

            // Add authorization policy evaluator
            services.AddAuthorizationPolicyEvaluator();

            // Configure authorization policies
            services.AddAuthorization(options =>
            {
                var builder = new AuthorizationPolicyBuilder();
                builder.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                builder.RequireAuthenticatedUser();
                options.AddPolicy("protectedScope", policy => policy.RequireClaim("scope", "Viking.Annotation"));
                options.AddPolicy(Config.Policy.BearerToken, builder.Build());
                options.AddPolicy(Config.Policy.GroupAccessManager, policy => policy.Requirements.Add(Authorization.Operations.GroupAccessManager));
                options.AddPolicy(Config.Policy.OrgUnitAdmin, policy => policy.Requirements.Add(Authorization.Operations.OrgUnitAdmin));
            });

            // Configure SMTP options
            services.Configure<SMTPOptions>(configuration.GetSection("SMTP"));

            // Configure access token management
            services.AddAccessTokenManagement(options =>
            {
                // client config is inferred from OpenID Connect settings
            });
        }

        private static void ConfigureKestrel(IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureKestrel(options =>
            {
                // Configure HTTPS with custom certificate
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true)
                    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
                    .AddEnvironmentVariables()
                    .EnableSubstitutions()
                    .Build();

                var sslOptions = configuration.GetSection("SSL").Get<SSLOptions>();
                var https_port = configuration.GetValue<int>("https_port", 443);

                options.ListenAnyIP(80); // HTTP port
                options.ListenAnyIP(https_port, listenOptions => // HTTPS port
                {
                    var sslCert = Certs.LoadSSLCertificate(sslOptions);
                    if (sslCert != null)
                    {
                        try
                        {
                            Log.Information("Certificate found");
                            listenOptions.UseHttps(sslCert);
                            Log.Information("Successfully configured Kestrel HTTPS with certificate: {Subject}", sslCert.Subject);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to configure Kestrel HTTPS with certificate: {Subject}", sslCert.Subject);
                        }
                    }
                    else
                    {
                        Log.Warning("SSL certificate not found");
                    }
                });
            });
        }

        private static void Configure(WebApplication app, IWebHostEnvironment env)
        {
            Log.Information("Starting IdentityManagementWebsite configuration...");

            // Initialize database
            InitializeDatabase(app);

            if (env.IsDevelopment())
            {
                Log.Information("Using Developer Exception Pages...");
                app.UseDeveloperExceptionPage();
                // Note: UseDatabaseErrorPage is obsolete in .NET 9, using DatabaseDeveloperPageExceptionFilter instead
                // app.UseDatabaseErrorPage(); // Removed obsolete method
            }
            else
            {
                Log.Information("Using Error page for exceptions...");
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            // Modern top-level route registrations (replaces UseEndpoints)
            // We cannot require authorization on all routes or users are unable to login
            app.MapDefaultControllerRoute();
            app.MapControllers();
        }

        private static IApplicationBuilder InitializeDatabase(IApplicationBuilder app)
        {
            // Database initialization logic can be added here if needed
            // Currently commented out in the original Startup.cs
            return app;
        }
    }

    public static class PolicySchemeSelector
    {
        public static string SchemeSelector(HttpContext context)
        {
            var (scheme, token) = GetSchemeAndCredential(context);

            if (!string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            if (token.Contains("."))
            {
                return "Bearer";
            }
            else
            {
                return "Introspection";
            }
        }

        /// <summary>
        /// Extracts scheme and credential from Authorization header (if present)
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static (string, string) GetSchemeAndCredential(HttpContext context)
        {
            var header = context.Request.Headers["Authorization"].FirstOrDefault();

            if (string.IsNullOrEmpty(header))
            {
                return ("", "");
            }

            var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                return ("", "");
            }

            return (parts[0], parts[1]);
        }
    }
}
                
