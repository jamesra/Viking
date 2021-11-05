using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using IdentityModel.AspNetCore.AccessTokenValidation;
using IdentityModel.Client;
using MathNet.Numerics;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

    public static class PolicySchemeSelector
    {
        public static string SchemeSelector(HttpContext context)
        {
            var (scheme, token) = GetSchemeAndCredential(context);

            if (!string.Equals(scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
            {
                return null;
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

    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;

            /*Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.File("IDServerLogs.json", Serilog.Events.LogEventLevel.Verbose)
              .CreateLogger();*/
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var connectionString = Configuration.GetConnectionString("AnnotationConnection");

            services.AddDbContext<Viking.DataModel.Annotation.AnnotationContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("AnnotationConnection"),
                                     options => options.UseNetTopologySuite())
                       .EnableDetailedErrors()
                       .EnableSensitiveDataLogging());

            services.AddHttpContextAccessor();

            services.AddGrpc(options =>
            {
#if DEBUG
                options.EnableDetailedErrors = true;
#endif
            });

            IConfigurationSection identityServerConfig = Configuration.GetSection("IdentityServer");
            string endpoint = identityServerConfig["Endpoint"];


            services.AddAuthorization(options =>
            {
                /*var builder = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme);
                    .RequireAuthenticatedUser();
                options.DefaultPolicy = builder.Build();*/
                options.AddPolicy("protectedScope", policy => policy.RequireClaim("scope", "Viking.Annotation"));
                //options.AddPolicy(Config.Policy.GroupAccessManager, policy => policy.Requirements.Add(Authorization.Operations.GroupAccessManager));
                //options.AddPolicy(Config.Policy.OrgUnitAdmin, policy => policy.Requirements.Add(Authorization.Operations.OrgUnitAdmin));
            });
             
            //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            services.AddAuthentication(options =>
                {
                    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                /*
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.Events.OnSigningOut = async e => { await e.HttpContext.RevokeUserRefreshTokenAsync(); };
                })*/
                /*.AddOpenIdConnect("oidc", options =>
                {
                    options.Authority = identityServerConfig["Endpoint"];
                    options.ClientId = "mvc";
                    options.ClientSecret = "CorrectHorseBatteryStaple";
                    options.ResponseType = "code id_token";

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    //options.Scope.Add("email");
                    //options.Scope.Add("offline_access");
                    //options.Scope.Add("api");
                    options.Scope.Add("Viking.Annotation");

                    // keeps id_token smaller
                    //options.GetClaimsFromUserInfoEndpoint = true;
                    options.SaveTokens = true;

                    // if token does not contain a dot, it is a reference token
                    //options.ForwardDefaultSelector = Selector.ForwardReferenceToken("Introspection");
                })*/
            /*
                .AddOAuth("oidc", options =>
                {   
                    options.AuthorizationEndpoint = identityServerConfig["Endpoint"];
                    options.SignInScheme = "cookie";
                    options.ClientId = "mvc";
                    options.ClientSecret = "CorrectHorseBatteryStaple";
                    options.SaveTokens = true;
                })*/
                .AddOAuth2Introspection("Introspection", options =>
                {
                    options.ClientId = "mvc";
                    options.ClientSecret = "CorrectHorseBatteryStaple";
                    options.Authority = identityServerConfig["Endpoint"];
                    options.EnableCaching = true;
                    options.SaveToken = true;
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuer = true,
                        ValidateTokenReplay = true,
                        ValidateIssuerSigningKey = true,
                    };
                    
                    options.RequireHttpsMetadata = false;
                    options.Authority = identityServerConfig["Endpoint"];
                    options.SaveToken = true;

                    options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
                    //options.MapInboundClaims = false; 

                    // if token does not contain a dot, it is a reference token
                    options.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;
                    
                    
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        ValidAudience = "Viking.Annotation"
                        //RoleClaimType = "role",
                    };
                    
                });


            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme);
            /*
            /*
                .AddPolicyScheme("token", "token", policySchemeOptions =>
    {
        policySchemeOptions.ForwardDefaultSelector =
            PolicySchemeSelector.SchemeSelector;
    });*/
            //.AddOAuth();

            // adds user and client access token management
            services.AddAccessTokenManagement(options =>
                {
                    // client config is inferred from OpenID Connect settings
                    // if you want to specify scopes explicitly, do it here, otherwise the scope parameter will not be sent
                    //options.Client.Scope = "Viking.Annotation";
                    options.Client.Clients.Add("identityserver", new ClientCredentialsTokenRequest
                    {
                        Address = identityServerConfig["Endpoint"],
                        ClientId = "mvc",
                        ClientSecret = "CorrectHorseBatteryStaple",
                        Scope = "openid profile Viking.Annotation"
                    });
                });

            // registers HTTP client that uses the managed user access token
            services.AddUserAccessTokenHttpClient("user_client", null, (client) =>
            {
                client.BaseAddress = new Uri(identityServerConfig["Endpoint"] + "/api/");
            });

            // registers HTTP client that uses the managed client access token
            services.AddClientAccessTokenHttpClient("client", configureClient: client =>
            {
                client.BaseAddress = new Uri(identityServerConfig["Endpoint"] + "/api/");
            });
            
            services.AddAuthorizationPolicyEvaluator();

            services.AddMvcCore()
                .AddAuthorization();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseHttpsRedirection();

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization(); 
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<LocationService>().RequireAuthorization("protectedScope");

                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
                });
            });

        }
    }
}
