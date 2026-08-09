using System;
using System.Linq;
using Duende.AspNetCore.Authentication.OAuth2Introspection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace gRPCAnnotationService
{
    /// <summary>
    /// Identity Server issues both JWTs and opaque reference tokens. A JWT always
    /// contains dots separating its three segments; a reference token never does,
    /// and has to be sent back to the introspection endpoint to be resolved.
    /// </summary>
    public static class PolicySchemeSelector
    {
        public const string IntrospectionScheme = "Introspection";

        public static string SchemeSelector(HttpContext context)
        {
            var (scheme, token) = GetSchemeAndCredential(context);

            if (!string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                return null;

            return token.Contains('.') ? JwtBearerDefaults.AuthenticationScheme : IntrospectionScheme;
        }

        /// <summary>Splits the Authorization header into its scheme and credential.</summary>
        public static (string Scheme, string Credential) GetSchemeAndCredential(HttpContext context)
        {
            var header = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(header))
                return ("", "");

            var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length != 2 ? ("", "") : (parts[0], parts[1]);
        }
    }

    public class Startup
    {
        /// <summary>Scope a caller must hold to reach any annotation service.</summary>
        public const string AnnotationScope = "Viking.Annotation";

        private const string ProtectedScopePolicy = "protectedScope";

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<Viking.DataModel.Annotation.AnnotationContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("AnnotationConnection"),
                                     sql => sql.UseNetTopologySuite())
                       .EnableDetailedErrors()
                       .EnableSensitiveDataLogging());

            services.AddHttpContextAccessor();

            services.AddGrpc(options =>
            {
#if DEBUG
                options.EnableDetailedErrors = true;
#endif
            });

            var identityServer = Configuration.GetSection("IdentityServer");
            var authority = identityServer["Endpoint"];
            if (string.IsNullOrWhiteSpace(authority))
                throw new InvalidOperationException("IdentityServer:Endpoint is not configured.");

            services.AddAuthorization(options =>
                options.AddPolicy(ProtectedScopePolicy,
                    policy => policy.RequireClaim("scope", AnnotationScope)));

            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddOAuth2Introspection(PolicySchemeSelector.IntrospectionScheme, options =>
                {
                    options.Authority = authority;
                    options.ClientId = identityServer["ClientId"];
                    options.ClientSecret = identityServer["ClientSecret"];
                    // Caching is on by default (backed by HybridCache) as of Duende.AspNetCore.Authentication.OAuth2Introspection 7.x;
                    // see options.SetCacheEntryFlags to tune or disable it.
                    options.SaveToken = true;

                    // Local DevTest Identity is HTTP-only. Set the introspection endpoint
                    // explicitly so Duende does not require HTTPS discovery metadata.
                    var introspectionEndpoint = identityServer["IntrospectionEndpoint"];
                    if (string.IsNullOrWhiteSpace(introspectionEndpoint) &&
                        string.Equals(identityServer["AllowHttpMetadata"], "true", StringComparison.OrdinalIgnoreCase))
                    {
                        introspectionEndpoint = authority.TrimEnd('/') + "/connect/introspect";
                    }

                    if (!string.IsNullOrWhiteSpace(introspectionEndpoint))
                        options.IntrospectionEndpoint = introspectionEndpoint;
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority = authority;
                    options.RequireHttpsMetadata = !string.Equals(identityServer["AllowHttpMetadata"], "true",
                        StringComparison.OrdinalIgnoreCase);
                    options.SaveToken = true;
                    options.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuer = true,
                        ValidateIssuerSigningKey = true,
                        ValidateTokenReplay = true,
                        ValidAudience = AnnotationScope,
                        NameClaimType = "name",
                        ValidTypes = new[] { "at+jwt" }
                    };
                });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            // Skip HTTPS redirect when the service is deliberately HTTP (Docker test stack).
            var allowHttp = string.Equals(Configuration["IdentityServer:AllowHttpMetadata"], "true",
                StringComparison.OrdinalIgnoreCase);
            if (!allowHttp)
                app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<LocationService>().RequireAuthorization(ProtectedScopePolicy);
                endpoints.MapGrpcService<StructureService>().RequireAuthorization(ProtectedScopePolicy);
                endpoints.MapGrpcService<StructureTypeService>().RequireAuthorization(ProtectedScopePolicy);
                endpoints.MapGrpcService<PermittedStructureLinksService>().RequireAuthorization(ProtectedScopePolicy);
                endpoints.MapGrpcService<MetaDataService>().RequireAuthorization(ProtectedScopePolicy);

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync(
                        "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });
        }
    }
}
