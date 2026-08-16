using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Xunit;

namespace TestIdentityModel
{
    public class PermissionsEveryoneTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly InMemoryIdentityFixture _fixture;
        private readonly ApplicationDbContext _db;

        public PermissionsEveryoneTests(InMemoryIdentityFixture fixture)
        {
            _fixture = fixture;
            _db = fixture.DataContext;
        }

        [Fact]
        public async Task UserWithoutEveryoneAssignment_StillGetsEveryoneGroupPermission()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = _fixture.CreateUserWithoutEveryoneAssignment($"everyone-{unique}");

            Assert.False(await _db.UserToGroupAssignments.AnyAsync(a =>
                a.UserId == userId && a.GroupId == Special.Groups.Everyone.Id));

            var volume = new Volume { Name = $"EveryoneVol-{unique}" };
            _db.Volume.Add(volume);
            await _db.SaveChangesAsync();

            _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                GroupId = Special.Groups.Everyone.Id,
                ResourceId = volume.Id,
                PermissionId = Special.Permissions.Volume.Read
            });
            await _db.SaveChangesAsync();

            Assert.True(await _db.IsUserPermitted(volume.Id, userId, Special.Permissions.Volume.Read));
            Assert.False(await _db.IsUserPermitted(volume.Id, userId, Special.Permissions.Volume.Review));
        }

        [Fact]
        public async Task UserResourcePermissionsByType_IncludesEveryoneGroupGrants()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = _fixture.CreateUserWithoutEveryoneAssignment($"everyone-type-{unique}");

            var volume = new Volume { Name = $"EveryoneTypeVol-{unique}" };
            _db.Volume.Add(volume);
            await _db.SaveChangesAsync();

            _db.GrantedGroupPermissions.Add(new GrantedGroupPermission
            {
                GroupId = Special.Groups.Everyone.Id,
                ResourceId = volume.Id,
                PermissionId = Special.Permissions.Volume.Annotate
            });
            await _db.SaveChangesAsync();

            var byType = await _db.UserResourcePermissionsByType(userId, new[] { nameof(Volume) });
            Assert.True(byType.ContainsKey(volume.Id));
            Assert.Contains(Special.Permissions.Volume.Annotate, byType[volume.Id]);
        }

        [Fact]
        public async Task CreateUser_AddsEveryoneAssignment()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = _db.CreateUser($"everyone-row-{unique}", "None");
            await _db.SaveChangesAsync();

            Assert.True(await _db.UserToGroupAssignments.AnyAsync(a =>
                a.UserId == userId && a.GroupId == Special.Groups.Everyone.Id));
        }
    }
}
