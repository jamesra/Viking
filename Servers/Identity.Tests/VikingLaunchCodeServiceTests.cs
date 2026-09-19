using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    public class VikingLaunchCodeServiceTests
    {
        [Fact]
        public async Task CreateAsync_StoresVolumeNameAndUrl()
        {
            await using var db = CreateDb();
            var userId = db.CreateUser("launch-user", "x");
            var volume = new Volume
            {
                Name = "RPC1",
                ResourceTypeId = nameof(Volume),
                Endpoint = new System.Uri("http://rogue1.example/RPC1/SliceToVolume.VikingXML")
            };
            db.Volume.Add(volume);
            await db.SaveChangesAsync();

            var service = new VikingLaunchCodeService(db, new DenyAllAuthorization());
            var code = await service.CreateAsync(userId, volume);

            Assert.Equal("RPC1", code.VolumeName);
            Assert.Contains("RPC1", code.VolumeUrl);
            Assert.False(string.IsNullOrEmpty(code.Code));

            var url = VikingLaunchCodeService.BuildOpenUrl(code);
            Assert.StartsWith("viking://open?code=", url);
            Assert.Contains("volumeName=RPC1", url);
            Assert.DoesNotContain("&api=", url);
        }

        [Fact]
        public async Task UserCanAccessVolume_DirectGrant_Succeeds()
        {
            await using var db = CreateDb();
            var userId = db.CreateUser("granted", "x");
            var volume = new Volume { Name = "RPC1", ResourceTypeId = nameof(Volume) };
            db.Volume.Add(volume);
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission
            {
                PermissionId = Special.Permissions.Volume.Read,
                ResourceId = volume.Id,
                UserId = userId
            });
            await db.SaveChangesAsync();

            var resolved = await new VikingLaunchCodeService(db, new DenyAllAuthorization())
                .ResolveVolumeAsync("RPC1");
            Assert.NotNull(resolved);

            var allowed = await new VikingLaunchCodeService(db, new DenyAllAuthorization())
                .UserCanAccessVolumeAsync(resolved, new ClaimsPrincipal(), userId);
            Assert.True(allowed);
        }

        [Fact]
        public async Task UserCanAccessVolume_NoGrant_Denied()
        {
            await using var db = CreateDb();
            var userId = db.CreateUser("denied", "x");
            db.Volume.Add(new Volume { Name = "RPC1", ResourceTypeId = nameof(Volume) });
            await db.SaveChangesAsync();

            var service = new VikingLaunchCodeService(db, new DenyAllAuthorization());
            var volume = await service.ResolveVolumeAsync("RPC1");
            Assert.False(await service.UserCanAccessVolumeAsync(volume, new ClaimsPrincipal(), userId));
        }

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(System.Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options, new PasswordHasher<ApplicationUser>());
            db.Database.EnsureCreated();
            return db;
        }

        private sealed class DenyAllAuthorization : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
                Task.FromResult(AuthorizationResult.Failed());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
                Task.FromResult(AuthorizationResult.Failed());
        }
    }
}
