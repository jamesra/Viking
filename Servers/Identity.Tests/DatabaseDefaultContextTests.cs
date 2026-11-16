using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// An annotation database names its default context; services learn it through AssignedAnnotationContexts. Deleting a
    /// context that a database names as its default first applies the replacement the administrator chose.
    /// </summary>
    public class DatabaseDefaultContextTests
    {
        [Fact]
        public async Task AssignedContexts_CarryTheDatabaseDefault()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            server.DefaultAnnotationContextId = internalCopy.Id;
            await db.SaveChangesAsync();

            var rows = (await db.WithCatalog().ToListAsync()).Select(AssignedAnnotationContext.From).ToList();

            Assert.Equal(2, rows.Count);
            Assert.All(rows, row => Assert.Equal(internalCopy.Id, row.DatabaseDefaultContextId));
        }

        [Fact]
        public async Task AssignedContexts_WithoutADatabaseDefault_CarryNull()
        {
            await using var db = CatalogDb.Create();
            await CatalogDb.AddSharedServerAsync(db);

            var rows = (await db.WithCatalog().ToListAsync()).Select(AssignedAnnotationContext.From).ToList();

            Assert.All(rows, row => Assert.Null(row.DatabaseDefaultContextId));
        }

        [Fact]
        public async Task Holders_ListTheDatabase_WithItsOtherContexts()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            server.DefaultAnnotationContextId = rc1.Id;
            await db.SaveChangesAsync();

            var database = Assert.Single(await DefaultContextReplacement.HoldersAsync(db, rc1.Id));
            Assert.Equal(server.Id, database.Id);
            Assert.Equal(new[] { internalCopy.Id }, database.Alternatives.Select(a => a.Id));
        }

        [Fact]
        public async Task Apply_SetsTheChosenDefaults()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            server.DefaultAnnotationContextId = rc1.Id;
            await db.SaveChangesAsync();

            var problems = await DefaultContextReplacement.ApplyAsync(db, rc1.Id, new Dictionary<long, string>
            {
                [server.Id] = internalCopy.Id.ToString(),
            });

            Assert.Empty(problems);
            Assert.Equal(internalCopy.Id, (await db.AnnotationServers.FindAsync(server.Id)).DefaultAnnotationContextId);
        }

        [Fact]
        public async Task Apply_NoDefault_ClearsIt()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            server.DefaultAnnotationContextId = rc1.Id;
            await db.SaveChangesAsync();

            var problems = await DefaultContextReplacement.ApplyAsync(db, rc1.Id, new Dictionary<long, string>
            {
                [server.Id] = DefaultContextReplacement.NoDefault,
            });

            Assert.Empty(problems);
            Assert.Null((await db.AnnotationServers.FindAsync(server.Id)).DefaultAnnotationContextId);
        }

        [Fact]
        public async Task Apply_RefusesAMissingChoice_AndChangesNothing()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            server.DefaultAnnotationContextId = rc1.Id;
            await db.SaveChangesAsync();

            var problems = await DefaultContextReplacement.ApplyAsync(db, rc1.Id, new Dictionary<long, string>());

            Assert.Single(problems);
            Assert.Equal(rc1.Id, (await db.AnnotationServers.FindAsync(server.Id)).DefaultAnnotationContextId);
        }

        [Fact]
        public async Task Apply_RefusesAContextOutsideTheHolder()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            server.DefaultAnnotationContextId = rc1.Id;
            var unlinked = CatalogDb.AddVolume(db, "Elsewhere", "https://storage/Elsewhere.VikingXML");
            await db.SaveChangesAsync();

            // A context not linked to the database cannot become its default.
            var problems = await DefaultContextReplacement.ApplyAsync(db, rc1.Id, new Dictionary<long, string>
            {
                [server.Id] = unlinked.Id.ToString(),
            });

            Assert.Single(problems);
            Assert.Equal(rc1.Id, (await db.AnnotationServers.FindAsync(server.Id)).DefaultAnnotationContextId);
        }
    }
}
