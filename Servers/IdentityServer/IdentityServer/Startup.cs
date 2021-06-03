using IdentityModel;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Services;
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System;
using System.Linq;
using System.Reflection;

namespace IdentityServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
              
            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
              .WriteTo.File("IDServerLogs.json", Serilog.Events.LogEventLevel.Verbose)
              .CreateLogger();
        }

        public IConfiguration Configuration { get; }
         
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

            services.AddControllersWithViews();
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            var connectionString = Configuration.GetConnectionString("IdentityConnection");
            var persistedGrantConnectionString = Configuration.GetConnectionString("PersistedGrantConnection");
            var configConnectionString = Configuration.GetConnectionString("ConfigConnection");

            services.AddLogging(loggingBuilder =>
                loggingBuilder.AddSerilog(dispose: true));

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("IdentityConnection")).EnableDetailedErrors().EnableSensitiveDataLogging());


            IConfigurationSection identityServerConfig = Configuration.GetSection("IdentityServer");

            services.AddAuthentication(options =>
            { 
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
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidateTokenReplay = true,

                };
                options.RequireHttpsMetadata = true;
                options.Authority = identityServerConfig["Endpoint"];
                options.SaveToken = true;

                options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
                //options.MapInboundClaims = false; 

                // if token does not contain a dot, it is a reference token
                //options.ForwardDefaultSelector = ForwardReferenceToken;//Selector.ForwardReferenceToken("introspection");
            })
            //.AddApplicationCookie()
             
            // reference tokens
            /*
            .AddOAuth2Introspection("introspection", options =>
            {
                options.Authority = identityServerConfig["Endpoint"];
                options.ClientId = "mvc";
                options.ClientSecret = Config.Secret;
                options.SaveToken = true;
                options.SkipTokensWithDots = true;
                options.
            }) */
            ;

            services.AddAuthorization(options =>
            {
                var builder = new AuthorizationPolicyBuilder();
                builder.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
                builder.RequireAuthenticatedUser();
                options.AddPolicy(Config.Policy.BearerToken, builder.Build());
                options.AddPolicy(Config.Policy.GroupAccessManager, policy => policy.Requirements.Add(Authorization.Operations.GroupAccessManager));
                options.AddPolicy(Config.Policy.OrgUnitAdmin, policy => policy.Requirements.Add(Authorization.Operations.OrgUnitAdmin));
            });

            //services.AddHttpContextAccessor();
            //services.AddTransient<System.Security.Claims.ClaimsPrincipal>(provider => provider.GetService<IHttpContextAccessor>().HttpContext.User);
            services.AddTransient<IdentityServer4.Validation.ICustomTokenRequestValidator, IdentityServer.Extensions.UserScopeTokenRequestValidator>();
            services.AddScoped<IdentityServer.Extensions.AuthorizationHelper>();
            services.AddScoped<IAuthorizationHandler, IdentityServer.Authorization.ResourceIdPermissionsAuthorizationHandler>();
            services.AddScoped<IAuthorizationHandler, IdentityServer.Authorization.ResourcePermissionsAuthorizationHandler>();
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
                //.AddScopeParser<ParameterizedScopeParser>()
                .AddInMemoryApiScopes(Config.GetApiScopes())
                .AddInMemoryApiResources(Config.GetApiResources())
                .AddInMemoryClients(Config.GetClients()) 
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
                    options.TokenCleanupInterval = 30;
                })                
                .AddAspNetIdentity<ApplicationUser>()
                .AddJwtBearerClientAuthentication();

            //            builder.AddDeveloperSigningCredential();
            IConfigurationSection sslConfig = Configuration.GetSection("SSL");
            ConfigureSSL(builder, sslConfig);

            services.AddTransient<IdentityServer4.Services.IProfileService, IdentityServer.Extensions.IdentityWithExtendedClaimsProfileService>();
             
            services.Configure<SMTPOptions>(Configuration.GetSection("SMTP")); 
        }

        public void ConfigureSSL(IIdentityServerBuilder builder, IConfigurationSection config)
        {
            string serialNumber = config["SerialNumber"];
            //var certs = X509.LocalMachine.My.SubjectDistinguishedName.Find(serialNumber).ToList();

            var cert = X509.LocalMachine.My.SerialNumber.Find(serialNumber, false)
                .Where(o => DateTime.UtcNow >= o.NotBefore)
                .OrderByDescending(o => o.NotAfter)
                .FirstOrDefault();
            if (cert == null) throw new Exception("No valid certificates could be found.");

            var signingCredentials = new SigningCredentials(new X509SecurityKey(cert), "RS256");

            builder.AddSigningCredential(signingCredentials);
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

            app.UseHttpsRedirection();
            app.UseStaticFiles();           
            app.UseIdentityServer();
            app.UseRouting(); 
            //app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapDefaultControllerRoute());

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
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();
                serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();

                Config.AdminRoleId = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.FirstOrDefault(ur => ur.Name == Config.AdminRoleName).Id;

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
}
