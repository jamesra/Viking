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
using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.IdentityModel.Tokens;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Viking.Identity;
using Viking.Identity.Data;
using Viking.Identity.Models;

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
            var SSLOptions = Configuration.GetSection("SSLOptions").Get<SSLOptions>();

            //            builder.AddDeveloperSigningCredential();
            IConfigurationSection sslConfig = Configuration.GetSection("SSL");
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

                    // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                    options.EmitStaticAudienceClaim = true;
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
                .AddAspNetIdentity<ApplicationUser>()
                .AddJwtBearerClientAuthentication();

            ConfigureSSL(builder, SSLOptions);
        }

        public static IIdentityServerBuilder ConfigureSSL(IIdentityServerBuilder builder, SSLOptions config)
        {
            string serialNumber = config.SerialNumber;
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

            return builder;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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
            //app.UseStaticFiles();   
            app.UseIdentityServer();
            //app.UseRouting(); 
            //app.UseAuthorization(); 
            /*app.UseEndpoints(endpoints =>
            {
                //endpoints.MapRazorPages();
            });*/
        }

        private static IApplicationBuilder InitializeDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
                                            serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

                            
            }

            return app;
        }
    }



    public struct SSLOptions
    {
        [NotNull]
        public string SerialNumber { get; set; }
    }
}
