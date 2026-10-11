using System;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// Covers the Identity side of the correction service: which contexts a service is assigned and the
    /// CorrectionServiceUrl clients receive (null when unassigned).
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

        [Fact]
        public async Task ListAsync_ReturnsOnlyContextsAssignedToThatService()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            server.AnnotationDatabaseName = "RC1db";
            rc1.RegistrationName = "grid";
            var mine = AddService(db, "corr-a", "https://corr-a.example/");
            AddService(db, "corr-b", "https://corr-b.example/");
            rc1.CorrectionServiceId = mine.Id;
            await db.SaveChangesAsync();

            var assigned = await CorrectionServiceAssignments.ListAsync(db, "corr-a");

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

            Assert.Null(await CorrectionServiceAssignments.ListAsync(db, "nope"));
        }

        [Fact]
        public async Task VolumeMetadata_CorrectionServiceUrl_IsServiceEndpointOrNull()
        {
            await using var db = CatalogDb.Create();
            var (_, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var service = AddService(db, "corr-a", "https://corr-a.example/");
            rc1.CorrectionServiceId = service.Id;
            await db.SaveChangesAsync();

            var contexts = db.WithCatalog().ToList().ToDictionary(c => c.Id);

            Assert.Equal("https://corr-a.example/", VolumeMetadata.Build(contexts[rc1.Id])[VolumeMetadata.CorrectionServiceUrl]);
            Assert.Null(VolumeMetadata.Build(contexts[internalCopy.Id])[VolumeMetadata.CorrectionServiceUrl]);
        }
    }
}
