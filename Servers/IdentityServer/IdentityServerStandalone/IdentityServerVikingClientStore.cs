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
        ApplicationDbContext _context;
        IResourceStore _resourceStore;

        Dictionary<string, Client> ClientCache = new Dictionary<string, Client>();
        
        private readonly Secret _clientSecret;

        private readonly Uri _redirectUri;

        public IdentityServerVikingClientStore(ApplicationDbContext context, IResourceStore resourceStore, IOptions<VikingIdentityServerOptions> serverOptions)
        {
            var options = serverOptions.Value;
            var secret = options.Secret;

            _redirectUri = new Uri(options.Authority);
            _clientSecret = new Secret(secret.Sha256());
            _context = context;
            _resourceStore = resourceStore;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId != "ro.viking" && clientId != "mvc" && clientId != "Viking" && clientId != "api")
                return null;

            if (ClientCache.ContainsKey(clientId))
                return ClientCache[clientId];

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

            Client result;
            
            if (clientId == "mvc")  /* The MVC client is used for the Identity Management Site */
            {
                result = new Client
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
                result = new Client
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
            else if (clientId == "Viking") /* The Viking client is used for the Viking Application */
            {
                result = new Client
                {
                    ClientId = clientId,
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode, GrantType.ResourceOwnerPassword, GrantType.ClientCredentials },
                    ClientSecrets = { _clientSecret },
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    FrontChannelLogoutUri = new Uri(_redirectUri,"signout-oidc").ToString(),
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                    /*AllowedScopes = scopes.Where(s => s == IdentityServerConstants.StandardScopes.OpenId || 
                                                      s == IdentityServerConstants.StandardScopes.Profile).ToArray()
                    */
                    AllowedScopes = scopes
                };
            }
            else // ro.viking = Read only tools/applications that access Viking Data
            {
                result = new Client
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

            ClientCache[clientId] = result;

            return result;
        }
    }
}
