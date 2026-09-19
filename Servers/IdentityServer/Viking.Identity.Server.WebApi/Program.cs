using ConfigurationSubstitution;
using DotNetEnv; 
using Duende.IdentityModel.Client;
using Duende.IdentityServer.Services;
using IdentityModel.AspNetCore.OAuth2Introspection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.Services;
using Viking.Identity.Server.WebManagement.Extensions;
using Viking.SSL;
using Viking.Identity;

public class Program
{
    /// <summary>
    /// Named auth scheme for opaque reference-token introspection.
    /// Must not be "Bearer" — IdentityModel's default is Bearer and would collide with JWT.
    /// </summary>
    public const string IntrospectionScheme = "Introspection";

    public static void Main(string[] args)
    {
        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            ///This log file is used by the Docker container to log to a volume mapped to the host
            //.WriteTo.File("Viking.Identity.Server.WebApi.json", Serilog.Events.LogEventLevel.Verbose, rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var envFile = ".env";
        Env.TraversePath().Load(envFile);

        var aspnetCoreEnv = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var hostingEnv = Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT") ?? "Local";
        
        var buildEnvFile = $".env.{aspnetCoreEnv}";
        Env.TraversePath().Load(buildEnvFile);
        
        var hostingEnvFile = $".env.{hostingEnv}";
        Env.TraversePath().Load(hostingEnvFile);
 
        try
        {
            Log.Information("Starting Viking Identity Server WebApi");

            var builder = WebApplication.CreateBuilder(args);

            // Enable environment variable substitution in the main configuration
            builder.Configuration.AddJsonFile("appsettings.json", optional: true)
                               .AddJsonFile($"appsettings.{aspnetCoreEnv}.json", optional: true)
                               .AddJsonFile($"appsettings.{hostingEnv}.json", optional: true)
                               .AddEnvironmentVariables()
                               .AddUserSecrets<Program>(optional: true)
                               .EnableSubstitutions("${", "}", UnresolvedVariableBehaviour.Throw);


            // Configure Serilog
            builder.Host.UseSerilog();

            // Load SSL certificate configuration
            var sslOptions = builder.Configuration.GetSection("SSL").Get<SSLOptions>();
            var sslCert = Certs.LoadSSLCertificate(sslOptions);
             

            var http_port = builder.Configuration.GetValue<int>("IDENTITY_WEBAPI_CONTAINER_HTTP_PORT");
            var https_port = builder.Configuration.GetValue<int>("IDENTITY_WEBAPI_CONTAINER_HTTPS_PORT");
            
            Log.Information("DEBUG: http_port = {HttpPort}, https_port = {HttpsPort}", http_port, https_port);
            Log.Information("DEBUG: Environment variables - IDENTITY_WEBAPI_HTTP_PORT = {HttpPortEnv}, IDENTITY_WEBAPI_HTTPS_PORT = {HttpsPortEnv}", 
                Environment.GetEnvironmentVariable("IDENTITY_WEBAPI_HTTP_PORT"), 
                Environment.GetEnvironmentVariable("IDENTITY_WEBAPI_HTTPS_PORT"));


            // Configure Kestrel with SSL
            builder.WebHost.ConfigureKestrel(options =>
            {
                
                // Configure HTTP and HTTPS endpoints
                options.ListenAnyIP(http_port); // HTTP port
                options.ListenAnyIP(https_port, listenOptions => // HTTPS port
                {
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

            // Add services to the container
            builder.Services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true).AddConsole()
            );

            builder.Services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = https_port;
            });

            builder.Services.AddTransient<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();

            builder.Services.AddSingleton<ICorsPolicyService>((container) =>
            {
                var logger = container.GetRequiredService<ILogger<DefaultCorsPolicyService>>();
                return new DefaultCorsPolicyService(logger)
                {
                    //AllowedOrigins = { "https://websvc1.connectomes.utah.edu", "https://bar" }
                    AllowAll = true
                };
            });

            builder.Services.AddRazorPages();

            // Store the certificate in services for potential use by other components
            if (sslCert != null)
            {
                builder.Services.AddSingleton(sslCert);
                Log.Information("SSL certificate loaded and registered for WebApi. Subject: {Subject}, Thumbprint: {Thumbprint}",
                    sslCert.Subject, sslCert.Thumbprint);
            }
            else
            {
                Log.Warning("No SSL certificate loaded for WebApi.");
            }

            // Configure Identity Server Data Context
            builder.Services.ConfigureIdentityServerDataContext(builder.Configuration);

            builder.Services.Configure<VikingIdentityServerOptions>(
                builder.Configuration.GetSection(nameof(VikingIdentityServerOptions)));

            var vikingConfig = builder.Configuration.GetSection("VikingIdentityServerOptions").Get<VikingIdentityServerOptions>();

            // Introspection authenticates as an ApiResource (name + ApiSecret), not as the OAuth "api" client.
            // Duende returns active:false when the caller is not an API that owns the token's scopes.
            builder.Services.AddMemoryCache();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddOAuth2Introspection(IntrospectionScheme, options =>
                {
                    options.Authority = vikingConfig?.Authority;
                    options.ClientId = IdentityApiResources.PermissionsApiResourceName;
                    options.ClientSecret = vikingConfig?.GetClientSecret("api");
                    options.ClientCredentialStyle = IdentityModel.Client.ClientCredentialStyle.AuthorizationHeader;
                    options.EnableCaching = true;
                    options.CacheDuration = TimeSpan.FromMinutes(5);
                    options.NameClaimType = "name";
                    options.RoleClaimType = "role";
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    builder.Configuration.Bind(nameof(JwtBearerOptions), options);

                    if (!string.IsNullOrWhiteSpace(vikingConfig?.Authority))
                        options.Authority = vikingConfig.Authority;

                    // Tokens issued to the Viking client carry the scope name as their audience,
                    // not the API resource name bound from configuration.
                    options.TokenValidationParameters.ValidAudiences =
                        new[] { IdentityApiResources.PermissionsApiResourceName, "Viking.Annotation.API" };
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.TokenValidationParameters.RoleClaimType = "role";

                    options.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;
                });

            // Configure Authorization — require bearer token on all endpoints unless [AllowAnonymous]
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, IntrospectionScheme)
                    .RequireAuthenticatedUser()
                    .Build();
            });

            builder.Services.AddScoped<IAuthorizationHandler, ResourceIdPermissionsAuthorizationHandler>();
            builder.Services.AddScoped<IAuthorizationHandler, ResourcePermissionsAuthorizationHandler>();

            // API has no interactive cookie UI — IdentityCore avoids registering cookie auth schemes.
            builder.Services.AddIdentityCore<ApplicationUser>(config =>
                {
                    config.SignIn.RequireConfirmedEmail = true;
                })
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddTransient<IEmailSender, EmailSender>();

            builder.Services.AddScoped<Viking.Identity.Server.Extensions.Services.IPermissionService, Viking.Identity.Server.Extensions.Services.PermissionService>();
            builder.Services.AddScoped<Viking.Identity.Server.Extensions.Services.IAuthenticationService, Viking.Identity.Server.Extensions.Services.AuthenticationService>();
            builder.Services.AddScoped<Viking.Identity.Server.Extensions.Services.VikingLaunchCodeService>();
            builder.Services.Configure<DebugLoggingOptions>(builder.Configuration.GetSection("DebugLogging"));
            builder.Services.AddSingleton<IDebugLoggingService, DebugLoggingService>();

            builder.Services.AddHttpClient();

            builder.Services.AddHealthChecks();

            // Configure Swagger/OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Viking.Identity.Server.WebApi", Version = "v1" });
            });

            builder.Services.AddControllers();

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Viking.Identity.Server.WebApi v1"));
            }

            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapHealthChecks("/health").AllowAnonymous();
            app.MapControllers();
            app.MapRazorPages();
            app.MapDefaultControllerRoute();

            // Log all registered endpoints
            var endpointDataSource = app.Services.GetRequiredService<EndpointDataSource>();
            Log.Information("=== REGISTERED ENDPOINTS ===");
            foreach (var endpoint in endpointDataSource.Endpoints)
            {
                var routeEndpoint = endpoint as RouteEndpoint;
                if (routeEndpoint != null)
                {
                    var httpMethods = routeEndpoint.Metadata.GetOrderedMetadata<HttpMethodMetadata>();
                    var methodNames = httpMethods.SelectMany(m => m.HttpMethods).ToArray();
                    Log.Information("Endpoint: {Method} {Pattern} -> {DisplayName}",
                        string.Join(", ", methodNames),
                        routeEndpoint.RoutePattern.RawText ?? routeEndpoint.RoutePattern.ToString(),
                        endpoint.DisplayName);
                }
            }
            Log.Information("=== END ENDPOINT LIST ===");

            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

/// <summary>
/// Routes an incoming bearer token to the handler that can validate it.
/// Reference tokens are opaque; JWTs always contain dots.
/// </summary>
public static class PolicySchemeSelector
{
    public static string SchemeSelector(HttpContext context)
    {
        var (scheme, token) = GetSchemeAndCredential(context);

        if (!string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            return JwtBearerDefaults.AuthenticationScheme;

        return token.Contains('.')
            ? JwtBearerDefaults.AuthenticationScheme
            : Program.IntrospectionScheme;
    }

    private static (string scheme, string credential) GetSchemeAndCredential(HttpContext context)
    {
        var header = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(header))
            return ("", "");

        var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length != 2 ? ("", "") : (parts[0], parts[1]);
    }
}