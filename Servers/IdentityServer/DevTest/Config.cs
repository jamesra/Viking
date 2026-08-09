// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using System.Collections.Generic;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Test;

namespace DevTest
{
    /// <summary>
    /// Throwaway, in-memory Identity Server used only to run the gRPC annotation
    /// integration tests locally, without touching the production identity.codepharm.net
    /// instance. Not for any real deployment: secrets below are intentionally simple
    /// and the token store is in-memory (nothing survives a restart).
    /// </summary>
    public static class Config
    {
        // Must match the introspection secret handed to GrpcAnnotationService via
        // IdentityServer__ClientId / IdentityServer__ClientSecret (see
        // Servers/GrpcAnnotationService/config-template/build/.env.Docker).
        public const string GrpcAnnotationApiResourceName = "grpc-annotation";
        public const string GrpcAnnotationApiSecret = "DevTestIntrospectionSecret";

        // Must match Clients/WebAnnotationModel.gRPC.Tests appsettings.json (ro.viking / CorrectHorseBatteryStaple).
        public const string ResourceOwnerClientId = "ro.viking";
        public const string ResourceOwnerClientSecret = "CorrectHorseBatteryStaple";

        // Must match Clients/WebAnnotationModel.gRPC.Tests secrets.json (TestIdentity section).
        public const string TestUserName = "testuser";
        public const string TestUserPassword = "Testing123!";

        public static IEnumerable<IdentityResource> GetIdentityResources() =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
            };

        public static IEnumerable<ApiScope> GetApiScopes() =>
            new List<ApiScope>
            {
                new ApiScope("Viking.Annotation", "Viking Annotation gRPC API"),
            };

        // The introspection endpoint authenticates callers using the API resource's
        // own secret (not a client secret) - this is what GrpcAnnotationService's
        // OAuth2Introspection ClientId/ClientSecret map to.
        public static IEnumerable<ApiResource> GetApiResources() =>
            new List<ApiResource>
            {
                new ApiResource(GrpcAnnotationApiResourceName, "Viking Annotation gRPC Service")
                {
                    ApiSecrets = { new Secret(GrpcAnnotationApiSecret.Sha256()) },
                    Scopes = { "Viking.Annotation" },
                },
            };

        public static IEnumerable<Client> GetClients() =>
            new List<Client>
            {
                // Resource owner password grant client used by the test suites to
                // mint tokens on behalf of TestUserName/TestUserPassword.
                new Client
                {
                    ClientId = ResourceOwnerClientId,
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,

                    ClientSecrets = { new Secret(ResourceOwnerClientSecret.Sha256()) },

                    AllowedScopes = { "openid", "profile", "Viking.Annotation" },
                    AccessTokenType = AccessTokenType.Reference,
                },
            };

        public static List<TestUser> GetTestUsers() =>
            new List<TestUser>
            {
                new TestUser
                {
                    SubjectId = "1",
                    Username = TestUserName,
                    Password = TestUserPassword,
                },
            };
    }
}
