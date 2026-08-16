using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.WebManagement.Extensions;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// Covers the permission checks performed by <see cref="UserScopeTokenRequestValidator"/>
    /// (resource resolution + IsUserPermitted) without constructing a full Duende token request.
    /// </summary>
    public class UserScopeTokenRequestValidatorTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly ApplicationDbContext _db;

        public UserScopeTokenRequestValidatorTests(InMemoryIdentityFixture fixture)
        {
            _db = fixture.DataContext;
        }

        [Fact]
        public async Task ScopePermissionPath_AllowsUserWithGroupGrant()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userName = $"scope-ok-{unique}";
            var userId = _db.CreateUser(userName, "None");
            await _db.SaveChangesAsync();

            var group = new Group { Name = $"ScopeGrp-{unique}" };
            var volume = new Volume { Name = $"ScopeVol-{unique}" };
            _db.Group.Add(group);
            _db.Volume.Add(volume);
            await _db.SaveChangesAsync();

            _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                GroupId = group.Id,
                ResourceId = volume.Id,
                PermissionId = Special.Permissions.Volume.Review
            });
            _db.UserToGroupAssignments.Add(new UserToGroupAssignment
            {
                GroupId = group.Id,
                UserId = userId
            });
            await _db.SaveChangesAsync();

            var resource = await _db.FindApiFacingResourceAsync(volume.Name);
            Assert.NotNull(resource);
            Assert.Equal(nameof(Volume), resource.ResourceTypeId);

            var user = await _db.Users.FirstAsync(u => u.UserName == userName);
            Assert.True(await _db.IsUserPermitted(resource.Id, user.Id, Special.Permissions.Volume.Review));

            // Validator would accept VolumeName.Review for this user.
            var scopeName = ResourceScopeNames.ToScope(volume.Name, Special.Permissions.Volume.Review);
            Assert.True(ResourceScopeNames.TryParse(scopeName, out var prefix, out var encodedPermission));
            Assert.True(await _db.IsUserPermitted(
                (await _db.FindApiFacingResourceAsync(prefix)).Id,
                user.Id,
                ResourceScopeNames.ToPermissionId(encodedPermission)));
        }

        [Fact]
        public async Task ScopePermissionPath_DeniesUserWithoutGrant()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userName = $"scope-deny-{unique}";
            var userId = _db.CreateUser(userName, "None");
            await _db.SaveChangesAsync();

            var volume = new Volume { Name = $"ScopeDenyVol-{unique}" };
            _db.Volume.Add(volume);
            await _db.SaveChangesAsync();

            var resource = await _db.FindApiFacingResourceAsync(volume.Name);
            var user = await _db.Users.FirstAsync(u => u.UserName == userName);

            Assert.False(await _db.IsUserPermitted(resource.Id, user.Id, Special.Permissions.Volume.Review));
        }

        [Fact]
        public async Task FindApiFacingResource_PrefersVolumeOverGroupWithSameName()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var name = $"Yiu-{unique}";

            _db.Group.Add(new Group { Name = name });
            _db.Volume.Add(new Volume { Name = name });
            await _db.SaveChangesAsync();

            var resource = await _db.FindApiFacingResourceAsync(name);
            Assert.NotNull(resource);
            Assert.Equal(nameof(Volume), resource.ResourceTypeId);
        }

        [Fact]
        public void UserScopeTokenRequestValidator_CanBeConstructed()
        {
            var validator = new UserScopeTokenRequestValidator(_db);
            Assert.NotNull(validator);
        }
    }
}
