using System;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Xunit;

namespace TestIdentityModel
{
    public class ResourceScopeNamesTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly ApplicationDbContext _db;

        public ResourceScopeNamesTests(InMemoryIdentityFixture fixture)
        {
            _db = fixture.DataContext;
        }

        [Fact]
        public void ToScope_ReplacesSpacesInVolumeName()
        {
            var scope = ResourceScopeNames.ToScope("gRPC RC1 Test", Special.Permissions.Volume.Read);
            Assert.Equal("gRPC-RC1-Test.Read", scope);
        }

        [Fact]
        public void ToScope_LeavesNamesWithoutSpacesUnchanged()
        {
            var scope = ResourceScopeNames.ToScope("RC1", Special.Permissions.Volume.Read);
            Assert.Equal("RC1.Read", scope);
        }

        [Fact]
        public void TryParse_SplitsOnLastDot()
        {
            Assert.True(ResourceScopeNames.TryParse("gRPC-RC1-Test.Read", out var prefix, out var permission));
            Assert.Equal("gRPC-RC1-Test", prefix);
            Assert.Equal("Read", permission);
            Assert.Equal(Special.Permissions.Volume.Read, ResourceScopeNames.ToPermissionId(permission));
        }

        [Fact]
        public void ToPermissionId_RestoresAccessManagerSpaces()
        {
            var encoded = ResourceScopeNames.ToScopePrefix(Special.Permissions.SegmentationService.AccessManager);
            Assert.Equal("Access-Manager", encoded);
            Assert.Equal(Special.Permissions.SegmentationService.AccessManager, ResourceScopeNames.ToPermissionId(encoded));
        }

        [Fact]
        public async Task FindApiFacingResource_EncodedPrefix_FindsVolumeWithSpaces()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var volume = new Volume { Name = $"gRPC RC1 {unique}" };
            _db.Volume.Add(volume);
            await _db.SaveChangesAsync();

            var encoded = ResourceScopeNames.ToScopePrefix(volume.Name);
            var found = await _db.FindApiFacingResourceAsync(encoded);
            Assert.NotNull(found);
            Assert.Equal(volume.Id, found.Id);

            var foundByDisplayName = await _db.FindApiFacingResourceAsync(volume.Name);
            Assert.NotNull(foundByDisplayName);
            Assert.Equal(volume.Id, foundByDisplayName.Id);
        }

        [Fact]
        public async Task FindApiFacingResource_UnspacedName_StillMatches()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var volume = new Volume { Name = $"RC1{unique}" };
            _db.Volume.Add(volume);
            await _db.SaveChangesAsync();

            var found = await _db.FindApiFacingResourceAsync(volume.Name);
            Assert.NotNull(found);
            Assert.Equal(volume.Id, found.Id);
        }

        [Fact]
        public async Task IsResourceNameTaken_DetectsEncodedPrefixCollision()
        {
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var existing = new Volume { Name = $"Foo-{unique}" };
            _db.Volume.Add(existing);
            await _db.SaveChangesAsync();

            var colliding = $"Foo {unique}";
            Assert.True(ResourceScopeNames.ScopePrefixesCollide(existing.Name, colliding));
            Assert.True(_db.IsResourceNameTaken(colliding, nameof(Volume)));
            Assert.False(_db.IsResourceNameTaken($"Bar {unique}", nameof(Volume)));
        }
    }
}
