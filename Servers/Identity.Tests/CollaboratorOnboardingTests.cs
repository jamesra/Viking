using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;
using Xunit;

namespace TestIdentityModel
{
    public class VikingXmlMetadataServiceTests
    {
        [Fact]
        public void Parse_ExtractsVolumeNameNotesAndInvestigator()
        {
            const string xml = @"
<Volume Name=""RC2"">
  <Section number=""1"">
    <Notes>Investigator: LabX
Experiment: Slice to volume</Notes>
  </Section>
  <Section number=""2"">
    <Notes>Other notes</Notes>
  </Section>
</Volume>";

            var metadata = VikingXmlMetadataService.Parse(xml);

            Assert.Equal("RC2", metadata.VolumeName);
            Assert.Equal("LabX", metadata.OrgNameSuggestion);
            Assert.Contains("Investigator: LabX", metadata.Description);
            Assert.Contains("Experiment: Slice to volume", metadata.Description);
        }

        [Fact]
        public void Parse_FallsBackToVolumeNameWhenNoInvestigator()
        {
            const string xml = @"
<Volume Name=""Yiu"">
  <Section number=""1"">
    <Notes>Just some notes</Notes>
  </Section>
</Volume>";

            var metadata = VikingXmlMetadataService.Parse(xml);

            Assert.Equal("Yiu", metadata.VolumeName);
            Assert.Equal("Yiu", metadata.OrgNameSuggestion);
            Assert.Equal("Just some notes", metadata.Description);
        }
    }

    public class ResourceProvisioningServiceTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly string _adminUserId;

        public ResourceProvisioningServiceTests(InMemoryIdentityFixture fixture)
        {
            _dbContext = fixture.DataContext;
            _adminUserId = fixture.AdminUserId;
        }

        [Fact]
        public async Task CreateOrganizationalUnit_AndGrantSiteAdmins()
        {
            var service = new ResourceProvisioningService(_dbContext);
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);

            var org = await service.CreateOrganizationalUnitAsync($"Org-{unique}", "Test org", null);
            await service.GrantSiteAdminsOrgUnitAdminAsync(org.Id);

            Assert.True(org.Id > 0);
            Assert.True(await _dbContext.IsUserPermitted(org.Id, _adminUserId, Special.Permissions.OrgUnit.Admin));
        }

        [Fact]
        public async Task CreateVolume_AndGrantFullAccessIdempotent()
        {
            var service = new ResourceProvisioningService(_dbContext);
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var userId = _dbContext.CreateUser($"voluser-{unique}", "None", $"voluser-{unique}@example.com");
            await _dbContext.SaveChangesAsync();

            var volume = await service.CreateVolumeAsync(
                $"Vol-{unique}",
                "desc",
                null,
                new Uri("http://example.com/Test.VikingXML"));

            await service.GrantUserVolumeFullAccessAsync(userId, volume.Id);
            await service.GrantUserVolumeFullAccessAsync(userId, volume.Id);

            Assert.Equal(3, await _dbContext.GrantedUserPermissions.CountAsync(p =>
                p.UserId == userId && p.ResourceId == volume.Id));
            Assert.True(await _dbContext.IsUserPermitted(volume.Id, userId, Special.Permissions.Volume.Read));
            Assert.True(await _dbContext.IsUserPermitted(volume.Id, userId, Special.Permissions.Volume.Annotate));
            Assert.True(await _dbContext.IsUserPermitted(volume.Id, userId, Special.Permissions.Volume.Review));
        }
    }

    public class CollaboratorOnboardingServiceTests : IClassFixture<InMemoryIdentityFixture>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly string _adminUserId;

        public CollaboratorOnboardingServiceTests(InMemoryIdentityFixture fixture)
        {
            _dbContext = fixture.DataContext;
            _adminUserId = fixture.AdminUserId;
        }

        private CollaboratorOnboardingService CreateService()
        {
            var provisioning = new ResourceProvisioningService(_dbContext);
            var userManager = CreateUserManager(_dbContext);
            return new CollaboratorOnboardingService(_dbContext, provisioning, userManager);
        }

        private static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext context)
        {
            var store = new UserStore<ApplicationUser>(context);
            return new UserManager<ApplicationUser>(
                store,
                Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                Array.Empty<IUserValidator<ApplicationUser>>(),
                Array.Empty<IPasswordValidator<ApplicationUser>>(),
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                null,
                null);
        }

        [Fact]
        public async Task CreateLabAndInvite_CreatesInviteForNewEmail()
        {
            var service = CreateService();
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var email = $"new-{unique}@example.com";

            var result = await service.CreateLabAndInviteAsync(
                $"Lab-{unique}",
                "lab desc",
                null,
                $"Vol-{unique}",
                "vol desc",
                new Uri("http://example.com/Lab.VikingXML"),
                email,
                _adminUserId);

            Assert.False(result.ExistingUserGranted);
            Assert.False(string.IsNullOrEmpty(result.InviteToken));
            Assert.Equal(email, result.CollaboratorEmail);

            var invite = await _dbContext.CollaboratorInvites.FindAsync(result.InviteToken);
            Assert.NotNull(invite);
            Assert.Equal(result.OrganizationalUnitId, invite.OrganizationalUnitId);
            Assert.Equal(result.VolumeId, invite.VolumeId);
        }

        [Fact]
        public async Task CreateLabAndInvite_GrantsExistingUserWithoutInvite()
        {
            var service = CreateService();
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var email = $"existing-{unique}@example.com";
            var userId = _dbContext.CreateUser($"existing-{unique}", "None", email);
            await _dbContext.SaveChangesAsync();

            var result = await service.CreateLabAndInviteAsync(
                $"LabExist-{unique}",
                "lab desc",
                null,
                $"VolExist-{unique}",
                "vol desc",
                new Uri("http://example.com/Exist.VikingXML"),
                email,
                _adminUserId);

            Assert.True(result.ExistingUserGranted);
            Assert.Null(result.InviteToken);
            Assert.True(await _dbContext.IsUserPermitted(result.OrganizationalUnitId, userId, Special.Permissions.OrgUnit.Admin));
            Assert.True(await _dbContext.IsUserPermitted(result.VolumeId, userId, Special.Permissions.Volume.Annotate));
        }

        [Fact]
        public async Task RedeemInvite_RejectsEmailMismatch()
        {
            var service = CreateService();
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);

            var created = await service.CreateLabAndInviteAsync(
                $"LabRedeem-{unique}",
                "lab",
                null,
                $"VolRedeem-{unique}",
                "vol",
                new Uri("http://example.com/Redeem.VikingXML"),
                $"invitee-{unique}@example.com",
                _adminUserId);

            var otherUserId = _dbContext.CreateUser($"other-{unique}", "None", $"other-{unique}@example.com");
            await _dbContext.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RedeemInviteAsync(created.InviteToken, otherUserId, $"other-{unique}@example.com"));
        }

        [Fact]
        public async Task RedeemInvite_GrantsPermissionsOnSuccess()
        {
            var service = CreateService();
            var unique = Guid.NewGuid().ToString("N").Substring(0, 8);
            var email = $"redeemok-{unique}@example.com";

            var created = await service.CreateLabAndInviteAsync(
                $"LabOk-{unique}",
                "lab",
                null,
                $"VolOk-{unique}",
                "vol",
                new Uri("http://example.com/Ok.VikingXML"),
                email,
                _adminUserId);

            var userId = _dbContext.CreateUser($"redeemok-{unique}", "None", email);
            await _dbContext.SaveChangesAsync();

            await service.RedeemInviteAsync(created.InviteToken, userId, email);

            Assert.True(await _dbContext.IsUserPermitted(created.OrganizationalUnitId, userId, Special.Permissions.OrgUnit.Admin));
            Assert.True(await _dbContext.IsUserPermitted(created.VolumeId, userId, Special.Permissions.Volume.Read));
            Assert.True(await _dbContext.IsUserPermitted(created.VolumeId, userId, Special.Permissions.Volume.Annotate));
            Assert.True(await _dbContext.IsUserPermitted(created.VolumeId, userId, Special.Permissions.Volume.Review));

            var invite = await _dbContext.CollaboratorInvites.FindAsync(created.InviteToken);
            Assert.NotNull(invite.ClaimedAtUtc);
            Assert.Equal(userId, invite.ClaimedByUserId);
        }
    }
}
