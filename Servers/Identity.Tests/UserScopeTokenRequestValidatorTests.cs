using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using IdentityModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.WebManagement.Extensions;
using Xunit;

namespace TestIdentityModel
{
    /// <summary>
    /// Launch-exchange uses viking_user_token, which sets Subject and leaves UserName empty.
    /// </summary>
    public class UserScopeTokenRequestValidatorTests
    {
        [Fact]
        public async Task VolumeScope_ExtensionGrant_ResolvesUserBySubject()
        {
            await using var db = CreateDb();
            var userId = db.CreateUser("launch-user", "x");
            var volume = new Volume { Name = "RC2", ResourceTypeId = nameof(Volume) };
            db.Volume.Add(volume);
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission
            {
                PermissionId = Special.Permissions.Volume.Read,
                ResourceId = volume.Id,
                UserId = userId
            });
            await db.SaveChangesAsync();

            var validator = new UserScopeTokenRequestValidator(db);
            var context = CreateContext(userName: null, subjectId: userId, scopeName: "RC2.Read");
            await validator.ValidateAsync(context);

            Assert.False(context.Result.IsError);
        }

        [Fact]
        public async Task VolumeScope_ExtensionGrant_NoSubject_UserNotFound()
        {
            await using var db = CreateDb();
            db.Volume.Add(new Volume { Name = "RC2", ResourceTypeId = nameof(Volume) });
            await db.SaveChangesAsync();

            var validator = new UserScopeTokenRequestValidator(db);
            var context = CreateContext(userName: null, subjectId: null, scopeName: "RC2.Read");
            await validator.ValidateAsync(context);

            Assert.True(context.Result.IsError);
            Assert.Equal("user not found", context.Result.Error);
        }

        [Fact]
        public async Task VolumeScope_ResourceOwner_ResolvesUserByUserName()
        {
            await using var db = CreateDb();
            var userId = db.CreateUser("ropc-user", "x");
            var volume = new Volume { Name = "RC2", ResourceTypeId = nameof(Volume) };
            db.Volume.Add(volume);
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission
            {
                PermissionId = Special.Permissions.Volume.Read,
                ResourceId = volume.Id,
                UserId = userId
            });
            await db.SaveChangesAsync();

            var validator = new UserScopeTokenRequestValidator(db);
            var context = CreateContext(userName: "ropc-user", subjectId: null, scopeName: "RC2.Read");
            await validator.ValidateAsync(context);

            Assert.False(context.Result.IsError);
        }

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options, new PasswordHasher<ApplicationUser>());
            db.Database.EnsureCreated();
            return db;
        }

        private static CustomTokenRequestValidationContext CreateContext(string userName, string subjectId, string scopeName)
        {
            var request = new ValidatedTokenRequest
            {
                UserName = userName,
                ValidatedResources = new ResourceValidationResult(new Resources(
                    Array.Empty<IdentityResource>(),
                    Array.Empty<ApiResource>(),
                    new[] { new ApiScope(scopeName) }))
            };

            if (!string.IsNullOrEmpty(subjectId))
            {
                request.Subject = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(JwtClaimTypes.Subject, subjectId) },
                    "viking_user_token"));
            }

            return new CustomTokenRequestValidationContext
            {
                Result = new TokenRequestValidationResult(request)
            };
        }
    }
}
