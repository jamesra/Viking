using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Viking.Identity.Server.WebManagement.Extensions;
using Xunit;

namespace TestIdentityModel
{
    public class AnnotationServiceUrlsTests
    {
        [Theory]
        [InlineData("https://webann.example/RC1/", "https://webann.example/RC1")]
        [InlineData("https://webann.example/RC1/Annotation/Service.svc", "https://webann.example/RC1")]
        [InlineData("https://webann.example/RC1/OData/", "https://webann.example/RC1")]
        [InlineData("https://webann.example/RC1/Export", "https://webann.example/RC1")]
        public void ToBaseUrl_StripsServiceSuffixes(string input, string expected)
        {
            Assert.Equal(expected, AnnotationServiceUrls.ToBaseUrl(new Uri(input)));
        }

        [Fact]
        public void DerivedUrls_FollowConnectomeLayout()
        {
            var server = new AnnotationServer { BaseUrl = "https://webann.example/RC1" };
            Assert.Equal("https://webann.example/RC1/Annotation/Service.svc", AnnotationServiceUrls.AnnotationUrl(server));
            Assert.Equal("https://webann.example/RC1/OData/", AnnotationServiceUrls.ODataUrl(server));
            Assert.Equal("https://webann.example/RC1/Export/", AnnotationServiceUrls.ExportUrl(server));
        }
    }

    public class VikingXmlCatalogTests
    {
        [Fact]
        public void Parse_ReadsVolumeToEndpoint()
        {
            var entry = VikingXmlCatalog.Parse(CatalogXml.WithEndpoint("RC1", "http://rogue1/RABBIT", "https://webann.example/RC1/"));

            Assert.Equal("RC1", entry.VolumeName);
            Assert.Equal("RC1db", entry.AnnotationDatabaseName);
            Assert.Equal(new Uri("https://webann.example/RC1/"), entry.AnnotationEndpoint);
            Assert.Equal(new Uri("https://export.example/RC1"), entry.ExportUrl);
            Assert.Equal(new Uri("https://identity.example:5001/"), entry.AuthenticationUrl);
            Assert.Null(entry.EndpointError);
            Assert.False(string.IsNullOrEmpty(entry.ContentHash));
        }

        [Fact]
        public void Parse_NoVolumeToEndpoint_IsImageOnly()
        {
            var entry = VikingXmlCatalog.Parse(CatalogXml.ImageOnly("Mosaics", "http://rogue1/Rohrer"));

            Assert.Null(entry.AnnotationEndpoint);
            Assert.Null(entry.EndpointError);
        }

        [Fact]
        public void Parse_RelativeEndpoint_ReportsError()
        {
            var entry = VikingXmlCatalog.Parse(CatalogXml.WithEndpoint("RC1", "http://rogue1/RABBIT", "/relative/path"));

            Assert.Null(entry.AnnotationEndpoint);
            Assert.NotNull(entry.EndpointError);
        }

        [Theory]
        [InlineData("https://WebAnn.Example:443/RC1/", "https://webann.example/RC1")]
        [InlineData("https://webann.example/RC1?x=1#frag", "https://webann.example/RC1")]
        [InlineData("http://webann.example:8080/RC1", "http://webann.example:8080/RC1")]
        public void NormalizeEndpoint_DropsCaseDefaultPortAndTrailingSlash(string input, string expected)
        {
            Assert.Equal(expected, VikingXmlCatalog.NormalizeEndpoint(new Uri(input)));
        }

        [Fact]
        public void ContentHash_IgnoresHostPathOnly()
        {
            var a = VikingXmlCatalog.Parse(CatalogXml.WithEndpoint("RC1", "http://rogue1/RABBIT", "https://webann.example/RC1"));
            var b = VikingXmlCatalog.Parse(CatalogXml.WithEndpoint("RC1", "https://storage1.example/RABBIT", "https://webann.example/RC1"));
            var c = VikingXmlCatalog.Parse(CatalogXml.WithEndpoint("RC1", "http://rogue1/RABBIT", "https://webann.example/RC1", sectionCount: 3));

            Assert.Equal(a.ContentHash, b.ContentHash);
            Assert.NotEqual(a.ContentHash, c.ContentHash);
        }
    }

    public class AnnotationServerCatalogSyncTests
    {
        [Fact]
        public async Task SyncAll_VolumesSharingAnEndpoint_ShareOneServerNamedAfterOldestVolume()
        {
            await using var db = CatalogDb.Create();
            var rc1 = CatalogDb.AddVolume(db, "RC1", "http://rogue1/RABBIT/rogue1.vikingxml");
            var internalCopy = CatalogDb.AddVolume(db, "RC1-Internal", "https://storage1/RABBIT/VolumeV2.VikingXML");
            await db.SaveChangesAsync();

            var source = new FakeXmlSource
            {
                ["http://rogue1/RABBIT/rogue1.vikingxml"] = CatalogXml.WithEndpoint("RC1", "http://rogue1/RABBIT", "https://webann.example/RC1/"),
                ["https://storage1/RABBIT/VolumeV2.VikingXML"] = CatalogXml.WithEndpoint("RC1", "https://storage1/RABBIT", "https://WEBANN.example/RC1")
            };

            var results = await new AnnotationServerCatalogSync(db, source).SyncAllVolumesAsync();

            Assert.All(results, r => Assert.Equal(CatalogSyncStatus.Linked, r.Status));
            var server = Assert.Single(db.AnnotationServers.ToList());
            Assert.Equal("RC1", server.Name);
            Assert.Equal("https://webann.example/RC1", server.BaseUrl);
            Assert.Equal("RC1db", server.AnnotationDatabaseName);
            Assert.Equal(server.Id, (await db.Volume.FindAsync(rc1.Id)).AnnotationServerId);
            Assert.Equal(server.Id, (await db.Volume.FindAsync(internalCopy.Id)).AnnotationServerId);

            // Volumes are never merged: each keeps its own image set with its URL as the first mirror.
            Assert.Equal(2, await db.ImageSets.CountAsync());
            var mirror = await db.ImageSetMirrors.SingleAsync(m => m.ImageSet.Volumes.Any(v => v.Id == rc1.Id));
            Assert.Equal(new Uri("http://rogue1/RABBIT/rogue1.vikingxml"), mirror.VikingXmlUrl);
            Assert.Equal(0, mirror.Priority);
        }

        [Fact]
        public async Task Sync_ImageOnlyVolume_GetsImageSetButNoServer()
        {
            await using var db = CatalogDb.Create();
            var mosaics = CatalogDb.AddVolume(db, "Mosaics", "http://rogue1/Rohrer/Mosaic.VikingXML");
            await db.SaveChangesAsync();

            var source = new FakeXmlSource { ["http://rogue1/Rohrer/Mosaic.VikingXML"] = CatalogXml.ImageOnly("Mosaics", "http://rogue1/Rohrer") };
            var result = await new AnnotationServerCatalogSync(db, source).SyncVolumeAsync(mosaics.Id);

            Assert.Equal(CatalogSyncStatus.ImageOnly, result.Status);
            Assert.Empty(db.AnnotationServers.ToList());
            var volume = await db.Volume.FindAsync(mosaics.Id);
            Assert.Null(volume.AnnotationServerId);
            Assert.NotNull(volume.ImageSetId);
            Assert.NotNull(volume.CatalogSyncedUtc);
        }

        [Fact]
        public async Task Sync_ImageOnlyVolume_KeepsManualAnnotationServerLink()
        {
            await using var db = CatalogDb.Create();
            var volume = CatalogDb.AddVolume(db, "RC2-Pitt", "http://storage/RC2/SliceToVolume.VikingXML");
            var server = new AnnotationServer
            {
                Name = "RC2",
                ResourceTypeId = nameof(AnnotationServer),
                BaseUrl = "https://webann.example/RC2"
            };
            db.AnnotationServers.Add(server);
            await db.SaveChangesAsync();
            await VolumeAnnotationServerLinks.ReplaceLinksAsync(db, volume, new[] { server.Id }, server.Id);
            await db.SaveChangesAsync();

            var source = new FakeXmlSource
            {
                ["http://storage/RC2/SliceToVolume.VikingXML"] = CatalogXml.ImageOnly("RC2-Pitt", "http://storage/RC2")
            };
            var result = await new AnnotationServerCatalogSync(db, source).SyncVolumeAsync(volume.Id);

            Assert.Equal(CatalogSyncStatus.ImageOnly, result.Status);
            var reloaded = await db.Volume.Include(v => v.AnnotationServerLinks).FirstAsync(v => v.Id == volume.Id);
            Assert.Equal(server.Id, reloaded.AnnotationServerId);
            Assert.Single(reloaded.AnnotationServerLinks);
        }

        [Fact]
        public async Task Sync_FetchFailure_KeepsExistingLinkAndRecordsError()
        {
            await using var db = CatalogDb.Create();
            var volume = CatalogDb.AddVolume(db, "RC2", "http://rogue1/RC2/SliceToVolume.VikingXML");
            await db.SaveChangesAsync();

            var source = new FakeXmlSource { ["http://rogue1/RC2/SliceToVolume.VikingXML"] = CatalogXml.WithEndpoint("RC2", "http://rogue1/RC2", "https://webann.example/RC2") };
            var sync = new AnnotationServerCatalogSync(db, source);
            await sync.SyncVolumeAsync(volume.Id);
            var linkedTo = (await db.Volume.FindAsync(volume.Id)).AnnotationServerId;

            source.Fail = true;
            var result = await sync.SyncVolumeAsync(volume.Id);

            Assert.Equal(CatalogSyncStatus.FetchFailed, result.Status);
            var reloaded = await db.Volume.FindAsync(volume.Id);
            Assert.Equal(linkedTo, reloaded.AnnotationServerId);
            Assert.Contains("Could not fetch", reloaded.CatalogSyncMessage);
        }

        [Fact]
        public async Task Sync_EditedVolumeUrl_BecomesPreferredMirror()
        {
            await using var db = CatalogDb.Create();
            var volume = CatalogDb.AddVolume(db, "RPC1", "http://rogue1/RPC1/SliceToVolume.VikingXML");
            await db.SaveChangesAsync();

            var source = new FakeXmlSource
            {
                ["http://rogue1/RPC1/SliceToVolume.VikingXML"] = CatalogXml.WithEndpoint("RPC1", "http://rogue1/RPC1", "https://webann.example/RPC1"),
                ["https://mirror.eu/RPC1/SliceToVolume.VikingXML"] = CatalogXml.WithEndpoint("RPC1", "https://mirror.eu/RPC1", "https://webann.example/RPC1")
            };
            var sync = new AnnotationServerCatalogSync(db, source);
            await sync.SyncVolumeAsync(volume.Id);

            volume.Endpoint = new Uri("https://mirror.eu/RPC1/SliceToVolume.VikingXML");
            await db.SaveChangesAsync();
            await sync.SyncVolumeAsync(volume.Id);

            var imageSet = await db.ImageSets.Include(i => i.Mirrors).SingleAsync();
            Assert.Equal(2, imageSet.Mirrors.Count);
            Assert.Equal(new Uri("https://mirror.eu/RPC1/SliceToVolume.VikingXML"), AnnotationServerCatalogSync.PrimaryMirror(imageSet).VikingXmlUrl);
        }

        [Fact]
        public async Task CheckMirrors_FlagsMirrorWithDifferentContent()
        {
            await using var db = CatalogDb.Create();
            var volume = CatalogDb.AddVolume(db, "RPC2", "http://rogue1/RPC2/SliceToVolume.VikingXML");
            await db.SaveChangesAsync();

            var source = new FakeXmlSource
            {
                ["http://rogue1/RPC2/SliceToVolume.VikingXML"] = CatalogXml.WithEndpoint("RPC2", "http://rogue1/RPC2", "https://webann.example/RPC2"),
                ["https://clone/RPC2.VikingXML"] = CatalogXml.WithEndpoint("RPC2", "https://clone/RPC2", "https://webann.example/RPC2"),
                ["https://other-build/RPC2.VikingXML"] = CatalogXml.WithEndpoint("RPC2", "https://other-build/RPC2", "https://webann.example/RPC2", sectionCount: 4)
            };
            var sync = new AnnotationServerCatalogSync(db, source);
            await sync.SyncVolumeAsync(volume.Id);

            var imageSet = await db.ImageSets.Include(i => i.Mirrors).SingleAsync();
            imageSet.Mirrors.Add(new ImageSetMirror { VikingXmlUrl = new Uri("https://clone/RPC2.VikingXML"), Priority = 1, Enabled = true });
            imageSet.Mirrors.Add(new ImageSetMirror { VikingXmlUrl = new Uri("https://other-build/RPC2.VikingXML"), Priority = 2, Enabled = true });
            await db.SaveChangesAsync();

            var checks = await sync.CheckMirrorsAsync(imageSet.Id);

            Assert.Equal(3, checks.Count);
            Assert.True(checks.Single(c => c.Url.Host == "clone").MatchesImageSet);
            Assert.False(checks.Single(c => c.Url.Host == "other-build").MatchesImageSet);
        }

        [Fact]
        public async Task Report_ListsUnlinkedVolumesAndCloneSuggestions()
        {
            await using var db = CatalogDb.Create();
            var a = CatalogDb.AddVolume(db, "Copy-A", "http://host-a/Vol.VikingXML");
            var b = CatalogDb.AddVolume(db, "Copy-B", "http://host-b/Vol.VikingXML");
            CatalogDb.AddVolume(db, "Unreachable", "http://nowhere/Vol.VikingXML");
            await db.SaveChangesAsync();

            var source = new FakeXmlSource
            {
                ["http://host-a/Vol.VikingXML"] = CatalogXml.ImageOnly("Vol", "http://host-a"),
                ["http://host-b/Vol.VikingXML"] = CatalogXml.ImageOnly("Vol", "http://host-b")
            };
            var sync = new AnnotationServerCatalogSync(db, source);
            await sync.SyncAllVolumesAsync();

            var report = await sync.BuildReportAsync();

            Assert.Equal(3, report.UnlinkedVolumes.Count);
            var clones = Assert.Single(report.CloneSuggestions);
            Assert.Equal(new[] { a.Id, b.Id }.OrderBy(i => i), clones.ImageSets.SelectMany(i => i.Volumes).Select(v => v.Id).OrderBy(i => i));
        }

        [Fact]
        public async Task ApplyPrimaryMirror_UpdatesVolumeEndpoint()
        {
            await using var db = CatalogDb.Create();
            var volume = CatalogDb.AddVolume(db, "RC2", "http://rogue1/RC2.VikingXML");
            await db.SaveChangesAsync();
            var source = new FakeXmlSource { ["http://rogue1/RC2.VikingXML"] = CatalogXml.ImageOnly("RC2", "http://rogue1") };
            var sync = new AnnotationServerCatalogSync(db, source);
            await sync.SyncVolumeAsync(volume.Id);

            var imageSet = await db.ImageSets.Include(i => i.Mirrors).SingleAsync();
            imageSet.Mirrors.Single().Enabled = false;
            imageSet.Mirrors.Add(new ImageSetMirror { VikingXmlUrl = new Uri("https://eu/RC2.VikingXML"), Priority = 5, Enabled = true });
            await sync.ApplyPrimaryMirrorAsync(imageSet);
            await db.SaveChangesAsync();

            Assert.Equal(new Uri("https://eu/RC2.VikingXML"), (await db.Volume.FindAsync(volume.Id)).Endpoint);
        }
    }

    public class AnnotationServerGrantCopyTests
    {
        [Fact]
        public async Task Copy_UnionsVolumeGrantsOntoServer_AndReportsWidenedAccess()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var alice = db.CreateUser("alice", "x");
            var group = new Group { Name = "Lab" };
            db.Group.Add(group);
            await db.SaveChangesAsync();

            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = rc1.Id, PermissionId = "Annotate", UserId = alice });
            db.GrantedGroupPermissions.Add(new GrantedGroupPermission { ResourceId = rc1.Id, PermissionId = "Read", GroupId = group.Id });
            db.GrantedGroupPermissions.Add(new GrantedGroupPermission { ResourceId = internalCopy.Id, PermissionId = "Read", GroupId = group.Id });
            await db.SaveChangesAsync();

            var report = await new AnnotationServerGrantCopy(db).CopyVolumeGrantsAsync();

            Assert.Equal(1, report.UserGrantsAdded);
            Assert.Equal(1, report.GroupGrantsAdded);
            Assert.True(await db.GrantedUserPermissions.AnyAsync(g => g.ResourceId == server.Id && g.UserId == alice && g.PermissionId == "Annotate"));
            Assert.True(await db.GrantedGroupPermissions.AnyAsync(g => g.ResourceId == server.Id && g.GroupId == group.Id && g.PermissionId == "Read"));

            // Volume rows are kept for rollback.
            Assert.True(await db.GrantedUserPermissions.AnyAsync(g => g.ResourceId == rc1.Id && g.UserId == alice));

            // Alice was only granted RC1; the group already had both volumes.
            var widened = Assert.Single(report.WidenedAccess);
            Assert.Equal("alice", widened.PrincipalName);
            Assert.Equal(new[] { "RC1" }, widened.SourceVolumes);
            Assert.Equal(new[] { "RC1-Internal" }, widened.NewlyReachableVolumes);

            var again = await new AnnotationServerGrantCopy(db).CopyVolumeGrantsAsync();
            Assert.Equal(0, again.UserGrantsAdded + again.GroupGrantsAdded);
        }
    }

    public class EffectiveVolumePermissionTests
    {
        [Fact]
        public async Task ServerGrant_ReachesEveryLinkedVolume_WithReadImplied()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("annotator", "x");
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = server.Id, PermissionId = "Annotate", UserId = userId });
            await db.SaveChangesAsync();

            var volumes = await db.UserVolumePermissionsAsync(userId);

            Assert.Equal(new[] { "Read", "Annotate" }, volumes[rc1.Id]);
            Assert.Equal(new[] { "Read", "Annotate" }, volumes[internalCopy.Id]);
            Assert.Equal(new[] { "Read", "Annotate" }, await db.UserEffectiveResourcePermissionsAsync(userId, server));
        }

        [Fact]
        public async Task ImageOnlyVolume_UsesItsOwnGrant()
        {
            await using var db = CatalogDb.Create();
            var mosaics = CatalogDb.AddVolume(db, "Mosaics", "http://rogue1/Mosaic.VikingXML");
            var userId = db.CreateUser("viewer", "x");
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = mosaics.Id, PermissionId = "Read", UserId = userId });
            await db.SaveChangesAsync();

            var volumes = await db.UserVolumePermissionsAsync(userId);

            Assert.Equal(new[] { "Read" }, volumes[mosaics.Id]);
            Assert.True(await db.IsUserPermittedEffectiveAsync(mosaics, userId, "Read"));
            Assert.False(await db.IsUserPermittedEffectiveAsync(mosaics, userId, "Annotate"));
        }

        [Fact]
        public async Task NoGrants_NoVolumes()
        {
            await using var db = CatalogDb.Create();
            await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("nobody", "x");
            await db.SaveChangesAsync();

            Assert.Empty(await db.UserVolumePermissionsAsync(userId));
        }

        [Fact]
        public async Task AnonymousServerGrant_ListsLinkedVolumes()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            db.GrantedGroupPermissions.Add(new GrantedGroupPermission { ResourceId = server.Id, PermissionId = "Read", GroupId = Special.Groups.Anonymous.Id });
            await db.SaveChangesAsync();

            var volumes = await db.AnonymousVolumePermissionsAsync();

            Assert.Equal(new[] { "Read" }, volumes[rc1.Id]);
        }

        [Fact]
        public async Task FindApiFacingResource_PrefersAnnotationServerOverSameNamedVolume()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, _) = await CatalogDb.AddSharedServerAsync(db);

            var resolved = await db.FindApiFacingResourceAsync("RC1");

            Assert.Equal(server.Id, resolved.Id);
            Assert.True(rc1.Id < server.Id, "Volume RC1 is older, so Id order alone would pick it.");
        }

        [Fact]
        public async Task LaunchAccess_ServerGrantOpensSiblingVolume()
        {
            await using var db = CatalogDb.Create();
            var (server, _, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("launcher", "x");
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = server.Id, PermissionId = "Read", UserId = userId });
            await db.SaveChangesAsync();

            var service = new VikingLaunchCodeService(db, new CatalogDenyAllAuthorization());
            var volume = await service.ResolveVolumeAsync("RC1-Internal");

            Assert.True(await service.UserCanAccessVolumeAsync(volume, new ClaimsPrincipal(), userId));
        }

        [Fact]
        public async Task GrantFullAccess_LandsOnAnnotationServerWhenLinked()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, internalCopy) = await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("collaborator", "x");
            await db.SaveChangesAsync();

            await new ResourceProvisioningService(db).GrantUserVolumeFullAccessAsync(userId, internalCopy.Id);

            Assert.Equal(3, await db.GrantedUserPermissions.CountAsync(g => g.UserId == userId && g.ResourceId == server.Id));
            Assert.False(await db.GrantedUserPermissions.AnyAsync(g => g.UserId == userId && g.ResourceId == internalCopy.Id));
            Assert.Equal(new[] { "Read", "Annotate", "Review" }, (await db.UserVolumePermissionsAsync(userId))[rc1.Id]);
        }

        [Fact]
        public async Task PermissionService_VolumeMetadataKeepsEndpointAndAddsCatalog()
        {
            await using var db = CatalogDb.Create();
            var (server, rc1, _) = await CatalogDb.AddSharedServerAsync(db);
            var imageSet = new ImageSet { Name = "RC1 images", VersionLabel = "v2", CreatedUtc = DateTime.UtcNow };
            imageSet.Mirrors.Add(new ImageSetMirror { VikingXmlUrl = new Uri("http://rogue1/RABBIT/rogue1.vikingxml"), Priority = 0, Enabled = true, RegionLabel = "US" });
            imageSet.Mirrors.Add(new ImageSetMirror { VikingXmlUrl = new Uri("https://eu/RABBIT/rogue1.vikingxml"), Priority = 1, Enabled = true, RegionLabel = "EU" });
            imageSet.Mirrors.Add(new ImageSetMirror { VikingXmlUrl = new Uri("https://off/RABBIT/rogue1.vikingxml"), Priority = 2, Enabled = false });
            rc1.ImageSet = imageSet;
            var userId = db.CreateUser("reader", "x");
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = server.Id, PermissionId = "Read", UserId = userId });
            await db.SaveChangesAsync();

            var service = new PermissionService(db, Microsoft.Extensions.Logging.Abstractions.NullLogger<PermissionService>.Instance, new NoDebugLogging());
            var volumes = await service.GetUserAccessibleVolumesAsync(userId);

            var metadata = volumes[rc1.Id].Metadata;
            Assert.Equal("http://rogue1/RABBIT/rogue1.vikingxml", metadata[VolumeMetadata.Endpoint]);
            Assert.Equal("RC1", metadata[VolumeMetadata.AnnotationServerName]);
            Assert.Equal("https://webann.example/RC1", metadata[VolumeMetadata.BaseUrl]);
            Assert.Equal("https://webann.example/RC1/Annotation/Service.svc", metadata[VolumeMetadata.AnnotationEndpoint]);
            Assert.Equal("https://webann.example/RC1/OData/", metadata[VolumeMetadata.ODataEndpoint]);
            var annotationServers = Assert.IsType<List<VolumeAnnotationServerInfo>>(metadata[VolumeMetadata.AnnotationServers]);
            Assert.Single(annotationServers);
            Assert.True(annotationServers[0].IsDefault);
            var mirrors = Assert.IsType<List<VolumeMirrorInfo>>(metadata[VolumeMetadata.Mirrors]);
            Assert.Equal(new[] { "US", "EU" }, mirrors.Select(m => m.Region));
            Assert.Equal("v2", Assert.IsType<VolumeImageSetInfo>(metadata[VolumeMetadata.ImageSet]).VersionLabel);
        }
    }

    public class LegacyVolumeScopeTests
    {
        [Fact]
        public async Task VolumeNameScope_AuthorizedByAnnotationServerGrant()
        {
            await using var db = CatalogDb.Create();
            var (server, _, _) = await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("scoped", "x");
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = server.Id, PermissionId = "Annotate", UserId = userId });
            await db.SaveChangesAsync();

            foreach (var scope in new[] { "RC1-Internal.Annotate", "RC1-Internal.Read", "RC1.Annotate", "RC1.Read" })
            {
                var context = TokenContext.Create(userId, scope);
                await new UserScopeTokenRequestValidator(db).ValidateAsync(context);
                Assert.False(context.Result.IsError, scope);
            }
        }

        [Fact]
        public async Task VolumeNameScope_DeniedWithoutTheGrant()
        {
            await using var db = CatalogDb.Create();
            var (server, _, _) = await CatalogDb.AddSharedServerAsync(db);
            var userId = db.CreateUser("reader-only", "x");
            await db.SaveChangesAsync();
            db.GrantedUserPermissions.Add(new GrantedUserPermission { ResourceId = server.Id, PermissionId = "Read", UserId = userId });
            await db.SaveChangesAsync();

            var context = TokenContext.Create(userId, "RC1-Internal.Review");
            await new UserScopeTokenRequestValidator(db).ValidateAsync(context);

            Assert.True(context.Result.IsError);
        }
    }

    internal static class TokenContext
    {
        public static CustomTokenRequestValidationContext Create(string subjectId, string scopeName)
        {
            var request = new ValidatedTokenRequest
            {
                ValidatedResources = new ResourceValidationResult(new Resources(
                    Array.Empty<IdentityResource>(),
                    Array.Empty<ApiResource>(),
                    new[] { new ApiScope(scopeName) })),
                Subject = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(JwtClaimTypes.Subject, subjectId) },
                    "viking_user_token"))
            };

            return new CustomTokenRequestValidationContext { Result = new TokenRequestValidationResult(request) };
        }
    }

    internal static class CatalogDb
    {
        public static ApplicationDbContext Create()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("Catalog-" + Guid.NewGuid().ToString("N"))
                .Options;
            var db = new ApplicationDbContext(options, new PasswordHasher<ApplicationUser>());
            db.Database.EnsureCreated();
            return db;
        }

        public static Volume AddVolume(ApplicationDbContext db, string name, string url)
        {
            var volume = new Volume { Name = name, ResourceTypeId = nameof(Volume), Endpoint = new Uri(url) };
            db.Volume.Add(volume);
            return volume;
        }

        /// <summary>Volumes RC1 and RC1-Internal pointing at annotation server RC1, created in that order.</summary>
        public static async Task<(AnnotationServer Server, Volume Rc1, Volume Internal)> AddSharedServerAsync(ApplicationDbContext db)
        {
            var rc1 = AddVolume(db, "RC1", "http://rogue1/RABBIT/rogue1.vikingxml");
            var internalCopy = AddVolume(db, "RC1-Internal", "https://storage1/RABBIT/VolumeV2.VikingXML");
            await db.SaveChangesAsync();

            var server = new AnnotationServer
            {
                Name = "RC1",
                ResourceTypeId = nameof(AnnotationServer),
                BaseUrl = "https://webann.example/RC1"
            };
            db.AnnotationServers.Add(server);
            rc1.AnnotationServer = server;
            internalCopy.AnnotationServer = server;
            await db.SaveChangesAsync();
            db.VolumeAnnotationServers.Add(new VolumeAnnotationServer
            {
                VolumeId = rc1.Id,
                AnnotationServerId = server.Id,
                IsDefault = true
            });
            db.VolumeAnnotationServers.Add(new VolumeAnnotationServer
            {
                VolumeId = internalCopy.Id,
                AnnotationServerId = server.Id,
                IsDefault = true
            });
            await db.SaveChangesAsync();
            return (server, rc1, internalCopy);
        }
    }

    internal static class CatalogXml
    {
        public static string WithEndpoint(string name, string path, string endpoint, int sectionCount = 2) => $@"
<Volume Name=""{name}"" path=""{path}"">
  <VolumeToEndpoint Name=""{name}db"" Endpoint=""{endpoint}"" ExportURL=""https://export.example/{name}"" Authentication=""https://identity.example:5001/"" />
  {Sections(sectionCount)}
</Volume>";

        public static string ImageOnly(string name, string path) => $@"
<Volume Name=""{name}"" path=""{path}"">
  {Sections(2)}
</Volume>";

        private static string Sections(int count) =>
            string.Concat(Enumerable.Range(1, count).Select(i => $@"<Section number=""{i}"" path=""{i:D4}"" />"));
    }

    internal sealed class FakeXmlSource : Dictionary<string, string>, IVikingXmlSource
    {
        public bool Fail { get; set; }

        public Task<string> FetchXmlAsync(Uri vikingXmlUrl, CancellationToken cancellationToken = default)
        {
            if (Fail || !TryGetValue(vikingXmlUrl.ToString(), out var xml))
                throw new System.Net.Http.HttpRequestException($"No response from {vikingXmlUrl}");
            return Task.FromResult(xml);
        }
    }

    internal sealed class CatalogDenyAllAuthorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Failed());
    }

    internal sealed class NoDebugLogging : IDebugLoggingService
    {
        public bool IsEnabled(DebugLogCategory category) => false;
        public void SetEnabled(DebugLogCategory category, bool enabled) { }
        public void SetGlobalEnabled(bool enabled) { }
        public DebugLoggingOptions GetOptions() => new DebugLoggingOptions();
        public void ResetToConfiguration() { }
    }
}
