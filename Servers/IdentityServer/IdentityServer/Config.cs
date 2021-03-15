using IdentityServer4;
using IdentityServer4.Models;
using System.Collections.Generic;
using System.Security.Claims;
using IdentityModel;


namespace IdentityServer
{ 

    public static class Config
    {
        private const string Secret = "CorrectHorseBatteryStaple";
        public const string AdminRoleName = "Administrator";

        public const string AccessManagerPolicy = "Access Manager";

        public const string GroupAccessManagerPermission = "Access Manager";

        /// <summary>
        /// Permissions defined in the global group are available to all groups (All child groups?)
        /// </summary>
        public const long AdminGroupId = -1;
        public const string AdminGroupName = "Administrators";

        public const long EveryoneGroupId = 0;
        public const string EveryoneGroupName = "Everyone";

        public const string GroupResourceType = "Group";
        public const string VolumeResourceType = "Volume";

        public static string AdminRoleId { get; set; }

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
                new ApiResource("Viking.Annotation", "Viking Annotation API")
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name, "Affiliation"},
                    ApiSecrets = { new Secret(Secret.Sha256())},
                }
            };
        }

        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return new List<ApiScope>
            {
                new ApiScope(name: "Viking.Annotation", displayName:"Access to Annotate a volume")
            };
        }

        public static readonly string[] AnnotationScopes =
            new string[]
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                "Viking.Annotation"
            };

        // clients want to access resources (aka scopes)
        public static IEnumerable<Client> GetClients()
        { 
            // client credentials client
            return new List<Client>
            {
                new Client
                {
                    ClientId = "Viking",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret(Secret.Sha256()) //"My co-workers remove eyeballs from cute mammals for a living"
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
                        new Secret(Secret.Sha256())
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
                        new Secret(Secret.Sha256())
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
