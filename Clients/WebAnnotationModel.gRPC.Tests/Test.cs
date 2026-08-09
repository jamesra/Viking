using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using Grpc.Core;
using Grpc.Net.Client;
using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Google.Protobuf.WellKnownTypes;

namespace WebAnnotationModel.gRPC.Tests
{
    public class IdentityClientSettings
    {
        public string ClientId { get; set; }
        public string Scope { get; set; }
        public string ClientSecret { get; set; }
    }

    public class UserIdentity
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    /// <summary>
    /// Integration tests against identity-devtest (:5020) + grpc-annotation-service (:5011)
    /// + AnnotationTest SQL. Start with:
    ///   Servers/GrpcAnnotationService/scripts/Start-AnnotationTestStack.ps1 -ApplySchema -Build
    /// Credentials match Servers/IdentityServer/DevTest/Config.cs (testuser / Testing123! / ro.viking).
    /// </summary>
    public class Tests
    {
        private string _identityServerUrl;
        private string _grpcServerUrl;
        private IdentityClientSettings _identityClient;
        private UserIdentity _userIdentity;

        [SetUp]
        public void Setup()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables()
                .Build();

            _identityServerUrl = config["IdentityServer:Endpoint"];
            _grpcServerUrl = config["GrpcServer:Endpoint"];
            _identityClient = config.GetSection("IdentityClient").Get<IdentityClientSettings>();
            _userIdentity = config.GetSection("TestIdentity").Get<UserIdentity>();

            Assert.That(_identityServerUrl, Is.Not.Null.And.Not.Empty);
            Assert.That(_grpcServerUrl, Is.Not.Null.And.Not.Empty);
            Assert.That(_identityClient, Is.Not.Null);
            Assert.That(_userIdentity, Is.Not.Null);
            Assert.That(_userIdentity.UserName, Is.EqualTo("testuser"));
        }

        [Test]
        public async Task GetLastModifiedLocation_WithDevTestToken_ReturnsLocation()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var reply = await client.GetLastModifiedLocationAsync(new GetLastModifiedLocationRequest());

            Assert.That(reply, Is.Not.Null);
            Assert.That(reply.Result, Is.Not.Null);
            Assert.That(reply.Result.Id, Is.GreaterThan(0));
            Assert.That(reply.Result.MosaicShape, Is.Not.Null);
        }

        [Test]
        public async Task GetLocationByID_SeedLocation_ReturnsId1()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            const long seedId = 1;
            var reply = await client.GetLocationByIDAsync(new GetLocationByIDRequest { Id = seedId });

            Assert.That(reply.Result, Is.Not.Null);
            Assert.That(reply.Result.Id, Is.EqualTo(seedId));
            Assert.That(reply.Result.ParentId, Is.EqualTo(1));
        }

        [Test]
        public async Task StreamLocationChangesInMosaicRegion_ReturnsSeedLocation()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            // Seed location from minimal-schema.sql: Z=1, POINT (100 200)
            var request = new GetLocationChangesInMosaicRegionRequest
            {
                Z = 1,
                MinRadius = 0,
                Region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
                {
                    Text = "POLYGON((90 190, 110 190, 110 210, 90 210, 90 190))"
                }
            };

            using var call = client.StreamLocationChangesInMosaicRegion(request);
            var locations = new System.Collections.Generic.List<Location>();
            var sawLast = false;
            Timestamp queryTime = null;

            await foreach (var chunk in call.ResponseStream.ReadAllAsync())
            {
                if (chunk.QueryExecutedTime != null)
                    queryTime = chunk.QueryExecutedTime;
                locations.AddRange(chunk.Locations);
                if (chunk.IsLast)
                    sawLast = true;
            }

            Assert.That(sawLast, Is.True);
            Assert.That(queryTime, Is.Not.Null);
            Assert.That(locations, Has.Some.Matches<Location>(l => l.Id == 1));
        }

        [Test]
        public async Task GetStructures_ReturnsAtLeastSeedStructure()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateStructures.AnnotateStructuresClient(channel);

            var reply = await client.GetStructuresAsync(new GetStructuresRequest());

            Assert.That(reply.Results, Is.Not.Null);
            Assert.That(reply.Results.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(reply.Results, Has.Some.Matches<Structure>(s => s.Id == 1));
        }

        [Test]
        public async Task GetStructureTypes_ReturnsAtLeastSeedType()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var reply = await client.GetStructureTypesAsync(new GetStructureTypesRequest());

            Assert.That(reply.Results, Is.Not.Null);
            Assert.That(reply.Results.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(reply.Results, Has.Some.Matches<StructureType>(t => t.Id == 1));
        }

        [Test]
        public async Task CreateGetDeleteLocation_Roundtrip()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);
            var locationsClient = new AnnotateLocations.AnnotateLocationsClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var createdType = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"RoundtripType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "RT",
                    Color = 0x00FF00,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });
            Assert.That(createdType.Result.Id, Is.GreaterThan(0));

            long? structureId = null;
            long? locationId = null;
            try
            {
                var createdStructure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "roundtrip",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = createdStructure.NewStructure.Id;
                Assert.That(structureId, Is.GreaterThan(0));

                var createLoc = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                {
                    Obj = new Location
                    {
                        ParentId = structureId.Value,
                        Section = 2,
                        MosaicPosition = new AnnotationPoint { X = 10.5, Y = 20.5 },
                        VolumePosition = new AnnotationPoint { X = 10.5, Y = 20.5 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (10.5 20.5)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (10.5 20.5)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                locationId = createLoc.Result.Id;
                Assert.That(locationId, Is.GreaterThan(0));

                var fetched = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest
                {
                    Id = locationId.Value
                });
                Assert.That(fetched.Result.Id, Is.EqualTo(locationId.Value));
                Assert.That(fetched.Result.ParentId, Is.EqualTo(structureId.Value));
                Assert.That(fetched.Result.Section, Is.EqualTo(2));

                var deleteResponse = await locationsClient.UpdateAsync(new UpdateLocationsRequest
                {
                    Locations =
                    {
                        new LocationChangeRequest { Delete = locationId.Value }
                    }
                });
                Assert.That(deleteResponse.Results, Has.Count.EqualTo(1));
                Assert.That(deleteResponse.Results[0].Success, Is.True);
                Assert.That(deleteResponse.Results[0].DeletedId, Is.EqualTo(locationId.Value));
                locationId = null;

                var ex = Assert.ThrowsAsync<RpcException>(async () =>
                    await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest
                    {
                        Id = deleteResponse.Results[0].DeletedId
                    }));
                Assert.That(ex.StatusCode, Is.EqualTo(StatusCode.NotFound));
            }
            finally
            {
                if (locationId.HasValue)
                {
                    try
                    {
                        await locationsClient.UpdateAsync(new UpdateLocationsRequest
                        {
                            Locations = { new LocationChangeRequest { Delete = locationId.Value } }
                        });
                    }
                    catch (RpcException) { /* best-effort cleanup */ }
                }

                if (structureId.HasValue)
                {
                    try
                    {
                        await structuresClient.UpdateAsync(new UpdateStructuresRequest
                        {
                            Objs = { new StructureChangeRequest { Delete = structureId.Value } }
                        });
                    }
                    catch (RpcException) { /* best-effort cleanup */ }
                }

                try
                {
                    await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                    {
                        Objs = { new StructureTypeChangeRequest { Delete = createdType.Result.Id } }
                    });
                }
                catch (RpcException) { /* best-effort cleanup */ }
            }
        }

        [Test]
        public async Task GetStructureLocations_SeedStructure_ReturnsSeedLocation()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var reply = await client.GetStructureLocationsAsync(new GetStructureLocationsRequest { StructureId = 1 });

            Assert.That(reply.Results, Is.Not.Null);
            Assert.That(reply.Results.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(reply.Results, Has.Some.Matches<Location>(l => l.Id == 1 && l.ParentId == 1));
        }

        [Test]
        public async Task CreateStructureLink_Roundtrip()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var createdType = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"SLinkType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "SL",
                    Color = 0xFF00FF,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? aId = null;
            long? bId = null;
            try
            {
                async Task<long> CreateStructure(string label)
                {
                    var created = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                    {
                        NewStructure = new Structure
                        {
                            TypeId = createdType.Result.Id,
                            Label = label,
                            Confidence = 0.5,
                            Created = now,
                            LastModified = now,
                            Username = _userIdentity.UserName,
                        }
                    });
                    return created.NewStructure.Id;
                }

                aId = await CreateStructure("link-a");
                bId = await CreateStructure("link-b");

                var link = await structuresClient.CreateStructureLinkAsync(new CreateStructureLinkRequest
                {
                    NewLink = new StructureLink
                    {
                        SourceId = aId.Value,
                        TargetId = bId.Value,
                        Bidirectional = true,
                    }
                });
                Assert.That(link.Result, Is.Not.Null);
                Assert.That(link.Result.SourceId, Is.EqualTo(aId.Value));
                Assert.That(link.Result.TargetId, Is.EqualTo(bId.Value));

                var linked = await structuresClient.GetLinkedStructuresAsync(new GetLinkedStructuresRequest
                {
                    Id = aId.Value
                });
                Assert.That(linked.Results, Has.Some.Matches<StructureLink>(l =>
                    (l.SourceId == aId.Value && l.TargetId == bId.Value) ||
                    (l.SourceId == bId.Value && l.TargetId == aId.Value)));
            }
            finally
            {
                foreach (var id in new[] { aId, bId })
                {
                    if (!id.HasValue) continue;
                    try
                    {
                        await structuresClient.UpdateAsync(new UpdateStructuresRequest
                        {
                            Objs = { new StructureChangeRequest { Delete = id.Value } }
                        });
                    }
                    catch (RpcException) { }
                }

                try
                {
                    await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                    {
                        Objs = { new StructureTypeChangeRequest { Delete = createdType.Result.Id } }
                    });
                }
                catch (RpcException) { }
            }
        }

        [Test]
        public async Task StreamAnnotationsInMosaicRegion_ReturnsSeedLocation()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var request = new GetAnnotationsInMosaicRegionRequest
            {
                Z = 1,
                MinRadius = 0,
                Region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
                {
                    Text = "POLYGON((0 0, 200 0, 200 300, 0 300, 0 0))"
                }
            };

            using var call = client.StreamAnnotationsInMosaicRegion(request);
            var locations = new List<Location>();
            var sawLast = false;
            while (await call.ResponseStream.MoveNext())
            {
                var chunk = call.ResponseStream.Current;
                if (chunk.Partial != null)
                    locations.AddRange(chunk.Partial.Locations);
                if (chunk.IsLast)
                    sawLast = true;
            }

            Assert.That(sawLast, Is.True);
            Assert.That(locations, Has.Some.Matches<Location>(l => l.Id == 1));
        }

        [Test]
        public async Task CreateLocationLink_Roundtrip()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var locationsClient = new AnnotateLocations.AnnotateLocationsClient(channel);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var createdType = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"LinkType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "LT",
                    Color = 0x0000FF,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locA = null;
            long? locB = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "link-test",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = structure.NewStructure.Id;

                async Task<long> CreatePoint(double x, double y)
                {
                    var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                    {
                        Obj = new Location
                        {
                            ParentId = structureId.Value,
                            Section = 3,
                            MosaicPosition = new AnnotationPoint { X = x, Y = y },
                            VolumePosition = new AnnotationPoint { X = x, Y = y },
                            MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = $"POINT ({x} {y})" },
                            VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = $"POINT ({x} {y})" },
                            TypeCode = AnnotationType.Circle,
                            Created = now,
                            LastModified = now,
                            Username = _userIdentity.UserName,
                        }
                    });
                    return created.Result.Id;
                }

                locA = await CreatePoint(1, 1);
                locB = await CreatePoint(2, 2);

                var link = await locationsClient.CreateLocationLinkAsync(new CreateLocationLinkRequest
                {
                    SourceId = locA.Value,
                    TargetId = locB.Value
                });
                Assert.That(link, Is.Not.Null);

                var linkedFromA = await locationsClient.GetLinkedLocationsAsync(new GetLinkedLocationsRequest
                {
                    Id = locA.Value
                });
                Assert.That(linkedFromA.Results, Does.Contain(locB.Value));

                await locationsClient.DeleteLocationLinkAsync(new DeleteLocationLinkRequest
                {
                    SourceId = locA.Value,
                    TargetId = locB.Value
                });

                var linkedAfterDelete = await locationsClient.GetLinkedLocationsAsync(new GetLinkedLocationsRequest
                {
                    Id = locA.Value
                });
                Assert.That(linkedAfterDelete.Results, Does.Not.Contain(locB.Value));
            }
            finally
            {
                if (locA.HasValue && locB.HasValue)
                {
                    try
                    {
                        await locationsClient.DeleteLocationLinkAsync(new DeleteLocationLinkRequest
                        {
                            SourceId = locA.Value,
                            TargetId = locB.Value
                        });
                    }
                    catch (RpcException) { }
                }

                foreach (var id in new[] { locA, locB })
                {
                    if (!id.HasValue) continue;
                    try
                    {
                        await locationsClient.UpdateAsync(new UpdateLocationsRequest
                        {
                            Locations = { new LocationChangeRequest { Delete = id.Value } }
                        });
                    }
                    catch (RpcException) { }
                }

                if (structureId.HasValue)
                {
                    try
                    {
                        await structuresClient.UpdateAsync(new UpdateStructuresRequest
                        {
                            Objs = { new StructureChangeRequest { Delete = structureId.Value } }
                        });
                    }
                    catch (RpcException) { }
                }

                try
                {
                    await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                    {
                        Objs = { new StructureTypeChangeRequest { Delete = createdType.Result.Id } }
                    });
                }
                catch (RpcException) { }
            }
        }

        private async Task<string> RequestAccessTokenAsync()
        {
            using var http = new HttpClient { BaseAddress = new Uri(_identityServerUrl) };
            var disco = await http.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = _identityServerUrl,
                Policy =
                {
                    RequireHttps = false,
                    // DevTest IssuerUri is host.docker.internal; allow localhost aliases in CI.
                    ValidateIssuerName = false
                }
            });
            Assert.That(disco.IsError, Is.False, $"{disco.Error} ({disco.Exception?.Message})");

            var token = await http.RequestPasswordTokenAsync(new PasswordTokenRequest
            {
                Address = disco.TokenEndpoint,
                UserName = _userIdentity.UserName,
                Password = _userIdentity.Password,
                ClientId = _identityClient.ClientId,
                ClientSecret = _identityClient.ClientSecret,
                Scope = _identityClient.Scope,
            });

            Assert.That(token.IsError, Is.False, token.Error);
            Assert.That(token.AccessToken, Is.Not.Null.And.Not.Empty);
            return token.AccessToken;
        }

        private GrpcChannel CreateAuthenticatedChannel(string accessToken)
        {
            // Test stack serves h2c on :5010 (no TLS cert in the container).
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            var socketsHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                SslOptions =
                {
                    RemoteCertificateValidationCallback = static (_, _, _, _) => true
                }
            };

            // Inject Bearer on the HTTP layer so auth works with InsecureCredentials
            // (CallCredentials cannot be composed with InsecureCredentials on older Grpc.Net.Client).
            var handler = new BearerTokenHandler(accessToken) { InnerHandler = socketsHandler };

            return GrpcChannel.ForAddress(_grpcServerUrl, new GrpcChannelOptions
            {
                HttpHandler = handler,
                Credentials = ChannelCredentials.Insecure
            });
        }

        private sealed class BearerTokenHandler : DelegatingHandler
        {
            private readonly string _accessToken;

            public BearerTokenHandler(string accessToken) => _accessToken = accessToken;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
                return base.SendAsync(request, cancellationToken);
            }
        }
    }
}
