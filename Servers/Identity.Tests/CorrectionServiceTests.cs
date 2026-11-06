using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// Covers the Identity side of the correction service: which contexts a service is assigned, that a context has one
    /// correction service, and the CorrectionServiceUrl clients receive (null when unassigned).
    /// </summary>
    public class CorrectionServiceTests
    {
        private static CorrectionService AddService(Viking.Identity.Data.ApplicationDbContext db, string name, string endpoint)
        {
            var service = new CorrectionService
            {
                Name = name,
                ResourceTypeId = nameof(CorrectionService),
                Endpoint = new Uri(endpoint)
            };
            db.CorrectionServices.Add(service);
            db.SaveChanges();
            return service;
        }

        private static void Assign(Viking.Identity.Data.ApplicationDbContext db, CorrectionService service, Resource resource)
        {
            db.ContextServiceAssignments.Add(new ContextServiceAssignment { ServiceId = service.Id, ResourceId = resource.Id });
            db.SaveChanges();
        }

        private static Group AddGroup(Viking.Identity.Data.ApplicationDbContext db, string name)
        {
            var group = new Group { Name = name, ResourceTypeId = nameof(Group) };
            db.Group.Add(group);
            db.SaveChanges();
            return group;
        }

        [Fact]
        public async Task ListAsync_ReturnsOnlyContextsAssignedToThatService()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            server.AnnotationDatabaseName = "RC1db";
            rc1.RegistrationName = "grid";
            var mine = AddService(db, "corr-a", "https://corr-a.example/");
            AddService(db, "corr-b", "https://corr-b.example/");
            await db.SaveChangesAsync();
            Assign(db, mine, rc1);

            var assigned = await ContextServiceAssignments.ListAsync<CorrectionService>(db, "corr-a");

            var only = Assert.Single(assigned);
            Assert.Equal(rc1.Id, only.AnnotationContextId);
            Assert.Equal("grid", only.RegistrationName);
            Assert.Equal("RC1db", only.AnnotationDatabaseName);
            Assert.Equal("https://webann.example/RC1/OData/", only.ODataEndpoint);
            Assert.DoesNotContain(assigned, a => a.AnnotationContextId == internalCopy.Id);
        }

        [Fact]
        public async Task ListAsync_UnknownService_ReturnsNull()
        {
            await using var db = CatalogDb.Create();

            Assert.Null(await ContextServiceAssignments.ListAsync<CorrectionService>(db, "nope"));
        }

        [Fact]
        public async Task VolumeMetadata_CorrectionServiceUrl_IsServiceEndpointOrNull()
        {
            await using var db = CatalogDb.Create();
            var (_, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var service = AddService(db, "corr-a", "https://corr-a.example/");
            Assign(db, service, rc1);

            var contexts = db.WithCatalog().ToList().ToDictionary(c => c.Id);
            var owners = await db.CorrectionOwnersAsync();

            Assert.Equal("https://corr-a.example/", VolumeMetadata.Build(contexts[rc1.Id], owners.GetValueOrDefault(rc1.Id))[VolumeMetadata.CorrectionServiceUrl]);
            Assert.Null(VolumeMetadata.Build(contexts[internalCopy.Id], owners.GetValueOrDefault(internalCopy.Id))[VolumeMetadata.CorrectionServiceUrl]);
        }

        [Fact]
        public async Task AGroupAssignment_ReachesItsContexts()
        {
            await using var db = CatalogDb.Create();
            var (_, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var utah = AddGroup(db, "Utah");
            Assert.True(await db.AddResourceToGroupAsync(utah.Id, rc1.Id));
            await db.SaveChangesAsync();
            Assign(db, AddService(db, "corr-a", "https://corr-a.example/"), utah);

            var only = Assert.Single(await ContextServiceAssignments.ListAsync<CorrectionService>(db, "corr-a"));
            Assert.Equal(rc1.Id, only.AnnotationContextId);
        }

        [Fact]
        public async Task Conflicts_NameContextsAnotherCorrectionServiceHas()
        {
            await using var db = CatalogDb.Create();
            var (_, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var utah = AddGroup(db, "Utah");
            Assert.True(await db.AddResourceToGroupAsync(utah.Id, rc1.Id));
            await db.SaveChangesAsync();
            var a = AddService(db, "corr-a", "https://corr-a.example/");
            var b = AddService(db, "corr-b", "https://corr-b.example/");
            Assign(db, a, rc1);

            var conflict = Assert.Single(await db.CorrectionConflictsAsync(b.Id, new[] { utah.Id, internalCopy.Id }));
            Assert.Equal(rc1.Id, conflict.Context.Id);
            Assert.Equal(a.Id, conflict.Owner.Id);
            Assert.Empty(await db.CorrectionConflictsAsync(a.Id, new[] { utah.Id, rc1.Id }));
        }

        [Fact]
        public async Task TwoServicesReachingAContext_TheLowestIdKeepsIt()
        {
            await using var db = CatalogDb.Create();
            var (_, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            var a = AddService(db, "corr-a", "https://corr-a.example/");
            var b = AddService(db, "corr-b", "https://corr-b.example/");
            Assign(db, b, rc1);
            Assign(db, a, rc1);

            Assert.Equal(a.Id, (await db.CorrectionOwnersAsync())[rc1.Id].Id);
            Assert.Single(await ContextServiceAssignments.ListAsync<CorrectionService>(db, "corr-a"));
            Assert.Empty(await ContextServiceAssignments.ListAsync<CorrectionService>(db, "corr-b"));
        }

        [Fact]
        public async Task DeletingAService_RemovesItsAssignments()
        {
            await using var db = CatalogDb.Create();
            var (_, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            var a = AddService(db, "corr-a", "https://corr-a.example/");
            Assign(db, a, rc1);

            db.CorrectionServices.Remove(a);
            await db.SaveChangesAsync();

            Assert.Empty(db.ContextServiceAssignments);
            Assert.Empty(await db.CorrectionOwnersAsync());
        }
    }
}
