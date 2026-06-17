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

            var aspnetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
            var hostingEnv = Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") ?? "Local";
            
            var buildEnvFile = $".env.{aspnetCoreEnv}";
            Env.TraversePath().Load(buildEnvFile);
            
            var hostingEnvFile = $".env.{hostingEnv}";
            Env.TraversePath().Load(hostingEnvFile);

            // Configure Serilog
            var logPath = Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") == "Docker" 
                ? "/var/log/supervisor/identity-server/IdentityManagerLogs.json"
                : "IdentityManagerLogs.json";
            
            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File(logPath, Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                Log.Information("Starting IdentityManagementWebsite...");
                var builder = WebApplication.CreateBuilder(args);

                // Enable environment variable substitution in the main configuration
                builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                                     .AddJsonFile($"appsettings.{aspnetCoreEnv}.json", optional: true)
                                     .AddJsonFile($"appsettings.{hostingEnv}.json", optional: true)
                                     .AddUserSecrets<Program>(optional: true)
                                     .AddEnvironmentVariables()
                                     .EnableSubstitutions("${", "}", UnresolvedVariableBehaviour.Throw)
                                     .AddJsonFile("secrets.json", optional: true, reloadOnChange: false); // Load secrets.json last to override all other configuration

                // Configure Serilog
                var managementLogPath = Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") == "Docker" 
                    ? "/var/log/supervisor/identity-server/IdentityServerManagement.json"
                    : "IdentityServerManagement.json";
                
                builder.Host.UseSerilog((context, services, configuration) => configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext() 
                    .WriteTo.Console() 
                    .WriteTo.File(managementLogPath, Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
                );

                // Configure services
                ConfigureServices(builder.Services, builder.Configuration);
                Log.Information("=== Configure Services Complete ===");
                // Configure Kestrel
                ConfigureKestrel(builder.WebHost, builder.Configuration);
                Log.Information("=== Configure Kestrel Complete ===");
                var app = builder.Build();

                // Configure the HTTP request pipeline
                Configure(app, builder.Environment);
                Log.Information("=== Configure Pipeline Complete ===");

                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.Information("Web Management exit");
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Configure Identity Server Data Context (must be first for database dependencies)
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
            Console.WriteLine(" Loading OAuth2IntrospectionOptions configuration...");
            var OAuth2ConfigurationSection = configuration.GetSection(nameof(OAuth2IntrospectionOptions));
            if (OAuth2ConfigurationSection is null)
            {
                Console.WriteLine(" ERROR: OAuth2IntrospectionOptions section missing from configuration");
                throw new ArgumentException(
                    $"{nameof(OAuth2IntrospectionOptions)} section missing from appsettings.json configuration");
            }

            Console.WriteLine(" OAuth2IntrospectionOptions section found, deserializing...");
            OAuth2IntrospectionOptions OAuth2Options = OAuth2ConfigurationSection.Get<OAuth2IntrospectionOptions>();
            
            if (OAuth2Options == null)
            {
                Console.WriteLine(" ERROR: OAuth2IntrospectionOptions deserialization returned null");
                Console.WriteLine(" Configuration section exists but failed to deserialize");
                throw new InvalidOperationException("Failed to deserialize OAuth2IntrospectionOptions from configuration");
            }
             
            Console.WriteLine(" Validating OAuth2IntrospectionOptions...");
            try
            {
                OAuth2Options.Validate();
                Console.WriteLine(" OAuth2IntrospectionOptions validation passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ERROR: OAuth2IntrospectionOptions validation failed: {ex.Message}");
                Console.WriteLine($" Exception type: {ex.GetType().Name}");
                Console.WriteLine($" Stack trace: {ex.StackTrace}");
                throw;
            }

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
            var https_port = configuration.GetValue<int>("IDENTITY_MANAGEMENT_CONTAINER_HTTPS_PORT", 443); 
            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = https_port;
            });

            // Configure Identity (after database context)
            services.AddIdentity<ApplicationUser, ApplicationRole>(config =>
             { 
                 config.SignIn.RequireConfirmedEmail = true;  
             })
                .AddEntityFrameworkStores<ApplicationDbContext>() 
                .AddDefaultTokenProviders();

            // Add application services
            services.AddTransient<IEmailSender, EmailSender>();
            services.AddTransient<IPermissionsViewModelHelper, PermissionsViewModelHelper>();

            // Add HTTP context accessor
            services.AddHttpContextAccessor();

            // Add profile service
            services.AddTransient<Duende.IdentityServer.Services.IProfileService, IdentityWithExtendedClaimsProfileService>();

            // Add authorization policy evaluator
            services.AddAuthorizationPolicyEvaluator();

            // Configure authorization policies (after authentication services)
            services.AddAuthorization(options =>
            {
                var builder = new AuthorizationPolicyBuilder();
                builder.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                builder.RequireAuthenticatedUser();
                options.AddPolicy("protectedScope", policy => policy.RequireClaim("scope", "Viking.Annotation"));
                options.AddPolicy(Policy.BearerToken, builder.Build());
                options.AddPolicy(Policy.GroupAccessManager, policy => policy.Requirements.Add(Authorization.Operations.GroupAccessManager));
                options.AddPolicy(Policy.OrgUnitAdmin, policy => policy.Requirements.Add(Authorization.Operations.OrgUnitAdmin));
            });

            // Add CORS policy service (after other services are configured)
            services.AddSingleton<ICorsPolicyService>((container) => {
                var logger = container.GetRequiredService<ILogger<DefaultCorsPolicyService>>();
                return new DefaultCorsPolicyService(logger) {
                    AllowAll = true
                };
            });

            // Configure Email options
            services.Configure<Viking.Identity.Server.Services.EmailOptions>(configuration.GetSection("Email"));

            // Configure access token management
            services.AddAccessTokenManagement(options =>
            {
                // client config is inferred from OpenID Connect settings
            });

            // Configure HttpClient for calling the WebAPI
            services.AddHttpClient("IdentityApi", client =>
            {
                var webApiOptions = configuration.GetSection(nameof(WebApiOptions)).Get<WebApiOptions>();
                client.BaseAddress = new Uri(webApiOptions.BaseUrl);
            });
        }

        private static void ConfigureKestrel(IWebHostBuilder webHostBuilder, IConfiguration configuration)
        {
            webHostBuilder.ConfigureKestrel(options =>
            {
                // Configure HTTPS with custom certificate
                var sslOptions = configuration.GetSection("SSL").Get<SSLOptions>();
                var http_port = configuration.GetValue<int>("IDENTITY_MANAGEMENT_CONTAINER_HTTP_PORT", 80);
                var https_port = configuration.GetValue<int>("IDENTITY_MANAGEMENT_CONTAINER_HTTPS_PORT", 443);

                Log.Information("Configuring Kestrel to listen on HTTP port {HttpPort} and HTTPS port {HttpsPort}", http_port, https_port);

                options.ListenAnyIP(http_port); // HTTP port
                options.ListenAnyIP(https_port, listenOptions => // HTTPS port
                {
                    var sslCert = Certs.LoadSSLCertificate(sslOptions);
                    if (sslCert != null)
                    {
                        try
                        {
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
            Console.WriteLine(" Starting IdentityManagementWebsite configuration...");
            Log.Information("Starting IdentityManagementWebsite configuration...");

            // Initialize database
            Console.WriteLine(" About to initialize database...");
            InitializeDatabase(app);
            Console.WriteLine(" Database initialization complete");

            Console.WriteLine(" Checking environment...");
            if (env.IsDevelopment())
            {
                Console.WriteLine(" Using Development environment");
                Log.Information("Using Developer Exception Pages...");
                app.UseDeveloperExceptionPage();
                // Note: UseDatabaseErrorPage is obsolete in .NET 9, using DatabaseDeveloperPageExceptionFilter instead
                // app.UseDatabaseErrorPage(); // Removed obsolete method
            }
            else
            {
                Console.WriteLine(" Using Production environment");
                Log.Information("Using Error page for exceptions...");
                app.UseExceptionHandler("/Home/Error");
            }

            Console.WriteLine(" Configuring middleware...");
            app.UseSerilogRequestLogging();
            Console.WriteLine(" Serilog request logging configured");

            app.UseHttpsRedirection();
            Console.WriteLine(" HTTPS redirection configured");
            
            app.UseStaticFiles();
            Console.WriteLine(" Static files configured");

            app.UseRouting();
            Console.WriteLine(" Routing configured");
            
            app.UseAuthentication();
            Console.WriteLine(" Authentication configured");
            
            app.UseAuthorization();
            Console.WriteLine(" Authorization configured");

            // Modern top-level route registrations (replaces UseEndpoints)
            // We cannot require authorization on all routes or users are unable to login
            Console.WriteLine(" Mapping routes...");
            app.MapDefaultControllerRoute();
            Console.WriteLine(" Default controller route mapped");
            
            app.MapControllers();
            Console.WriteLine(" Controllers mapped");
            Console.WriteLine(" === Configure Pipeline Complete ===");
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
                
