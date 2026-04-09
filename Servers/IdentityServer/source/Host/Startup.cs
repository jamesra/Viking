/*
 * Copyright 2014 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Owin;
using Microsoft.Owin;
using System.Collections.Generic;
using System.Data.Entity;
using IdentityManager.Configuration;
using IdentityManager.Host.Config;
using IdentityManager.Core.Logging;
using IdentityManager.Logging;
using System.Threading.Tasks;
using Thinktecture.IdentityServer.Core.Configuration;
using Thinktecture.IdentityServer.Core.Models;
using Thinktecture.IdentityServer.AccessTokenValidation;
using System.Web.Configuration;

[assembly: Microsoft.Owin.OwinStartup(typeof(IdentityManager.Host.StartupWithHostCookiesSecurity))]

namespace IdentityManager.Host
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            LogProvider.SetCurrentLogProvider(new TraceSourceLogProvider());

            var factory = new IdentityManagerServiceFactory();

            factory.ConfigureCustomIdentityManagerServiceWithGuidKeys("IdentityDB");

            app.UseIdentityManager(new IdentityManagerOptions()
                {
                    Factory = factory,
                });
        }
    }

    public class StartupWithLocalhostSecurity
    {
        public void Configuration(IAppBuilder app)
        {
            LogProvider.SetCurrentLogProvider(new TraceSourceLogProvider());

            // this configures IdentityManager
            // we're using a Map just to test hosting not at the root
            app.Map("/idm", idm =>
            {
                var factory = new IdentityManagerServiceFactory();
                factory.ConfigureCustomIdentityManagerServiceWithGuidKeys("IdentityDB");
                idm.UseIdentityManager(new IdentityManagerOptions
                {
                    Factory = factory,
                });
            });

            // used to redirect to the main admin page visiting the root of the host
            app.Run(ctx =>
            {
                ctx.Response.Redirect("/idm/");
                return Task.FromResult(0);
            });
        }
    }

    

    public class StartupWithHostCookiesSecurity
    {  
        public void Configuration(IAppBuilder app)
        {
            LogProvider.SetCurrentLogProvider(new TraceSourceLogProvider());

            Database.SetInitializer<CustomContext>(new MyDbInitializer());

            System.IdentityModel.Tokens.JwtSecurityTokenHandler.InboundClaimTypeMap = new Dictionary<string, string>();


            app.UseCookieAuthentication(new Microsoft.Owin.Security.Cookies.CookieAuthenticationOptions
            {
                AuthenticationType = "Cookies"
            });

            string AuthorityUri = WebConfigurationManager.AppSettings.Get("AuthorityUri");
            string RedirectUri = WebConfigurationManager.AppSettings.Get("RedirectUri");
            
            app.UseOpenIdConnectAuthentication(new Microsoft.Owin.Security.OpenIdConnect.OpenIdConnectAuthenticationOptions
            {
                AuthenticationType = "oidc",
                Authority = AuthorityUri,
                ClientId = "idmgr_client",
                RedirectUri = RedirectUri,
                ResponseType = "id_token",
                UseTokenLifetime = false,
                Scope = "openid idmgr",
                SignInAsAuthenticationType = "Cookies",
                Caption = "Viking Authentication"
            });

            app.Map("/idm", idm =>
            {
                var factory = new IdentityManagerServiceFactory();
                factory.ConfigureCustomIdentityManagerServiceWithGuidKeys("IdentityDB");
                idm.UseIdentityManager(new IdentityManagerOptions
                {
                    Factory = factory,
                    SecurityConfiguration = new HostSecurityConfiguration
                    {
                        HostAuthenticationType = "Cookies",
                        AdminRoleName = "Admin",
                        RequireSsl = true,
                        ShowLoginButton = true,
                    }
                });
            });

            // this configures an embedded IdentityServer to act as an external authentication provider
            // when using IdentityManager in Token security mode. normally you'd configure this elsewhere.
            //var IDSvrFactory = new Thinktecture.IdentityServer.Core.Configuration.IdentityServerServiceFactory();
            
            app.Map("/ids", ids =>
            {
                var idServerFactory = VikingIdentityServer.Factory.Configure();
                VikingIdentityServer.UserServiceExtensions.ConfigureUserService(idServerFactory, "IdentityDB");
                
                var idsrvOptions = new IdentityServerOptions
                {
                    SiteName = "Viking Connectome Identity Server",
                    Factory = idServerFactory,
                    RequireSsl = true,
                    //SigningCertificate = VikingIdentityServer.Certificate.SelectCertificate(),
                    CorsPolicy = CorsPolicy.AllowAll,
                    LoggingOptions = new LoggingOptions { EnableHttpLogging = true, EnableWebApiDiagnostics = true, IncludeSensitiveDataInLogs = true, WebApiDiagnosticsIsVerbose = true}
                };

                ids.UseIdentityServer(idsrvOptions);
            });
              
            // used to redirect to the main admin page visiting the root of the host
            //app.Run(ctx =>
            //{ 
                //return Task.FromResult(0);
            //});
        }

    }
}