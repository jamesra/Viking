using System;
using System.Linq;
using System.Reflection;
using IdentityModel;
using IdentityModel.AspNetCore.OAuth2Introspection;
using IdentityServer4.Extensions;
using IdentityServer4.Services;
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

namespace Viking.Identity.Server.WebManagement
{
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

    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration; 
        }

        private IConfiguration Configuration { get; }
         
        /*
        private string ForwardReferenceToken(HttpContext context)
        {
            var auth = context.Request.Headers["Authorization"];

            int iBearer;
            for(iBearer = 0; iBearer < auth.Count; iBearer++)
            {
                if (auth[iBearer].StartsWith("Bearer"))
                    break;
            }

            if (iBearer == auth.Count)
                return null;

            var token = auth[iBearer];
             
            if (token.Contains('.'))
                return "introspection";
            
            return "Bearer";
        } 
        */
        
        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            //System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap = new Dictionary<string, string>();
            //System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap = new Dictionary<string, string>();

            //System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Add("sub", System.IdentityModel.ClaimTypes.NameIdentifier);

            services.AddSingleton<ICorsPolicyService>((container) => {
                var logger = container.GetRequiredService<ILogger<DefaultCorsPolicyService>>();
                return new DefaultCorsPolicyService(logger) {
                    //AllowedOrigins = { "https://websvc1.connectomes.utah.edu", "https://bar" }
                    AllowAll = true
                };
            });

            services.ConfigureIdentityServerDataContext(Configuration);

            services.AddControllers(); //Adds api controllers
            services.AddControllersWithViews(); 
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            //var connectionString = Configuration.GetConnectionString("IdentityConnection");
            var persistedGrantConnectionString = Configuration.GetConnectionString("PersistedGrantConnection");
            //var configConnectionString = Configuration.GetConnectionString("ConfigConnection");

            services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true).AddConsole()
            );
             
            services.AddAntiforgery();
             
            //IConfigurationSection identityServerConfig = Configuration.GetSection("IdentityServer");
            JwtBearerOptions jwtOptions = Configuration.GetSection(nameof(JwtBearerOptions)).Get<JwtBearerOptions>();

            //Configuration.GetSection(nameof(JwtBearerOptions)).Bind(jwtOptions);
            services.Configure<JwtBearerOptions>(Configuration.GetSection(nameof(JwtBearerOptions)));

            var serverOptions = Configuration.GetSection(nameof(VikingIdentityServerOptions)).Get<VikingIdentityServerOptions>();
            services.Configure<VikingIdentityServerOptions>(
                Configuration.GetSection(nameof(VikingIdentityServerOptions)));
            //jwtOptions.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;
              
            var OAuth2ConfigurationSection = Configuration.GetSection(nameof(OAuth2IntrospectionOptions));
            if (OAuth2ConfigurationSection is null)
                throw new ArgumentException(
                    $"{nameof(OAuth2IntrospectionOptions)} section missing from appsettings.json configuration");

            OAuth2IntrospectionOptions OAuth2Options = OAuth2ConfigurationSection.Get<OAuth2IntrospectionOptions>();
            OAuth2Options.Validate();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddCookie()
            //.AddApplicationCookie()
            /*.AddOpenIdConnect("oidc", options => { 
                options.Authority = "https://localhost:44322/";
                options.ClientId = "mvc";
                options.ClientSecret = Config.Secret.ToSha256();
                options.SaveTokens = true;
                options.Scope.Add("Viking.Annotation");
                options.Scope.Add("openid");
            })*/
            // JWT tokens 
            /*I spent an eternity getting authentication with a token to work and I was never
             * able to get jwt+cookies to work side-by-side
             * https://stackoverflow.com/questions/49455943/asp-net-core-webapi-cookie-jwt-authentication
             * The end result is any Web API style calls that require a token must be decorated with
             * [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
             * */
                .AddOAuth2Introspection("Introspection", options =>
                {
                    OAuth2ConfigurationSection.Bind(options);
                })
                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    Configuration.Bind(nameof(JwtBearerOptions), options);
                    options.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;
                    /*
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuer = true,
                        ValidateTokenReplay = true,
                        ValidateIssuerSigningKey = true, 
                    };
    
                    options.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                    options.Authority = jwtOptions.Authority;
                    options.SaveToken = true;
    
                    options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
                    options.ForwardDefaultSelector = PolicySchemeSelector.SchemeSelector;
    
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        NameClaimType = "name",
                        ValidAudience = "Viking.Annotation"
                        //RoleClaimType = "role",
                    };
                    */
                })
            //.AddApplicationCookie()
             
            // reference tokens
            /*
            .AddOAuth2Introspection("introspection", options =>
            {
                options.Authority = identityServerConfig["Endpoint"];
                options.ClientId = "mvc";
                options.ClientSecret = Config.Secret.ToSha256();
                options.SaveToken = true;
                options.SkipTokensWithDots = true;
                options.
            }) */
            ;


            //services.AddHttpContextAccessor();
            //services.AddTransient<System.Security.Claims.ClaimsPrincipal>(provider => provider.GetService<IHttpContextAccessor>().HttpContext.User);
            services.AddTransient<IdentityServer4.Validation.ICustomTokenRequestValidator, UserScopeTokenRequestValidator>();
                //services.AddScoped<IAuthorizationHelper, AuthorizationHelper>();
            services.AddScoped<IAuthorizationHandler, ResourceIdPermissionsAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, ResourcePermissionsAuthorizationHandler>();
            //services.AddTransient<IdentityServer.Extensions.AuthorizationHelper>();

            var https_port_section = Configuration.GetSection("https_port");
            int? https_port = new int?();

            if (https_port_section.Value != null)
            { 
                try
                {
                    https_port = System.Convert.ToInt32(https_port_section.Value);
                }

                catch (System.FormatException)
                {
                    https_port = new int?();
                    Log.Logger.Warning($"https_port in appsettings.json : {https_port_section.Value} could not be parsed to a port number.  Using default.");
                } 
            }
            else
            {
                Log.Logger.Information($"https_port not set in appsettings.json");
            }

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = https_port;
            });

            services.AddIdentity<ApplicationUser, ApplicationRole>(config =>
             { config.SignIn.RequireConfirmedEmail = true;  
             })
                .AddEntityFrameworkStores<ApplicationDbContext>() 
                .AddDefaultTokenProviders();
             
            // Add application services.
            services.AddTransient<IEmailSender, EmailSender>();

            //Add a helper service to pull permissions
            services.AddTransient<IPermissionsViewModelHelper, PermissionsViewModelHelper>();

            //services.AddMvc(options => options.EnableEndpointRouting = false);
             
            // configure identity server with in-memory stores, keys, clients and scopes
            
            /*
            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                options.EmitStaticAudienceClaim = true;
            })/*.AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        builder.UseSqlServer(configConnectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                */
            /*
                //.AddScopeParser<ParameterizedScopeParser>()
                .AddInMemoryApiScopes(Config.GetApiScopes(serverOptions))
                .AddInMemoryApiResources(Config.GetApiResources(serverOptions))
                .AddInMemoryClients(Config.GetClients(serverOptions)) 
                .AddInMemoryIdentityResources(Config.GetIdentityResources())
                .AddResourceStore<IdentityServerCustomResourceStore>()
                .AddClientStore<IdentityServerVikingClientStore>()
                // this adds the operational data from DB (codes, tokens, consents)
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        builder.UseSqlServer(persistedGrantConnectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));

                    // this enables automatic token cleanup. this is optional.
                    options.EnableTokenCleanup = true;
                    options.TokenCleanupInterval = 3600;
                })                
                .AddAspNetIdentity<ApplicationUser>()
                .AddJwtBearerClientAuthentication();
            */
            //            builder.AddDeveloperSigningCredential();
            /*IConfigurationSection sslConfig = Configuration.GetSection("SSL");
            ConfigureSSL(builder, sslConfig);
            */

            services.AddTransient<IdentityServer4.Services.IProfileService, IdentityWithExtendedClaimsProfileService>();
              
            services.AddAuthorizationPolicyEvaluator();

            services.AddAuthorization(options =>
            {
                var builder = new AuthorizationPolicyBuilder();
                builder.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                //builder.AddAuthenticationSchemes("Introspection");
                //builder.AddAuthenticationSchemes("Cookies");
                builder.RequireAuthenticatedUser();
                options.AddPolicy("protectedScope", policy => policy.RequireClaim("scope", "Viking.Annotation"));
                options.AddPolicy(Config.Policy.BearerToken, builder.Build());
                options.AddPolicy(Config.Policy.GroupAccessManager, policy => policy.Requirements.Add(Authorization.Operations.GroupAccessManager));
                options.AddPolicy(Config.Policy.OrgUnitAdmin, policy => policy.Requirements.Add(Authorization.Operations.OrgUnitAdmin));
            });


            services.Configure<SMTPOptions>(Configuration.GetSection("SMTP"));

            
            services.AddAccessTokenManagement(options =>
            {
                // client config is inferred from OpenID Connect settings
                // if you want to specify scopes explicitly, do it here, otherwise the scope parameter will not be sent
                //options.Client.Scope = "Viking.Annotation";
                /*
                options.Client.Clients.Add("identityserver", new ClientCredentialsTokenRequest
                {
                    Address = jwtOptions.Authority,
                    ClientId = "mvc",
                    ClientSecret = serverOptions.Secret.ToSha256(),
                    Scope = "openid profile Viking.Annotation"
                });
                */
            });
            /*
            services.AddUserAccessTokenHttpClient("user_client", null, (client) =>
            {
                client.BaseAddress = new Uri(identityServerConfig["Endpoint"] + "/api/");
            });

            // registers HTTP client that uses the managed client access token
            services.AddClientAccessTokenHttpClient("client", configureClient: client =>
            {
                client.BaseAddress = new Uri(identityServerConfig["Endpoint"] + "/api/");
            });
            */
            //services.AddMvcCore().AddAuthorization();
        }

        public void ConfigureSSL(IIdentityServerBuilder builder, IConfigurationSection config)
        {
            string serialNumber = config["SerialNumber"];
            if (serialNumber.IsNullOrEmpty())
            { 
                builder.AddDeveloperSigningCredential();
            }
            else
            { 
                //var certs = X509.LocalMachine.My.SubjectDistinguishedName.Find(serialNumber).ToList();

                var cert = X509.LocalMachine.My.SerialNumber.Find(serialNumber, false)
                    .Where(o => DateTime.UtcNow >= o.NotBefore)
                    .OrderByDescending(o => o.NotAfter)
                    .FirstOrDefault();
                if (cert == null) throw new Exception("No valid certificates could be found.");

                var signingCredentials = new SigningCredentials(new X509SecurityKey(cert), "RS256");

                builder.AddSigningCredential(signingCredentials);
            }
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        { 
            // this will do the initial DB population
            InitializeDatabase(app);

            if (env.IsDevelopment())
            {
                //app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error"); 
            }
             
            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            //app.UseIdentityServer();
            app.UseRouting(); 
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                //We cannot require authorization on all routes or users are unable to login
                endpoints.MapDefaultControllerRoute(); //.RequireAuthorization(Config.Policy.BearerToken); 
                endpoints.MapControllers();
            });

            /*
            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });*/
        }

        private void InitializeDatabase(IApplicationBuilder app)
        {
            /*
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
            }
             */
            /* This should be added when I transition from keeping configuration in memory to keeping it in the database*/
                /*
               var context = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
               context.Database.Migrate();
               if (!context.Clients.Any())
               {
                   foreach (var client in Config.GetClients())
                   {
                       context.Clients.Add(client.ToEntity());
                   }
                   context.SaveChanges();
               }

               if (!context.IdentityResources.Any())
               {
                   foreach (var resource in Config.GetIdentityResources())
                   {
                       context.IdentityResources.Add(resource.ToEntity());
                   }
                   context.SaveChanges();
               }

               if (!context.ApiResources.Any())
               {
                   foreach (var resource in Config.GetApiResources())
                   {
                       context.ApiResources.Add(resource.ToEntity());
                   }
                   context.SaveChanges();
               }
               */
            
        }
    }
}
