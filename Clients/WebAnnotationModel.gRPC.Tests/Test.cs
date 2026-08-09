using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net;
using Grpc.Core;
using Grpc.Net.Client;
using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using Viking.AnnotationServiceTypes.gRPC.V1.Protos;
using Viking.AnnotationServiceTypes.Interfaces;
using Google.Protobuf.WellKnownTypes;
using WebAnnotationModel.gRPC.Converters;
using WebAnnotationModel.Objects;

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
    /// Integration tests against identity-devtest (:5020) + grpc-annotation-service (:5010)
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
        public void StructureClientToServerConverter_PreservesTypeId()
        {
            var src = new StructureObj(5, 9)
            {
                Label = "typed",
                Confidence = 0.5,
                DBAction = DBACTION.UPDATE,
            };

            var converted = new StructureClientToServerConverter().Convert(src);
            Assert.That(converted.TypeId, Is.EqualTo(9));
            Assert.That(converted.Label, Is.EqualTo("typed"));

            var change = (StructureChangeRequest)converted;
            Assert.That(change.Update, Is.Not.Null);
            Assert.That(change.Update.TypeId, Is.EqualTo(9));
        }

        [Test]
        public void StructureTypeClientToServerConverter_PreservesDbAction()
        {
            var src = new StructureTypeObj(3)
            {
                Name = "typed",
                Code = "T",
                Color = 0x112233,
                DBAction = DBACTION.UPDATE,
            };

            var converted = new StructureTypeClientToServerConverter().Convert(src);
            Assert.That(((IChangeAction)converted).DBAction, Is.EqualTo(DBACTION.UPDATE));

            var change = (StructureTypeChangeRequest)converted;
            Assert.That(change.Update, Is.Not.Null);
            Assert.That(change.Update.Id, Is.EqualTo(3));
            Assert.That(change.Update.Name, Is.EqualTo("typed"));
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

                await structuresClient.DeleteStructureLinkAsync(new DeleteStructureLinkRequest
                {
                    SourceId = aId.Value,
                    TargetId = bId.Value
                });
                var afterDelete = await structuresClient.GetLinkedStructuresAsync(new GetLinkedStructuresRequest
                {
                    Id = aId.Value
                });
                Assert.That(afterDelete.Results, Has.None.Matches<StructureLink>(l =>
                    l.SourceId == aId.Value && l.TargetId == bId.Value));
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
        public async Task GetNetworkedStructures_OneHopViaChildLinks()
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
                    Name = $"NetType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "NT",
                    Color = 0x00FF00,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? parentA = null, parentB = null, childA = null, childB = null;
            try
            {
                async Task<long> CreateStructure(string label, long? parentId = null)
                {
                    var structure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = label,
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    };
                    if (parentId.HasValue)
                        structure.ParentId = parentId.Value;

                    var created = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                    {
                        NewStructure = structure
                    });
                    return created.NewStructure.Id;
                }

                parentA = await CreateStructure("net-parent-a");
                parentB = await CreateStructure("net-parent-b");
                childA = await CreateStructure("net-child-a", parentA);
                childB = await CreateStructure("net-child-b", parentB);

                await structuresClient.CreateStructureLinkAsync(new CreateStructureLinkRequest
                {
                    NewLink = new StructureLink
                    {
                        SourceId = childA.Value,
                        TargetId = childB.Value,
                        Bidirectional = true,
                    }
                });

                var network = await structuresClient.GetNetworkedStructuresAsync(new GetNetworkedStructuresRequest
                {
                    Ids = { parentA.Value },
                    NumHops = 1
                });
                Assert.That(network.Results, Does.Contain(parentA.Value));
                Assert.That(network.Results, Does.Contain(parentB.Value));

                var links = await structuresClient.GetStructureLinksInNetworkAsync(new GetStructureLinksInNetworkRequest
                {
                    Ids = { parentA.Value },
                    NumHops = 1
                });
                Assert.That(links.Results, Has.Some.Matches<StructureLink>(l =>
                    l.SourceId == childA.Value && l.TargetId == childB.Value));

                var children = await structuresClient.GetChildStructuresInNetworkAsync(new GetChildStructuresInNetworkRequest
                {
                    Ids = { parentA.Value },
                    NumHops = 1
                });
                Assert.That(children.Results.Select(s => s.Id), Is.SupersetOf(new[] { childA.Value, childB.Value }));
            }
            finally
            {
                foreach (var id in new[] { childA, childB, parentA, parentB })
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
        public async Task GetChildStructures_ReturnsChildWithParentId()
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
                    Name = $"ChildType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "CH",
                    Color = 0xABCDEF,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? parentId = null;
            long? childId = null;
            try
            {
                var parent = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "parent",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                parentId = parent.NewStructure.Id;

                var child = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        ParentId = parentId.Value,
                        Label = "child",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                childId = child.NewStructure.Id;
                Assert.That(child.NewStructure.ParentId, Is.EqualTo(parentId.Value));

                var children = await structuresClient.GetChildStructuresAsync(new GetChildStructuresRequest
                {
                    StructureId = parentId.Value
                });
                Assert.That(children.Results, Has.Some.Matches<Structure>(s =>
                    s.Id == childId.Value && s.ParentId == parentId.Value));
            }
            finally
            {
                foreach (var id in new[] { childId, parentId })
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
        public async Task GetUnfinishedLocationsWithPosition_LinkedPair_ReturnsCoords()
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
                    Name = $"UnfinPos-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "UP",
                    Color = 0x778899,
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
                        Label = "unfin-pos",
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
                            Section = 6,
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

                locA = await CreatePoint(70.5, 80.5);
                locB = await CreatePoint(90.25, 100.75);
                await locationsClient.CreateLocationLinkAsync(new CreateLocationLinkRequest
                {
                    SourceId = locA.Value,
                    TargetId = locB.Value
                });

                var unfinished = await structuresClient.GetUnfinishedLocationsWithPositionAsync(
                    new GetUnfinishedLocationsWithPositionRequest { Id = structureId.Value });

                Assert.That(unfinished.Results, Has.Some.Matches<Viking.AnnotationServiceTypes.gRPC.V1.Protos.LocationPositionOnly>(t =>
                    t.Id == locA.Value &&
                    t.Position != null &&
                    Math.Abs(t.Position.X - 70.5) < 0.01 &&
                    Math.Abs(t.Position.Y - 80.5) < 0.01 &&
                    t.Position.Z == 6));
                Assert.That(unfinished.Results, Has.Some.Matches<Viking.AnnotationServiceTypes.gRPC.V1.Protos.LocationPositionOnly>(t =>
                    t.Id == locB.Value &&
                    t.Position != null &&
                    Math.Abs(t.Position.X - 90.25) < 0.01 &&
                    Math.Abs(t.Position.Y - 100.75) < 0.01));
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

        [Test]
        public async Task GetLocationChanges_ModifiedAfter_ReturnsOnlyUpdated()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var locationsClient = new AnnotateLocations.AnnotateLocationsClient(channel);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            const long section = 17;
            var createdType = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"ModAfter-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "MA",
                    Color = 0x445566,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locationId = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "mod-after",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = structure.NewStructure.Id;

                var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                {
                    Obj = new Location
                    {
                        ParentId = structureId.Value,
                        Section = section,
                        MosaicPosition = new AnnotationPoint { X = 30, Y = 31 },
                        VolumePosition = new AnnotationPoint { X = 30, Y = 31 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (30 31)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (30 31)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                locationId = created.Result.Id;
                // Watermark from the create row itself; SQL DATETIME has coarse precision, so
                // QueryExecutedTime can round equal to LastModified and miss the next write.
                var afterCreate = created.Result.LastModified;

                await Task.Delay(1100);

                var update = await locationsClient.UpdateAsync(new UpdateLocationsRequest
                {
                    Locations =
                    {
                        new LocationChangeRequest
                        {
                            Update = new Location
                            {
                                Id = locationId.Value,
                                ParentId = structureId.Value,
                                Section = section,
                                MosaicPosition = new AnnotationPoint { X = 40, Y = 41 },
                                VolumePosition = new AnnotationPoint { X = 40, Y = 41 },
                                MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (40 41)" },
                                VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (40 41)" },
                                TypeCode = AnnotationType.Circle,
                                Terminal = true,
                                Created = now,
                                LastModified = Timestamp.FromDateTime(DateTime.UtcNow),
                                Username = _userIdentity.UserName,
                            }
                        }
                    }
                });
                Assert.That(update.Results, Has.Count.EqualTo(1));
                Assert.That(update.Results[0].Success, Is.True);
                Assert.That(update.Results[0].Updated.Terminal, Is.True);

                var incremental = await locationsClient.GetLocationChangesAsync(new GetLocationChangesRequest
                {
                    Section = section,
                    ModifiedAfterThisUtcTime = afterCreate
                });
                Assert.That(incremental.Results, Has.Some.Matches<Location>(l =>
                    l.Id == locationId.Value && l.Terminal));

                var farFuture = await locationsClient.GetLocationChangesAsync(new GetLocationChangesRequest
                {
                    Section = section,
                    ModifiedAfterThisUtcTime = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(1))
                });
                Assert.That(farFuture.Results, Has.None.Matches<Location>(l => l.Id == locationId.Value));
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

        [Test]
        public async Task UpdateLocation_OmittingParentId_PreservesParent()
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
                    Name = $"KeepParent-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "KP",
                    Color = 0x203040,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locationId = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "keep-parent",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = structure.NewStructure.Id;

                var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                {
                    Obj = new Location
                    {
                        ParentId = structureId.Value,
                        Section = 19,
                        MosaicPosition = new AnnotationPoint { X = 50, Y = 51 },
                        VolumePosition = new AnnotationPoint { X = 50, Y = 51 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (50 51)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (50 51)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                locationId = created.Result.Id;

                var update = await locationsClient.UpdateAsync(new UpdateLocationsRequest
                {
                    Locations =
                    {
                        new LocationChangeRequest
                        {
                            Update = new Location
                            {
                                Id = locationId.Value,
                                Section = 19,
                                MosaicPosition = new AnnotationPoint { X = 60, Y = 61 },
                                VolumePosition = new AnnotationPoint { X = 60, Y = 61 },
                                MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (60 61)" },
                                VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (60 61)" },
                                TypeCode = AnnotationType.Circle,
                                Created = now,
                                LastModified = Timestamp.FromDateTime(DateTime.UtcNow),
                                Username = _userIdentity.UserName,
                            }
                        }
                    }
                });
                Assert.That(update.Results[0].Success, Is.True);
                Assert.That(update.Results[0].Updated.ParentId, Is.EqualTo(structureId.Value));

                var fetched = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest
                {
                    Id = locationId.Value
                });
                Assert.That(fetched.Result.ParentId, Is.EqualTo(structureId.Value));
                Assert.That(fetched.Result.MosaicPosition.X, Is.EqualTo(60).Within(0.01));
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

        [Test]
        public async Task UpdateLocation_AndNumberOfLocations_Roundtrip()
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
                    Name = $"UpdLoc-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "UL",
                    Color = 0x99AABB,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locationId = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "upd-loc",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = structure.NewStructure.Id;

                var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                {
                    Obj = new Location
                    {
                        ParentId = structureId.Value,
                        Section = 7,
                        MosaicPosition = new AnnotationPoint { X = 11, Y = 12 },
                        VolumePosition = new AnnotationPoint { X = 11, Y = 12 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (11 12)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (11 12)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                locationId = created.Result.Id;

                var countBefore = await structuresClient.NumberOfLocationsAsync(new NumberOfLocationsRequest
                {
                    Id = structureId.Value
                });
                Assert.That(countBefore.Result, Is.EqualTo(1));

                var update = await locationsClient.UpdateAsync(new UpdateLocationsRequest
                {
                    Locations =
                    {
                        new LocationChangeRequest
                        {
                            Update = new Location
                            {
                                Id = locationId.Value,
                                ParentId = structureId.Value,
                                Section = 7,
                                MosaicPosition = new AnnotationPoint { X = 21, Y = 22 },
                                VolumePosition = new AnnotationPoint { X = 21, Y = 22 },
                                MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (21 22)" },
                                VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (21 22)" },
                                TypeCode = AnnotationType.Circle,
                                Terminal = true,
                                Created = now,
                                LastModified = Timestamp.FromDateTime(DateTime.UtcNow),
                                Username = _userIdentity.UserName,
                            }
                        }
                    }
                });
                Assert.That(update.Results, Has.Count.EqualTo(1));
                Assert.That(update.Results[0].Success, Is.True);
                Assert.That(update.Results[0].Updated, Is.Not.Null);
                Assert.That(update.Results[0].Updated.Terminal, Is.True);

                var fetched = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest
                {
                    Id = locationId.Value
                });
                Assert.That(fetched.Result.Terminal, Is.True);
                Assert.That(fetched.Result.MosaicPosition.X, Is.EqualTo(21).Within(0.01));
                Assert.That(fetched.Result.MosaicPosition.Y, Is.EqualTo(22).Within(0.01));
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

        [Test]
        public async Task GetUnfinishedLocations_LinkedPair_ReturnsBothEnds()
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
                    Name = $"UnfinType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "UF",
                    Color = 0x112233,
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
                        Label = "unfinished",
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
                            Section = 4,
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

                locA = await CreatePoint(30, 30);
                locB = await CreatePoint(40, 40);
                await locationsClient.CreateLocationLinkAsync(new CreateLocationLinkRequest
                {
                    SourceId = locA.Value,
                    TargetId = locB.Value
                });

                var unfinished = await structuresClient.GetUnfinishedLocationsAsync(new GetUnfinishedLocationsRequest
                {
                    Id = structureId.Value
                });
                Assert.That(unfinished.Results, Does.Contain(locA.Value));
                Assert.That(unfinished.Results, Does.Contain(locB.Value));
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

        [Test]
        public async Task MergeStructures_MovesLocationsToKeepId()
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
                    Name = $"MergeType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "MG",
                    Color = 0x445566,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? keepId = null;
            long? mergeId = null;
            long? keepLoc = null;
            long? mergeLoc = null;
            try
            {
                async Task<(long structureId, long locationId)> CreateWithPoint(string label, double x, double y)
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
                    var loc = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                    {
                        Obj = new Location
                        {
                            ParentId = created.NewStructure.Id,
                            Section = 5,
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
                    return (created.NewStructure.Id, loc.Result.Id);
                }

                (keepId, keepLoc) = await CreateWithPoint("keep", 50, 50);
                (mergeId, mergeLoc) = await CreateWithPoint("merge", 60, 60);
                var deletedMergeId = mergeId.Value;

                var merge = await structuresClient.MergeAsync(new MergeRequest
                {
                    KeepId = keepId.Value,
                    MergeId = mergeId.Value
                });
                Assert.That(merge.KeptId, Is.EqualTo(keepId.Value));
                mergeId = null;

                var moved = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest
                {
                    Id = mergeLoc.Value
                });
                Assert.That(moved.Result.ParentId, Is.EqualTo(keepId.Value));

                var keepLocations = await locationsClient.GetStructureLocationsAsync(new GetStructureLocationsRequest
                {
                    StructureId = keepId.Value
                });
                Assert.That(keepLocations.Results, Has.Some.Matches<Location>(l => l.Id == keepLoc.Value));
                Assert.That(keepLocations.Results, Has.Some.Matches<Location>(l => l.Id == mergeLoc.Value));

                var mergeGone = Assert.ThrowsAsync<RpcException>(async () =>
                    await structuresClient.GetStructureByIDAsync(new GetStructureByIDRequest
                    {
                        Id = deletedMergeId
                    }));
                Assert.That(mergeGone.StatusCode, Is.EqualTo(StatusCode.NotFound));
            }
            finally
            {
                foreach (var id in new[] { keepLoc, mergeLoc })
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

                if (keepId.HasValue)
                {
                    try
                    {
                        await structuresClient.UpdateAsync(new UpdateStructuresRequest
                        {
                            Objs = { new StructureChangeRequest { Delete = keepId.Value } }
                        });
                    }
                    catch (RpcException) { }
                }

                if (mergeId.HasValue)
                {
                    try
                    {
                        await structuresClient.UpdateAsync(new UpdateStructuresRequest
                        {
                            Objs = { new StructureChangeRequest { Delete = mergeId.Value } }
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

        [Test]
        public async Task DeleteLocation_AppearsInLocationChangesDeletedIds()
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
                    Name = $"DelLog-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "DL",
                    Color = 0x1A2B3C,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locationId = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "del-log",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = structure.NewStructure.Id;

                var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                {
                    Obj = new Location
                    {
                        ParentId = structureId.Value,
                        Section = 13,
                        MosaicPosition = new AnnotationPoint { X = 8, Y = 9 },
                        VolumePosition = new AnnotationPoint { X = 8, Y = 9 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (8 9)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (8 9)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                locationId = created.Result.Id;

                var beforeDelete = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1));
                var delete = await locationsClient.UpdateAsync(new UpdateLocationsRequest
                {
                    Locations = { new LocationChangeRequest { Delete = locationId.Value } }
                });
                Assert.That(delete.Results[0].Success, Is.True);
                var deletedId = locationId.Value;
                locationId = null;

                var changes = await locationsClient.GetLocationChangesAsync(new GetLocationChangesRequest
                {
                    Section = 13,
                    ModifiedAfterThisUtcTime = beforeDelete
                });
                Assert.That(changes.DeletedIds, Does.Contain(deletedId));
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

        [Test]
        public async Task DeepDeleteStructure_LogsLocationInDeletedIds()
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
                    Name = $"DeepDel-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "DD",
                    Color = 0x2B3C4D,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locationId = null;
            try
            {
                var created = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "deep-del",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    },
                    NewAnnotation = new Location
                    {
                        Section = 14,
                        MosaicPosition = new AnnotationPoint { X = 12, Y = 13 },
                        VolumePosition = new AnnotationPoint { X = 12, Y = 13 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (12 13)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (12 13)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = created.NewStructure.Id;
                locationId = created.NewAnnotation.Id;

                var beforeDelete = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1));
                var delete = await structuresClient.UpdateAsync(new UpdateStructuresRequest
                {
                    Objs = { new StructureChangeRequest { Delete = structureId.Value } }
                });
                Assert.That(delete.Results[0].Success, Is.True);
                var deletedLoc = locationId.Value;
                structureId = null;
                locationId = null;

                var changes = await locationsClient.GetLocationChangesAsync(new GetLocationChangesRequest
                {
                    Section = 14,
                    ModifiedAfterThisUtcTime = beforeDelete
                });
                Assert.That(changes.DeletedIds, Does.Contain(deletedLoc));
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

        [Test]
        public async Task GetLocationChangesInMosaicRegion_Unary_ReturnsSeed()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var reply = await client.GetLocationChangesInMosaicRegionAsync(new GetLocationChangesInMosaicRegionRequest
            {
                Z = 1,
                MinRadius = 0,
                Region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
                {
                    Text = "POLYGON((90 190, 110 190, 110 210, 90 210, 90 190))"
                }
            });

            Assert.That(reply.Results, Has.Some.Matches<Location>(l => l.Id == 1));
            Assert.That(reply.QueryExecutedTime, Is.Not.Null);
        }

        [Test]
        public async Task GetAnnotationsInMosaicRegion_Unary_ReturnsSeed()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var reply = await client.GetAnnotationsInMosaicRegionAsync(new GetAnnotationsInMosaicRegionRequest
            {
                Z = 1,
                MinRadius = 0,
                Region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
                {
                    Text = "POLYGON((0 0, 200 0, 200 300, 0 300, 0 0))"
                }
            });

            Assert.That(reply.Result, Is.Not.Null);
            Assert.That(reply.Result.Locations, Has.Some.Matches<Location>(l => l.Id == 1));
            Assert.That(reply.Result.Structures, Has.Some.Matches<Structure>(s => s.Id == 1));
        }

        [Test]
        public async Task GetLocationsForSection_AndLocationChanges_ReturnSeed()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateLocations.AnnotateLocationsClient(channel);

            var forSection = await client.GetLocationsForSectionAsync(new GetLocationsForSectionRequest
            {
                Section = 1
            });
            Assert.That(forSection.Results, Has.Some.Matches<Location>(l => l.Id == 1));
            Assert.That(forSection.QueryExecutedTime, Is.Not.Null);

            var changes = await client.GetLocationChangesAsync(new GetLocationChangesRequest
            {
                Section = 1
            });
            Assert.That(changes.Results, Has.Some.Matches<Location>(l => l.Id == 1));
            Assert.That(changes.QueryExecutedTime, Is.Not.Null);
        }

        [Test]
        public async Task GetStructureTypesByIDs_ReturnsSeedAndCreated()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var created = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"ByIdsT-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "BD",
                    Color = 0x0D0E0F,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            try
            {
                var batch = await typesClient.GetStructureTypesByIDsAsync(new GetStructureTypesByIDsRequest
                {
                    Ids = { 1, created.Result.Id }
                });
                Assert.That(batch.Results, Has.Some.Matches<StructureType>(t => t.Id == 1));
                Assert.That(batch.Results, Has.Some.Matches<StructureType>(t => t.Id == created.Result.Id));
            }
            finally
            {
                try
                {
                    await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                    {
                        Objs = { new StructureTypeChangeRequest { Delete = created.Result.Id } }
                    });
                }
                catch (RpcException) { }
            }
        }

        [Test]
        public async Task GetLocationsByID_AndStructuresInVolumeRegion_ReturnSeed()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var locationsClient = new AnnotateLocations.AnnotateLocationsClient(channel);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);

            var byIds = await locationsClient.GetLocationsByIDAsync(new GetLocationsByIDRequest
            {
                Ids = { 1 }
            });
            Assert.That(byIds.Results, Has.Some.Matches<Location>(l => l.Id == 1));

            // Seed location uses the same mosaic/volume point (100, 200) on Z=1.
            var byVolume = await structuresClient.GetStructuresInVolumeRegionAsync(new GetStructuresInVolumeRegionRequest
            {
                Z = 1,
                MinRadius = 0,
                Region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
                {
                    Text = "POLYGON((90 190, 110 190, 110 210, 90 210, 90 190))"
                }
            });
            Assert.That(byVolume.Results, Has.Some.Matches<Structure>(s => s.Id == 1));
        }

        [Test]
        public async Task GetStructuresByID_ReturnsRequestedIds()
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
                    Name = $"ByIdT-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "BI",
                    Color = 0x010203,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? aId = null;
            long? bId = null;
            try
            {
                async Task<long> Create(string label)
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

                aId = await Create("by-id-a");
                bId = await Create("by-id-b");

                var batch = await structuresClient.GetStructuresByIDAsync(new GetStructuresByIDRequest
                {
                    Ids = { aId.Value, bId.Value, 1 }
                });
                Assert.That(batch.Results, Has.Some.Matches<Structure>(s => s.Id == aId.Value));
                Assert.That(batch.Results, Has.Some.Matches<Structure>(s => s.Id == bId.Value));
                Assert.That(batch.Results, Has.Some.Matches<Structure>(s => s.Id == 1));
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
        public async Task Split_UnlinkedLocation_CreatesNewStructure()
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
                    Name = $"SplitU-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "SU",
                    Color = 0x0A0B0C,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? keepStructureId = null;
            long? splitStructureId = null;
            long? keepLoc = null;
            long? splitLoc = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "split-unlinked",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                keepStructureId = structure.NewStructure.Id;

                async Task<long> CreatePoint(double x, double y)
                {
                    var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                    {
                        Obj = new Location
                        {
                            ParentId = keepStructureId.Value,
                            Section = 12,
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

                // Two locations, no link between them — Split moves the chosen tip's subgraph.
                keepLoc = await CreatePoint(10, 10);
                splitLoc = await CreatePoint(20, 20);

                var split = await structuresClient.SplitAsync(new SplitRequest
                {
                    Id = keepStructureId.Value,
                    FirstLocationIdOfSplitStructure = splitLoc.Value
                });
                Assert.That(split.SplitStructureId, Is.GreaterThan(0));
                Assert.That(split.SplitStructureId, Is.Not.EqualTo(keepStructureId.Value));
                splitStructureId = split.SplitStructureId;

                var moved = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest { Id = splitLoc.Value });
                Assert.That(moved.Result.ParentId, Is.EqualTo(splitStructureId.Value));

                var stayed = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest { Id = keepLoc.Value });
                Assert.That(stayed.Result.ParentId, Is.EqualTo(keepStructureId.Value));
            }
            finally
            {
                foreach (var id in new[] { keepLoc, splitLoc })
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

                foreach (var id in new[] { splitStructureId, keepStructureId })
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
        public async Task UpdateStructure_OmittingTypeId_PreservesExistingType()
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
                    Name = $"KeepType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "KT",
                    Color = 0x102030,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            try
            {
                var created = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "keep-type",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = created.NewStructure.Id;

                // Proto3 default TypeId=0 must not wipe the FK.
                var update = await structuresClient.UpdateAsync(new UpdateStructuresRequest
                {
                    Objs =
                    {
                        new StructureChangeRequest
                        {
                            Update = new Structure
                            {
                                Id = structureId.Value,
                                Label = "keep-type-updated",
                                Confidence = 0.6,
                                Created = now,
                                LastModified = Timestamp.FromDateTime(DateTime.UtcNow),
                                Username = _userIdentity.UserName,
                            }
                        }
                    }
                });
                Assert.That(update.Results[0].Success, Is.True);
                Assert.That(update.Results[0].Updated.TypeId, Is.EqualTo(createdType.Result.Id));
                Assert.That(update.Results[0].Updated.Label, Is.EqualTo("keep-type-updated"));

                var fetched = await structuresClient.GetStructureByIDAsync(new GetStructureByIDRequest
                {
                    Id = structureId.Value
                });
                Assert.That(fetched.Result.TypeId, Is.EqualTo(createdType.Result.Id));
            }
            finally
            {
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

        [Test]
        public async Task UpdateStructure_AndGetStructuresOfType_Roundtrip()
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
                    Name = $"OfType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "OT",
                    Color = 0x405060,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            try
            {
                var created = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "before",
                        Confidence = 0.4,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                structureId = created.NewStructure.Id;

                var update = await structuresClient.UpdateAsync(new UpdateStructuresRequest
                {
                    Objs =
                    {
                        new StructureChangeRequest
                        {
                            Update = new Structure
                            {
                                Id = structureId.Value,
                                TypeId = createdType.Result.Id,
                                Label = "after",
                                Confidence = 0.9,
                                Verified = true,
                                Created = now,
                                LastModified = Timestamp.FromDateTime(DateTime.UtcNow),
                                Username = _userIdentity.UserName,
                            }
                        }
                    }
                });
                Assert.That(update.Results, Has.Count.EqualTo(1));
                Assert.That(update.Results[0].Success, Is.True);
                Assert.That(update.Results[0].Updated.Label, Is.EqualTo("after"));
                Assert.That(update.Results[0].Updated.Verified, Is.True);

                var fetched = await structuresClient.GetStructureByIDAsync(new GetStructureByIDRequest
                {
                    Id = structureId.Value
                });
                Assert.That(fetched.Result.Label, Is.EqualTo("after"));
                Assert.That(fetched.Result.Confidence, Is.EqualTo(0.9).Within(0.001));

                var ofType = await structuresClient.GetStructuresOfTypeAsync(new GetStructuresOfTypeRequest
                {
                    Id = createdType.Result.Id
                });
                Assert.That(ofType.Results, Has.Some.Matches<Structure>(s => s.Id == structureId.Value));
            }
            finally
            {
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

        [Test]
        public async Task GetLocationLinksForSection_ReturnsCreatedLink()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var locationsClient = new AnnotateLocations.AnnotateLocationsClient(channel);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            const long section = 11;
            var createdType = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"LinkSec-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "LS",
                    Color = 0x708090,
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
                        Label = "link-sec",
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
                            Section = section,
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

                locA = await CreatePoint(5, 5);
                locB = await CreatePoint(6, 6);
                await locationsClient.CreateLocationLinkAsync(new CreateLocationLinkRequest
                {
                    SourceId = locA.Value,
                    TargetId = locB.Value
                });

                var links = await locationsClient.GetLocationLinksForSectionAsync(new GetLocationLinksForSectionRequest
                {
                    Section = section,
                    ModifiedAfterThisTime = 0
                });
                Assert.That(links.Results, Has.Some.Matches<LocationLink>(l =>
                    (l.SourceId == locA.Value && l.TargetId == locB.Value) ||
                    (l.SourceId == locB.Value && l.TargetId == locA.Value)));

                var regionLinks = await locationsClient.GetLocationLinksForSectionInMosaicRegionAsync(
                    new GetLocationLinksForSectionInMosaicRegionRequest
                    {
                        Section = section,
                        MinRadius = 0,
                        Bbox = new BoundingRectangle { Xmin = 0, Ymin = 0, Xmax = 10, Ymax = 10 }
                    });
                Assert.That(regionLinks.Results, Has.Some.Matches<LocationLink>(l =>
                    (l.SourceId == locA.Value && l.TargetId == locB.Value) ||
                    (l.SourceId == locB.Value && l.TargetId == locA.Value)));
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

        [Test]
        public async Task Scale_ReturnsConfiguredUnits()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateMetaData.AnnotateMetaDataClient(channel);

            var reply = await client.ScaleAsync(new ScaleRequest());

            Assert.That(reply.Scale, Is.Not.Null);
            Assert.That(reply.Scale.X, Is.Not.Null);
            Assert.That(reply.Scale.X.Units, Is.EqualTo("nm"));
            Assert.That(reply.Scale.X.Value, Is.EqualTo(2.176).Within(0.001));
            Assert.That(reply.Scale.Z, Is.Not.Null);
            Assert.That(reply.Scale.Z.Value, Is.EqualTo(90.0).Within(0.001));
        }

        [Test]
        public async Task CreateUpdateStructureType_WithParent_Roundtrip()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var parent = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"ParentT-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "PT",
                    Color = 0x111111,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? childId = null;
            try
            {
                var child = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
                {
                    Obj = new StructureType
                    {
                        Name = $"ChildT-{Guid.NewGuid():N}".Substring(0, 32),
                        Code = "CT",
                        Color = 0x222222,
                        ParentId = parent.Result.Id,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                childId = child.Result.Id;
                Assert.That(child.Result.ParentId, Is.EqualTo(parent.Result.Id));

                var renamed = $"Renamed-{Guid.NewGuid():N}".Substring(0, 32);
                var update = await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                {
                    Objs =
                    {
                        new StructureTypeChangeRequest
                        {
                            Update = new StructureType
                            {
                                Id = childId.Value,
                                Name = renamed,
                                Code = "CT",
                                Color = 0x333333,
                                ParentId = parent.Result.Id,
                                Created = now,
                                LastModified = Timestamp.FromDateTime(DateTime.UtcNow),
                                Username = _userIdentity.UserName,
                            }
                        }
                    }
                });
                Assert.That(update.Results, Has.Count.EqualTo(1));
                Assert.That(update.Results[0].Success, Is.True);
                Assert.That(update.Results[0].Updated.Name.Trim(), Is.EqualTo(renamed.Trim()));

                var fetched = await typesClient.GetStructureTypeByIDAsync(new GetStructureTypeByIDRequest
                {
                    Id = childId.Value
                });
                Assert.That(fetched.Result.ParentId, Is.EqualTo(parent.Result.Id));
                Assert.That(fetched.Result.Name.Trim(), Is.EqualTo(renamed.Trim()));
            }
            finally
            {
                if (childId.HasValue)
                {
                    try
                    {
                        await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                        {
                            Objs = { new StructureTypeChangeRequest { Delete = childId.Value } }
                        });
                    }
                    catch (RpcException) { }
                }

                try
                {
                    await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                    {
                        Objs = { new StructureTypeChangeRequest { Delete = parent.Result.Id } }
                    });
                }
                catch (RpcException) { }
            }
        }

        [Test]
        public async Task GetStructuresForSection_AndMosaicRegion_ReturnSeed()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var client = new AnnotateStructures.AnnotateStructuresClient(channel);

            var bySection = await client.GetStructuresForSectionAsync(new GetStructuresForSectionRequest { Z = 1 });
            Assert.That(bySection.Results, Has.Some.Matches<Structure>(s => s.Id == 1));
            Assert.That(bySection.QueryExecutedTime, Is.Not.Null);

            // DateTime.MinValue must mean "unbounded", not a SQL DATETIME comparison.
            var withMinValue = await client.GetStructuresForSectionAsync(new GetStructuresForSectionRequest
            {
                Z = 1,
                ModifiedAfterThisUtcTime = Timestamp.FromDateTime(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc))
            });
            Assert.That(withMinValue.Results, Has.Some.Matches<Structure>(s => s.Id == 1));

            var byRegion = await client.GetStructuresInMosaicRegionAsync(new GetStructuresInMosaicRegionRequest
            {
                Z = 1,
                MinRadius = 0,
                Region = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry
                {
                    Text = "POLYGON((90 190, 110 190, 110 210, 90 210, 90 190))"
                }
            });
            Assert.That(byRegion.Results, Has.Some.Matches<Structure>(s => s.Id == 1));
        }

        [Test]
        public async Task UpdateStructureLinks_UpdatesBidirectional()
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
                    Name = $"UpdLink-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "UL",
                    Color = 0x112233,
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

                aId = await CreateStructure("upd-a");
                bId = await CreateStructure("upd-b");

                await structuresClient.CreateStructureLinkAsync(new CreateStructureLinkRequest
                {
                    NewLink = new StructureLink
                    {
                        SourceId = aId.Value,
                        TargetId = bId.Value,
                        Bidirectional = false,
                    }
                });

                await structuresClient.UpdateLinksAsync(new UpdateStructureLinksRequest
                {
                    Objs =
                    {
                        new StructureLink
                        {
                            SourceId = aId.Value,
                            TargetId = bId.Value,
                            Bidirectional = true,
                        }
                    }
                });

                var linked = await structuresClient.GetLinkedStructuresAsync(new GetLinkedStructuresRequest
                {
                    Id = aId.Value
                });
                Assert.That(linked.Results, Has.Some.Matches<StructureLink>(l =>
                    l.SourceId == aId.Value && l.TargetId == bId.Value && l.Bidirectional));
            }
            finally
            {
                if (aId.HasValue && bId.HasValue)
                {
                    try
                    {
                        await structuresClient.DeleteStructureLinkAsync(new DeleteStructureLinkRequest
                        {
                            SourceId = aId.Value,
                            TargetId = bId.Value
                        });
                    }
                    catch (RpcException) { }
                }

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
        public async Task CreateStructure_WithAnnotation_ReturnsBoth()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var structuresClient = new AnnotateStructures.AnnotateStructuresClient(channel);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);
            var locationsClient = new AnnotateLocations.AnnotateLocationsClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            var createdType = await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
            {
                Obj = new StructureType
                {
                    Name = $"WithAnn-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "WA",
                    Color = 0x123456,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? structureId = null;
            long? locationId = null;
            try
            {
                var created = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "with-ann",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    },
                    NewAnnotation = new Location
                    {
                        Section = 8,
                        MosaicPosition = new AnnotationPoint { X = 15, Y = 16 },
                        VolumePosition = new AnnotationPoint { X = 15, Y = 16 },
                        MosaicShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (15 16)" },
                        VolumeShape = new Viking.AnnotationServiceTypes.gRPC.V1.Protos.Geometry { Text = "POINT (15 16)" },
                        TypeCode = AnnotationType.Circle,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });

                structureId = created.NewStructure.Id;
                Assert.That(created.NewAnnotation, Is.Not.Null);
                locationId = created.NewAnnotation.Id;
                Assert.That(locationId, Is.GreaterThan(0));
                Assert.That(created.NewAnnotation.ParentId, Is.EqualTo(structureId.Value));
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

        [Test]
        public async Task SplitAtLocationLink_MovesSplitSubgraph()
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
                    Name = $"SplitType-{Guid.NewGuid():N}".Substring(0, 32),
                    Code = "SP",
                    Color = 0x654321,
                    Created = now,
                    LastModified = now,
                    Username = _userIdentity.UserName,
                }
            });

            long? keepStructureId = null;
            long? splitStructureId = null;
            long? keepLoc = null;
            long? splitLoc = null;
            try
            {
                var structure = await structuresClient.CreateStructureAsync(new CreateStructureRequest
                {
                    NewStructure = new Structure
                    {
                        TypeId = createdType.Result.Id,
                        Label = "to-split",
                        Confidence = 0.5,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                });
                keepStructureId = structure.NewStructure.Id;

                async Task<long> CreatePoint(double x, double y)
                {
                    var created = await locationsClient.CreateLocationAsync(new CreateLocationRequest
                    {
                        Obj = new Location
                        {
                            ParentId = keepStructureId.Value,
                            Section = 9,
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

                keepLoc = await CreatePoint(1, 1);
                splitLoc = await CreatePoint(2, 2);
                await locationsClient.CreateLocationLinkAsync(new CreateLocationLinkRequest
                {
                    SourceId = keepLoc.Value,
                    TargetId = splitLoc.Value
                });

                var split = await structuresClient.SplitAtLocationLinkAsync(new SplitAtLocationLinkRequest
                {
                    LocationIdOfKeepStructure = keepLoc.Value,
                    LocationIdOfSplitStructure = splitLoc.Value
                });
                Assert.That(split.SplitStructureId, Is.GreaterThan(0));
                Assert.That(split.SplitStructureId, Is.Not.EqualTo(keepStructureId.Value));
                splitStructureId = split.SplitStructureId;

                var moved = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest { Id = splitLoc.Value });
                Assert.That(moved.Result.ParentId, Is.EqualTo(splitStructureId.Value));

                var stayed = await locationsClient.GetLocationByIDAsync(new GetLocationByIDRequest { Id = keepLoc.Value });
                Assert.That(stayed.Result.ParentId, Is.EqualTo(keepStructureId.Value));
            }
            finally
            {
                foreach (var id in new[] { keepLoc, splitLoc })
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

                foreach (var id in new[] { splitStructureId, keepStructureId })
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
        public async Task CreatePermittedStructureLink_Roundtrip()
        {
            var accessToken = await RequestAccessTokenAsync();
            using var channel = CreateAuthenticatedChannel(accessToken);
            var permittedClient = new PermittedStructureLinks.PermittedStructureLinksClient(channel);
            var typesClient = new AnnotateStructureTypes.AnnotateStructureTypesClient(channel);

            var now = Timestamp.FromDateTime(DateTime.UtcNow);
            async Task<long> CreateType(string code) =>
                (await typesClient.CreateStructureTypeAsync(new CreateStructureTypeRequest
                {
                    Obj = new StructureType
                    {
                        Name = $"Psl{code}-{Guid.NewGuid():N}".Substring(0, 32),
                        Code = code,
                        Color = 0x101010,
                        Created = now,
                        LastModified = now,
                        Username = _userIdentity.UserName,
                    }
                })).Result.Id;

            long? sourceTypeId = null;
            long? targetTypeId = null;
            try
            {
                sourceTypeId = await CreateType("P1");
                targetTypeId = await CreateType("P2");

                var created = await permittedClient.CreatePermittedStructureLinkAsync(new CreatePermittedStructureLinkRequest
                {
                    NewObj = new PermittedStructureLink
                    {
                        SourceTypeId = sourceTypeId.Value,
                        TargetTypeId = targetTypeId.Value,
                        Bidirectional = true
                    }
                });
                Assert.That(created.Result.SourceTypeId, Is.EqualTo(sourceTypeId.Value));
                Assert.That(created.Result.TargetTypeId, Is.EqualTo(targetTypeId.Value));

                var all = await permittedClient.GetPermittedStructureLinksAsync(new GetPermittedStructureLinksRequest());
                Assert.That(all.PermittedLinks, Has.Some.Matches<PermittedStructureLink>(p =>
                    p.SourceTypeId == sourceTypeId.Value && p.TargetTypeId == targetTypeId.Value));

                var deleted = await permittedClient.UpdatePermittedStructureLinksAsync(new UpdatePermittedStructureLinksRequest
                {
                    Changes =
                    {
                        new PermittedStructureLinkChange
                        {
                            Action = DBAction.Delete,
                            Result = new PermittedStructureLink
                            {
                                SourceTypeId = sourceTypeId.Value,
                                TargetTypeId = targetTypeId.Value,
                                Bidirectional = true
                            }
                        }
                    }
                });
                Assert.That(deleted.Changes, Has.Count.EqualTo(1));
                Assert.That(deleted.Changes[0].Sucess, Is.True);
                Assert.That(deleted.Changes[0].Action, Is.EqualTo(DBAction.Delete));

                var afterDelete = await permittedClient.GetPermittedStructureLinksAsync(new GetPermittedStructureLinksRequest());
                Assert.That(afterDelete.PermittedLinks, Has.None.Matches<PermittedStructureLink>(p =>
                    p.SourceTypeId == sourceTypeId.Value && p.TargetTypeId == targetTypeId.Value));
            }
            finally
            {
                if (sourceTypeId.HasValue && targetTypeId.HasValue)
                {
                    try
                    {
                        await permittedClient.UpdatePermittedStructureLinksAsync(new UpdatePermittedStructureLinksRequest
                        {
                            Changes =
                            {
                                new PermittedStructureLinkChange
                                {
                                    Action = DBAction.Delete,
                                    Result = new PermittedStructureLink
                                    {
                                        SourceTypeId = sourceTypeId.Value,
                                        TargetTypeId = targetTypeId.Value
                                    }
                                }
                            }
                        });
                    }
                    catch (RpcException) { }
                }

                foreach (var id in new[] { sourceTypeId, targetTypeId })
                {
                    if (!id.HasValue) continue;
                    try
                    {
                        await typesClient.UpdateAsync(new UpdateStructureTypesRequest
                        {
                            Objs = { new StructureTypeChangeRequest { Delete = id.Value } }
                        });
                    }
                    catch (RpcException) { }
                }
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
