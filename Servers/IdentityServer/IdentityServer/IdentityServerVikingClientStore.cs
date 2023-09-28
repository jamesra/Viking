using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Stores;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Data;

namespace Viking.Identity
{
    public class IdentityServerVikingClientStore : IClientStore
    {
        readonly ApplicationDbContext _context;
        readonly IResourceStore _resourceStore;

        readonly Dictionary<string, Client> ClientCache = new Dictionary<string, Client>();

        public IdentityServerVikingClientStore(ApplicationDbContext context, IResourceStore resourceStore)
        {
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
                        new Secret(Config.Secret.Sha256())
                    },
                AllowedScopes = scopes,
                RedirectUris = new string[] {"http://localhost:5000/"}
            };

            ClientCache[clientId] = result;

            return result;
        }
    }
}
