using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Options;
using Viking.Identity.Data;
using Viking.Identity.Server;

namespace Viking.Identity
{
    public class IdentityServerVikingClientStoreConfig
    {
        public string ClientId { get; set; } 
    }

    public class IdentityServerVikingClientStore : IClientStore
    {
        /// <summary>Client id issued to the third-party sbfsem-tools web application.</summary>
        public const string SbfsemToolsClientId = "sbfsem-tools";

        ApplicationDbContext _context;
        IResourceStore _resourceStore;

        private readonly Secret _clientSecret;

        private readonly Uri _redirectUri;

        private readonly string _sbfsemToolsSecret;
        private readonly string[] _sbfsemToolsRedirectUris;
        private readonly string[] _sbfsemToolsPostLogoutRedirectUris;

        public IdentityServerVikingClientStore(ApplicationDbContext context, IResourceStore resourceStore, IOptions<VikingIdentityServerOptions> serverOptions)
        {
            var options = serverOptions.Value;
            var secret = options.Secret;

            _redirectUri = new Uri(options.Authority);
            _clientSecret = new Secret(secret.Sha256());
            _sbfsemToolsSecret = options.SbfsemToolsClientSecret;
            _sbfsemToolsRedirectUris = options.SbfsemToolsRedirectUris ?? Array.Empty<string>();
            _sbfsemToolsPostLogoutRedirectUris = options.SbfsemToolsPostLogoutRedirectUris ?? Array.Empty<string>();
            _context = context;
            _resourceStore = resourceStore;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId != "ro.viking" && clientId != "mvc" && clientId != "Viking" && clientId != "api" &&
                clientId != SbfsemToolsClientId)
                return null;

            // Without a configured secret the third-party client would fall back to the first-party
            // shared secret, so refuse to serve it instead.
            if (clientId == SbfsemToolsClientId && string.IsNullOrWhiteSpace(_sbfsemToolsSecret))
                return null;

            // Rebuild AllowedScopes from the live resource store on every lookup so newly
            // created volumes (e.g. Yiu) are usable without restarting Identity Server.
            var allResources = await _resourceStore.GetAllResourcesAsync();

            var scopes = allResources.ApiScopes.Where(s => s != null).Select(s => s.Name).ToList();

            var readonlyScopes = new List<string>
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile
            };

            readonlyScopes.AddRange(allResources.ApiScopes.Where(s => s.Name.ToLower().Contains(".read")).Select(s => s.Name));
            readonlyScopes.AddRange(IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name));

            scopes.Add(IdentityServerConstants.StandardScopes.OpenId);
            scopes.Add(IdentityServerConstants.StandardScopes.Profile);
            scopes.AddRange(IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name));

            if (clientId == "mvc")  /* The MVC client is used for the Identity Management Site */
            {
                return new Client
                {
                    ClientId = clientId,
                    ClientName = "MVC Client",
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode, GrantType.ResourceOwnerPassword, GrantType.ClientCredentials },
                    RequireConsent = false,
                    ClientSecrets = { _clientSecret },
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    FrontChannelLogoutUri = new Uri(_redirectUri,"signout-oidc").ToString(),
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                    AllowedScopes = scopes,
                    AllowOfflineAccess = true
                };
            }
            else if (clientId == "api") /* Clients of the API*/
            {
                return new Client
                {
                    ClientId = clientId,
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode, GrantType.ResourceOwnerPassword, GrantType.ClientCredentials, VikingUserTokenGrantValidator.VikingUserTokenGrantType },
                    RequireConsent = false,
                    ClientSecrets = { _clientSecret },
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                    AllowedScopes = scopes,
                    AllowOfflineAccess = true,
                    AccessTokenType = AccessTokenType.Reference
                };
            }
            else if (clientId == SbfsemToolsClientId) /* Third party web tool, confidential backend */
            {
                return new Client
                {
                    ClientId = clientId,
                    ClientName = "sbfsem-tools",
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode },
                    RequirePkce = true,
                    RequireConsent = false,
                    ClientSecrets = { new Secret(_sbfsemToolsSecret.Sha256()) },
                    RedirectUris = _sbfsemToolsRedirectUris,
                    PostLogoutRedirectUris = _sbfsemToolsPostLogoutRedirectUris,
                    // Volume scopes are not needed: the Permissions API authorizes on the user, not the scope.
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile
                    }.Concat(IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name)).ToList(),
                    AllowOfflineAccess = true,
                    AccessTokenType = AccessTokenType.Reference
                };
            }
            else if (clientId == "Viking") /* The Viking client is used for the Viking Application */
            {
                return new Client
                {
                    ClientId = clientId,
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode, GrantType.ResourceOwnerPassword, GrantType.ClientCredentials },
                    ClientSecrets = { _clientSecret },
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    FrontChannelLogoutUri = new Uri(_redirectUri,"signout-oidc").ToString(),
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                    AllowedScopes = scopes
                };
            }
            else // ro.viking = Read only tools/applications that access Viking Data
            {
                return new Client
                {
                    ClientId = clientId,
                    AllowedGrantTypes = new[] { GrantType.ResourceOwnerPassword, GrantType.ClientCredentials },
                    ClientSecrets = { _clientSecret },
                    AllowedScopes = readonlyScopes,
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    FrontChannelLogoutUri = new Uri(_redirectUri,"signout-oidc").ToString(),
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                };
            }
        }
    }
}
