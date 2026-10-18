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
        public const string SbfsemToolsClientId = VikingOAuthClients.SbfsemTools;

        /// <summary>
        /// Client id shared by correction service containers. It carries only the Viking.Annotation scope so the
        /// Permissions API can introspect its token; the endpoint it may call checks this client id explicitly.
        /// </summary>
        public const string CorrectionClientId = "correction";

        /// <summary>Client ids of the SAM2 annotation galleries: confidential web apps that sign users in.</summary>
        public const string GalleryTemClientId = "sam2-gallery-tem";
        public const string GallerySemClientId = "sam2-gallery-sem";

        ApplicationDbContext _context;
        IResourceStore _resourceStore;
        private readonly VikingIdentityServerOptions _options;
        private readonly Uri _redirectUri;

        public IdentityServerVikingClientStore(ApplicationDbContext context, IResourceStore resourceStore, IOptions<VikingIdentityServerOptions> serverOptions)
        {
            _options = serverOptions.Value;
            _redirectUri = new Uri(_options.Authority);
            _context = context;
            _resourceStore = resourceStore;
        }

        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            if (clientId != "ro.viking" && clientId != "mvc" && clientId != "Viking" && clientId != "api" &&
                clientId != SbfsemToolsClientId && clientId != CorrectionClientId &&
                clientId != GalleryTemClientId && clientId != GallerySemClientId)
                return null;

            // GetClientSecret throws when a secret is unconfigured. For the third-party client that
            // would surface as a 500 on every lookup, so treat "not configured" as "not registered".
            if (clientId == SbfsemToolsClientId && string.IsNullOrWhiteSpace(_options.SbfsemToolsClientSecret))
                return null;
            if (clientId == CorrectionClientId && string.IsNullOrWhiteSpace(_options.CorrectionServiceClientSecret))
                return null;
            if (clientId == GalleryTemClientId && string.IsNullOrWhiteSpace(_options.GalleryTemClientSecret))
                return null;
            if (clientId == GallerySemClientId && string.IsNullOrWhiteSpace(_options.GallerySemClientSecret))
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

            var clientSecret = new Secret(_options.GetClientSecret(clientId).Sha256());

            if (clientId == "mvc")  /* The MVC client is used for the Identity Management Site */
            {
                return new Client
                {
                    ClientId = clientId,
                    ClientName = "MVC Client",
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode, GrantType.ResourceOwnerPassword, GrantType.ClientCredentials },
                    RequireConsent = false,
                    ClientSecrets = { clientSecret },
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
                    ClientSecrets = { clientSecret },
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                    AllowedScopes = scopes,
                    AllowOfflineAccess = true,
                    AccessTokenType = AccessTokenType.Reference
                };
            }
            else if (clientId == CorrectionClientId) /* Correction service containers (machine to machine) */
            {
                return new Client
                {
                    ClientId = clientId,
                    ClientName = "Correction Service",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    ClientSecrets = { clientSecret },
                    AllowedScopes = IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name).ToList(),
                    AccessTokenType = AccessTokenType.Reference
                };
            }
            else if (clientId == GalleryTemClientId || clientId == GallerySemClientId) /* SAM2 annotation galleries */
            {
                var isTem = clientId == GalleryTemClientId;
                return new Client
                {
                    ClientId = clientId,
                    ClientName = isTem ? "SAM2 Annotation Gallery (TEM)" : "SAM2 Annotation Gallery (SEM)",
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode },
                    // The gallery's backend authenticates with the client secret; it does not send a PKCE challenge.
                    RequirePkce = false,
                    RequireConsent = false,
                    ClientSecrets = { clientSecret },
                    RedirectUris = (isTem ? _options.GalleryTemRedirectUris : _options.GallerySemRedirectUris) ?? Array.Empty<string>(),
                    // Volume scopes are omitted: the Permissions API authorizes on the user, not the scope.
                    AllowedScopes = new List<string>
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile
                    }.Concat(IdentityServerCustomResourceStore.StandardScopes.Select(s => s.Name)).ToList(),
                    AccessTokenType = AccessTokenType.Reference
                };
            }
            else if (clientId == SbfsemToolsClientId) /* Third party web tool with a confidential backend */
            {
                return new Client
                {
                    ClientId = clientId,
                    ClientName = "sbfsem-tools",
                    AllowedGrantTypes = new[] { GrantType.AuthorizationCode },
                    RequirePkce = true,
                    RequireConsent = false,
                    ClientSecrets = { clientSecret },
                    RedirectUris = _options.SbfsemToolsRedirectUris ?? Array.Empty<string>(),
                    PostLogoutRedirectUris = _options.SbfsemToolsPostLogoutRedirectUris ?? Array.Empty<string>(),
                    // Volume scopes are omitted: the Permissions API authorizes on the user, not the scope.
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
                    ClientSecrets = { clientSecret },
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
                    ClientSecrets = { clientSecret },
                    AllowedScopes = readonlyScopes,
                    RedirectUris = { new Uri(_redirectUri, "signin-oidc").ToString() },
                    FrontChannelLogoutUri = new Uri(_redirectUri,"signout-oidc").ToString(),
                    PostLogoutRedirectUris = { new Uri(_redirectUri,"signout-callback-oidc").ToString() },
                };
            }
        }
    }
}
