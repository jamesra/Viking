using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IdentityModel;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Microsoft.IdentityModel.Tokens;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Viking.Identity;
using Viking.Identity.Data;
using Viking.Identity.Models;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using Viking.SSL;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Endpoints;
using Duende.IdentityServer.Services;

namespace Viking.Identity.Server.Standalone
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;            
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        { 
            var SSLOptions = Configuration.GetSection("SSL").Get<Viking.SSL.SSLOptions>();

            //            builder.AddDeveloperSigningCredential();
            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status308PermanentRedirect;
                options.HttpsPort = SSLOptions.Port;
            });
            services.AddRazorPages();
             
            services.ConfigureIdentityServerDataContext(Configuration);

            var persistedGrantConnectionString = Configuration.GetConnectionString("PersistedGrantConnection");
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            var serverOptions = Configuration.GetSection(nameof(VikingIdentityServerOptions)).Get<VikingIdentityServerOptions>();
            services.Configure<VikingIdentityServerOptions>(
                Configuration.GetSection(nameof(VikingIdentityServerOptions)));
              
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

                    // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                    options.EmitStaticAudienceClaim = true;
                      
                    options.IssuerUri = serverOptions.Authority;
                }) /*.AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        builder.UseSqlServer(configConnectionString,
                            sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                */
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
                .AddInMemoryCaching()
                
                .AddAspNetIdentity<ApplicationUser>()
                .AddDefaultEndpoints()
                .AddJwtBearerClientAuthentication();
            
            
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(@"./DataProtectionKeys/"))
                .ProtectKeysWithCertificate(Certs.LoadSSLCertificate(SSLOptions))
                .SetApplicationName("VikingIdentityServer");
             
            // Load SSL certificate and configure IdentityServer
            var sslCert = Certs.LoadSSLCertificate(SSLOptions);
            if (sslCert != null)
            {
                try
                {
                    var signingCredentials = new SigningCredentials(new X509SecurityKey(sslCert), SecurityAlgorithms.RsaSha256);
                    builder.AddSigningCredential(signingCredentials);
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
            
            // Add authorization services
            //services.AddAuthorization();
            
            // Configure email options
            services.Configure<EmailOptions>(Configuration.GetSection("Email"));
            
            // Register EmailSender for Identity email functionality
            services.AddTransient<IEmailSender<ApplicationUser>, EmailSender>();
            
            services.AddControllers();            
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Log.Information("Starting IdentityServer configuration...");
            
            // this will do the initial DB population and required migrations
            InitializeDatabase(app);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            } 

            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();
            app.UseStaticFiles();   
            app.UseRouting();
            
            Log.Information("Adding IdentityServer middleware...");
            app.UseIdentityServer();             
            Log.Information("IdentityServer middleware added successfully.");
            
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
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
