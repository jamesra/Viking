using System;
using IdentityModel;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using System.Collections.Generic;
using System.Linq;
using Viking.Identity.Server;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;


namespace Viking.Identity
{   

    public class Config
    {
        //internal const string Secret = "CorrectHorseBatteryStaple"; 

        public const string AuthenticationSchemes = "Bearer, Introspection, Cookies, idsrv";

        

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
                new ApiResource("Viking.Annotation.API", "Viking Annotation API")
                {
                    UserClaims = { JwtClaimTypes.Role, JwtClaimTypes.Id, JwtClaimTypes.Name},
                    ApiSecrets = { new Secret(options.Secret.Sha256())},
                    Scopes = options.ApiScopes.Select(s => s.Name).ToList()
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
            var allowedScopes = AnnotationScopes.Union(options.ApiScopes.Select(s => s.Name)).Distinct().ToArray();

            // client credentials client
            var result =  new List<Client>
            { 
                new Client
                {
                    ClientId = "Viking",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
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
                        new Secret("ro.viking.secret".Sha256())
                    },
                    AllowedScopes = AnnotationScopes,                    
                    AccessTokenType = AccessTokenType.Reference
                }, 
                // API Client for token introspection, used by WebApi project to validate tokens
                new Client
                {
                    ClientId = "api",
                    ClientName = "API Resource",
                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
                    },
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowedScopes = allowedScopes,
                    AccessTokenType = AccessTokenType.Reference
                },
                // OpenID Connect hybrid flow and client credentials client (MVC)
                new Client
                {
                    ClientId = "mvc",
                    ClientName = "MVC Client",
                    //AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                    AllowedGrantTypes = GrantTypes.Code,

                    RequireConsent = false,

                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
                    },

                    RedirectUris = { $"{options.Authority}signin-oidc" },
                    FrontChannelLogoutUri = $"{options.Authority}signout-oidc",
                    PostLogoutRedirectUris = { $"{options.Authority}signout-callback-oidc" },
                    AllowedScopes = allowedScopes,
                    AllowOfflineAccess = true
                },
                // OpenID Connect hybrid flow and client credentials client (MVC)
                new Client
                {
                    ClientId = "web", 
                    //AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                    AllowedGrantTypes = GrantTypes.Code,

                    RequireConsent = false,

                    ClientSecrets =
                    {
                        new Secret(options.Secret.Sha256())
                    },

                    RedirectUris = { $"{options.Authority}signin-oidc" },
                    PostLogoutRedirectUris = { $"{options.Authority}signout-callback-oidc"},
                    AllowedScopes = allowedScopes,
                    AllowOfflineAccess = true
                }
            };

            return result;
        }
    }
}
