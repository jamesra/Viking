// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace DevTest
{
    public static class Config
    {
        // scopes define the resources in your system
        public static IEnumerable<IdentityResource> GetIdentityResources()
        {
            return new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            };
        }

        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("api1", "My API"),

                // The gRPC annotation service (Servers/GrpcAnnotationService) requires this scope and also
                // accepts the Viking.Annotation.API audience. Its introspection client below authenticates
                // with the secret that docker-env-template.txt ships.
                new ApiResource("Viking.Annotation.API", "Viking annotation service")
                {
                    Scopes = { "Viking.Annotation" }
                }
            };
        }

        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return new List<ApiScope>
            {
                new ApiScope("api1", "My API"),
                new ApiScope("Viking.Annotation", "Viking annotation service")
            };
        }

        // clients want to access resources (aka scopes)
        public static IEnumerable<Client> GetClients()
        {
            // client credentials client
            return new List<Client>
            {
                new Client
                {
                    ClientId = "client",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    AllowedScopes = { "api1" }
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
                    AllowedScopes = { "api1", "Viking.Annotation" }
                },

                // Introspection client of the gRPC annotation service (IdentityServer__ClientId/ClientSecret
                // in the container env file). Holding the scope lets it introspect reference tokens that carry it.
                new Client
                {
                    ClientId = "grpc-annotation",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,

                    ClientSecrets =
                    {
                        new Secret("DevTestIntrospectionSecret".Sha256())
                    },
                    AllowedScopes = { "Viking.Annotation" }
                },

                // OpenID Connect hybrid flow and client credentials client (MVC)
                new Client
                {
                    ClientId = "mvc",
                    ClientName = "MVC Client",
                    AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,

                    RequireConsent = true,

                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },

                    RedirectUris = { "http://localhost:5002/signin-oidc" },
                    PostLogoutRedirectUris = { "http://localhost:5002/signout-callback-oidc" },

                    AllowedScopes =
                    {
                        IdentityServerConstants.StandardScopes.OpenId,
                        IdentityServerConstants.StandardScopes.Profile,
                        "api1"
                    },
                    AllowOfflineAccess = true
                }
            };
        }
    }
}