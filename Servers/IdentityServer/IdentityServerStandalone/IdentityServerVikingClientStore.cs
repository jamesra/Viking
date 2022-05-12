using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        private readonly string _redirectUri;

        public IdentityServerVikingClientStore(ApplicationDbContext context, IResourceStore resourceStore, IOptions<VikingIdentityServerOptions> serverOptions)
        {
            var options = serverOptions.Value;
            var secret = options.Secret;

            _redirectUri = options.Authority;
            _clientSecret = new Secret(secret.Sha256());
            _context = context;
            _resourceStore = resourceStore;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId != "ro.viking")
                return null;

            if (ClientCache.ContainsKey(clientId))
                return ClientCache[clientId];

            var allResources = await _resourceStore.GetAllResourcesAsync();

            var scopes = allResources.ApiScopes.Select(s => s.Name).ToList();

            scopes.Add(IdentityServerConstants.StandardScopes.OpenId);
            scopes.Add(IdentityServerConstants.StandardScopes.Profile);
            scopes.AddRange(IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name));

            var result = new Client
            {
                ClientId = clientId,
                AllowedGrantTypes = new[] { GrantType.AuthorizationCode, GrantType.ResourceOwnerPassword, GrantType.ClientCredentials },
                //AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                ClientSecrets =
                    {
                       _clientSecret
                    },
                AllowedScopes = scopes,
                RedirectUris = new string[] {_redirectUri}
            };

            ClientCache[clientId] = result;

            return result;
        }
    }
}
