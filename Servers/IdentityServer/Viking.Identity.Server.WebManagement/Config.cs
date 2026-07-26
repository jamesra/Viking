using System.Collections.Generic;
using System.Linq;
using IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Viking.Identity.Server.WebManagement
{
    public class Config
    {
        //internal const string Secret = "CorrectHorseBatteryStaple"; 

        public const string AuthenticationSchemes = "Bearer, Introspection, Identity.Application";
         
        // scopes define the resources in your system
        public static IEnumerable<IdentityResource> GetIdentityResources()
        { 
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            };
        }

        public static IEnumerable<ApiResource> GetApiResources(VikingIdentityServerOptions options)
        {
            return new List<ApiResource>
            {
                new ApiResource("Viking.Annotation", "Viking Annotation API")
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name},
                    ApiSecrets = { new Secret(options.Secret.Sha256())}
                },
            };
        }
        /*
        public IEnumerable<ApiResource> GetApiResources()
        {
            var resources = _context.ResourceTypes.Include(rt => rt.Permissions);

            var apiResources = new List<ApiResource>();

            foreach (var r in resources)
            {
                var ar = new ApiResource(r.Id, r.Id)
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name },
                    ApiSecrets = { new Secret(Secret.Sha256()) },
                    Scopes = r.Permissions.Select(perm => $"{r.Id}.{perm.PermissionId}").ToList(),
                    Description = r.Description,
                };

                apiResources.Add(ar);
            }

            apiResources.AddRange(GetLegacyClientApiResources());

            return apiResources;
        }
        */
         
        public static IEnumerable<ApiScope> GetApiScopes(VikingIdentityServerOptions options)
        {
            return options.ApiScopes;
            /*
            return new List<ApiScope>
            {
                new ApiScope(name: "Viking.Annotation", displayName:"Access to Annotate a volume")
            };
            */
        } 

        /*
        public IEnumerable<ApiResource> GetApiScopes()
        {
            var resources = _context.ResourceTypes.Include(rt => rt.Permissions);

            var apiScopes = new List<ApiResource>();

            foreach (var r in resources)
            {
                var ar = new ApiResource(r.Id, r.Id)
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name },
                    ApiSecrets = { new Secret(Secret.Sha256()) },
                    Scopes = r.Permissions.Select(perm => $"{r.Id}.{perm.PermissionId}").ToList(),
                    Description = r.Description,
                };

                apiScopes.Add(ar);
            }

            apiScopes.AddRange(GetLegacyClientApiResources());

            return apiScopes;
        }
        */

        public static readonly string[] AnnotationScopes =
            new string[]
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile  
            };

        // clients want to access resources (aka scopes)
        public static IEnumerable<Client> GetClients(VikingIdentityServerOptions options)
        { 
            var allowedScopes = AnnotationScopes.Union(options.ApiScopes.Select(s => s.Name)).ToArray();

            // client credentials client
            return new List<Client>
            { 
                /*
                new Client
                {
                    ClientId = "Viking",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256()) //"My co-workers remove eyeballs from cute mammals for a living"
                    },
                    AllowedScopes = AnnotationScopes,
                }, 
                // resource owner password grant client
                new Client
                {
                    ClientId = "ro.viking",
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                     
                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
                    },
                    AllowedScopes = AnnotationScopes
                }, 
                */
                // API Client for token introspection
                new Client
                {
                    ClientId = "api",
                    ClientName = "API Resource",
                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
                    },
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    RedirectUris = { options.Authority + "signin-oidc" },
                    PostLogoutRedirectUris = { options.Authority + "signout-callback-oidc"},
                    AllowedScopes = allowedScopes
                },

                // OpenID Connect hybrid flow and client credentials client (MVC)
                new Client
                {
                    ClientId = "mvc",
                    ClientName = "Management Website Client",
                    //AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                    AllowedGrantTypes = GrantTypes.Code,

                    RequireConsent = false,

                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
                    },

                    RedirectUris = { options.Authority + "signin-oidc" },
                    PostLogoutRedirectUris = { options.Authority + "signout-callback-oidc"},
                    AllowedScopes = allowedScopes,
                    AllowOfflineAccess = true
                }
            };
        }
    }
}
