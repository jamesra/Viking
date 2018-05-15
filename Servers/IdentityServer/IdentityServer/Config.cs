using IdentityServer4;
using IdentityServer4.Models;
using System.Collections.Generic;
using System.Security.Claims;
using IdentityModel;

namespace IdentityServer
{
    public static class Config
    {
        public static string AdminRoleName = "Access Manager";
        // scopes define the resources in your system
        public static IEnumerable<IdentityResource> GetIdentityResources()
        { 
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()                
            };
        }

        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("Viking.Annotation", "Viking.Annotation")
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name, "Affiliation"},
                    ApiSecrets = { new Secret("secret".Sha256())}
                }
            };
        }

        // clients want to access resources (aka scopes)
        public static IEnumerable<Client> GetClients()
        {
            string[] AnnotationScopes =
            {
                "Viking.Annotation",
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile
            };

            // client credentials client
            return new List<Client>
            {
                new Client
                {
                    ClientId = "Viking",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    AllowedScopes = AnnotationScopes
                    
                },

                // resource owner password grant client
                new Client
                {
                    ClientId = "ro.viking",
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                     
                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    AllowedScopes = AnnotationScopes

},

                // OpenID Connect hybrid flow and client credentials client (MVC)
                new Client
                {
                    ClientId = "mvc",
                    ClientName = "MVC Client",
                    //AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                    AllowedGrantTypes = GrantTypes.Implicit,

                    RequireConsent = false,

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },

                    RedirectUris = { "http://localhost:5001/signin-oidc" },
                    PostLogoutRedirectUris = { "http://localhost:5001/signout-callback-oidc" },

                    AllowedScopes = AnnotationScopes,
                    AllowOfflineAccess = true
                }
            };
        }
    }
}
