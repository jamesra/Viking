using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    public class BulkPermissionsApplyTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly ApplicationDbContext _db;

        public BulkPermissionsApplyTests(InMemoryIdentityFixture fixture)
        {
            _db = fixture.DataContext;
        }

        [Fact]
        public async Task SavingUnchangedUnion_DoesNotRemoveExistingGrants()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var group = new Group { Name = $"BulkGrp-{unique}" };
            var volA = new Volume { Name = $"BulkA-{unique}" };
            var volB = new Volume { Name = $"BulkB-{unique}" };
            _db.Group.Add(group);
            _db.Volume.Add(volA);
            _db.Volume.Add(volB);
            await _db.SaveChangesAsync();

            _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                GroupId = group.Id,
                ResourceId = volA.Id,
                PermissionId = Special.Permissions.Volume.Review
            });
            _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                GroupId = group.Id,
                ResourceId = volB.Id,
                PermissionId = Special.Permissions.Volume.Read
            });
            await _db.SaveChangesAsync();

            var resourceIds = new[] { volA.Id, volB.Id };
            var existing = await LoadGrants(resourceIds);

            // Union form state: both Review and Read checked.
            var submitted = new[]
            {
                new SubmittedGranteePermissions
                {
                    GranteeKey = group.Id.ToString(),
                    Permissions = new List<(string, bool)>
                    {
                        (Special.Permissions.Volume.Review, true),
                        (Special.Permissions.Volume.Read, true),
                        (Special.Permissions.Volume.Annotate, false)
                    }
                }
            };

            var analysis = BulkPermissionsChangeAnalyzer.Analyze(existing, resourceIds, null, submitted);
            Assert.Empty(analysis.GrantsToRemove);

            var volATracked = await _db.Volume
                .Include(v => v.GroupsWithPermissions)
                .SingleAsync(v => v.Id == volA.Id);
            var volBTracked = await _db.Volume
                .Include(v => v.GroupsWithPermissions)
                .SingleAsync(v => v.Id == volB.Id);

            ApplyDesiredGroupGrants(volATracked, group.Id, submitted[0]);
            ApplyDesiredGroupGrants(volBTracked, group.Id, submitted[0]);
            await _db.SaveChangesAsync();

            Assert.True(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.ResourceId == volA.Id && g.PermissionId == Special.Permissions.Volume.Review));
            Assert.True(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.ResourceId == volB.Id && g.PermissionId == Special.Permissions.Volume.Read));
            // Union replace also adds the missing cross grants:
            Assert.True(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.ResourceId == volA.Id && g.PermissionId == Special.Permissions.Volume.Read));
            Assert.True(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.ResourceId == volB.Id && g.PermissionId == Special.Permissions.Volume.Review));
        }

        [Fact]
        public async Task IntentionalUncheck_RemovesGrantFromAllSelectedResources()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var group = new Group { Name = $"BulkUnchk-{unique}" };
            var volA = new Volume { Name = $"UnchkA-{unique}" };
            var volB = new Volume { Name = $"UnchkB-{unique}" };
            _db.Group.Add(group);
            _db.Volume.Add(volA);
            _db.Volume.Add(volB);
            await _db.SaveChangesAsync();

            foreach (var vol in new[] { volA, volB })
            {
                _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
                {
                    GroupId = group.Id,
                    ResourceId = vol.Id,
                    PermissionId = Special.Permissions.Volume.Review
                });
                _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
                {
                    GroupId = group.Id,
                    ResourceId = vol.Id,
                    PermissionId = Special.Permissions.Volume.Read
                });
            }
            await _db.SaveChangesAsync();

            var submitted = new SubmittedGranteePermissions
            {
                GranteeKey = group.Id.ToString(),
                Permissions = new List<(string, bool)>
                {
                    (Special.Permissions.Volume.Review, false),
                    (Special.Permissions.Volume.Read, true)
                }
            };

            ApplyDesiredGroupGrants(
                await _db.Volume.Include(v => v.GroupsWithPermissions).SingleAsync(v => v.Id == volA.Id),
                group.Id,
                submitted);
            ApplyDesiredGroupGrants(
                await _db.Volume.Include(v => v.GroupsWithPermissions).SingleAsync(v => v.Id == volB.Id),
                group.Id,
                submitted);
            await _db.SaveChangesAsync();

            Assert.False(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.PermissionId == Special.Permissions.Volume.Review
                && (g.ResourceId == volA.Id || g.ResourceId == volB.Id)));
            Assert.True(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.ResourceId == volA.Id && g.PermissionId == Special.Permissions.Volume.Read));
            Assert.True(await _db.GrantedGroupPermissions.AnyAsync(g =>
                g.GroupId == group.Id && g.ResourceId == volB.Id && g.PermissionId == Special.Permissions.Volume.Read));
        }

        private async Task<List<PermissionGrant>> LoadGrants(IEnumerable<long> resourceIds)
        {
            var ids = resourceIds.ToList();
            var groupGrants = await _db.GrantedGroupPermissions
                .Where(g => ids.Contains(g.ResourceId))
                .ToListAsync();
            return groupGrants
                .Select(g => new PermissionGrant(g.ResourceId, g.GroupId.ToString(), g.PermissionId, false))
                .ToList();
        }

        private static void ApplyDesiredGroupGrants(Resource resource, long groupId, SubmittedGranteePermissions submitted)
        {
            foreach (var (permissionId, selected) in submitted.Permissions)
            {
                resource.UpdateGroupPermissions(groupId, permissionId, selected);
            }
        }
    }
}
