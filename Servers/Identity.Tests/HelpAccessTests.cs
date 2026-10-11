using System.Threading.Tasks;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>The administrator help page is limited to people who administer at least one organizational unit.</summary>
    public class HelpAccessTests
    {
        [Fact]
        public async Task OrgUnitAdministrator_CanView()
        {
            await using var db = CatalogDb.Create();
            var userId = db.CreateUser("orgadmin", "x");
            var org = new OrganizationalUnit { Name = "Lab", ResourceTypeId = nameof(OrganizationalUnit) };
            db.OrgUnit.Add(org);
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission
            {
                ResourceId = org.Id,
                PermissionId = Special.Permissions.OrgUnit.Admin,
                UserId = userId
            });
            await db.SaveChangesAsync();

            Assert.True(await HelpAccess.IsOrgUnitAdminAnywhereAsync(db, userId));
        }

        [Fact]
        public async Task UserWithNoOrgUnitAdministration_CannotView()
        {
            await using var db = CatalogDb.Create();
            var (server, _, _) = await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("annotator", "x");
            await db.SaveChangesAsync();
            // Annotation rights on a server are not administration of an organizational unit.
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = server.Id, PermissionId = "Annotate", UserId = userId });
            await db.SaveChangesAsync();

            Assert.False(await HelpAccess.IsOrgUnitAdminAnywhereAsync(db, userId));
        }
    }
}
