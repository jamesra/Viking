using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Extensions.Services
{
    public class CollaboratorOnboardingResult
    {
        public long OrganizationalUnitId { get; set; }
        public string OrganizationalUnitName { get; set; }
        public long VolumeId { get; set; }
        public string VolumeName { get; set; }
        public string InviteToken { get; set; }
        public bool ExistingUserGranted { get; set; }
        public string CollaboratorEmail { get; set; }
    }

    public class CollaboratorInviteInfo
    {
        public string Token { get; set; }
        public string Email { get; set; }
        public string OrganizationalUnitName { get; set; }
        public string VolumeName { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Orchestrates lab org + volume creation and collaborator invite lifecycle.
    /// Delegates persistence to <see cref="ResourceProvisioningService"/>.
    /// </summary>
    public class CollaboratorOnboardingService
    {
        public static readonly TimeSpan DefaultInviteLifetime = TimeSpan.FromDays(14);

        private readonly ApplicationDbContext _context;
        private readonly ResourceProvisioningService _provisioning;
        private readonly UserManager<ApplicationUser> _userManager;

        public CollaboratorOnboardingService(
            ApplicationDbContext context,
            ResourceProvisioningService provisioning,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _provisioning = provisioning;
            _userManager = userManager;
        }

        public async Task<CollaboratorOnboardingResult> CreateLabAndInviteAsync(
            string orgName,
            string orgDescription,
            long? parentOrgId,
            string volumeName,
            string volumeDescription,
            Uri vikingXmlUrl,
            string collaboratorEmail,
            string createdByUserId)
        {
            if (string.IsNullOrWhiteSpace(collaboratorEmail))
                throw new ArgumentException("Collaborator email is required.", nameof(collaboratorEmail));

            if (_context.IsResourceNameTaken(orgName, nameof(OrganizationalUnit)))
                throw new InvalidOperationException($"An organizational unit named {orgName} already exists.");

            if (_context.IsResourceNameTaken(volumeName, nameof(Volume)))
                throw new InvalidOperationException($"A volume named {volumeName} already exists.");

            if (_context.Database.IsRelational())
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var result = await CreateLabAndInviteCoreAsync(
                        orgName, orgDescription, parentOrgId, volumeName, volumeDescription,
                        vikingXmlUrl, collaboratorEmail, createdByUserId);
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            return await CreateLabAndInviteCoreAsync(
                orgName, orgDescription, parentOrgId, volumeName, volumeDescription,
                vikingXmlUrl, collaboratorEmail, createdByUserId);
        }

        private async Task<CollaboratorOnboardingResult> CreateLabAndInviteCoreAsync(
            string orgName,
            string orgDescription,
            long? parentOrgId,
            string volumeName,
            string volumeDescription,
            Uri vikingXmlUrl,
            string collaboratorEmail,
            string createdByUserId)
        {
            var org = await _provisioning.CreateOrganizationalUnitAsync(orgName, orgDescription, parentOrgId);
            await _provisioning.GrantSiteAdminsOrgUnitAdminAsync(org.Id);

            var volume = await _provisioning.CreateVolumeAsync(volumeName, volumeDescription, org.Id, vikingXmlUrl);

            var result = new CollaboratorOnboardingResult
            {
                OrganizationalUnitId = org.Id,
                OrganizationalUnitName = org.Name,
                VolumeId = volume.Id,
                VolumeName = volume.Name,
                CollaboratorEmail = collaboratorEmail.Trim()
            };

            var existingUser = await _userManager.FindByEmailAsync(result.CollaboratorEmail);
            if (existingUser != null)
            {
                await _provisioning.GrantUserOrgUnitAdminAsync(existingUser.Id, org.Id);
                await _provisioning.GrantUserVolumeFullAccessAsync(existingUser.Id, volume.Id);
                result.ExistingUserGranted = true;
                return result;
            }

            var now = DateTime.UtcNow;
            var invite = new CollaboratorInvite
            {
                Token = Guid.NewGuid().ToString("N"),
                Email = result.CollaboratorEmail,
                OrganizationalUnitId = org.Id,
                VolumeId = volume.Id,
                CreatedByUserId = createdByUserId,
                CreatedAtUtc = now,
                ExpiresAtUtc = now.Add(DefaultInviteLifetime)
            };

            _context.CollaboratorInvites.Add(invite);
            await _context.SaveChangesAsync();

            result.InviteToken = invite.Token;
            return result;
        }

        public async Task<CollaboratorInviteInfo> GetInviteInfoAsync(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new CollaboratorInviteInfo
                {
                    IsValid = false,
                    ErrorMessage = "Invite token is missing."
                };
            }

            var invite = await _context.CollaboratorInvites
                .Include(i => i.OrganizationalUnit)
                .Include(i => i.Volume)
                .FirstOrDefaultAsync(i => i.Token == token);

            if (invite == null)
            {
                return new CollaboratorInviteInfo
                {
                    IsValid = false,
                    ErrorMessage = "Invite was not found."
                };
            }

            if (invite.ClaimedAtUtc.HasValue)
            {
                return new CollaboratorInviteInfo
                {
                    Token = token,
                    Email = invite.Email,
                    IsValid = false,
                    ErrorMessage = "Invite has already been used."
                };
            }

            if (invite.ExpiresAtUtc < DateTime.UtcNow)
            {
                return new CollaboratorInviteInfo
                {
                    Token = token,
                    Email = invite.Email,
                    IsValid = false,
                    ErrorMessage = "Invite has expired."
                };
            }

            return new CollaboratorInviteInfo
            {
                Token = invite.Token,
                Email = invite.Email,
                OrganizationalUnitName = invite.OrganizationalUnit?.Name,
                VolumeName = invite.Volume?.Name,
                IsValid = true
            };
        }

        public async Task RedeemInviteAsync(string token, string userId, string userEmail)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentException("Invite token is required.", nameof(token));

            if (_context.Database.IsRelational())
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    await RedeemInviteCoreAsync(token, userId, userEmail);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
                return;
            }

            await RedeemInviteCoreAsync(token, userId, userEmail);
        }

        private async Task RedeemInviteCoreAsync(string token, string userId, string userEmail)
        {
            var invite = await _context.CollaboratorInvites
                .FirstOrDefaultAsync(i => i.Token == token);

            if (invite == null)
                throw new InvalidOperationException("Invite was not found.");

            if (invite.ClaimedAtUtc.HasValue)
                throw new InvalidOperationException("Invite has already been used.");

            if (invite.ExpiresAtUtc < DateTime.UtcNow)
                throw new InvalidOperationException("Invite has expired.");

            if (!string.Equals(invite.Email, userEmail?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Registration email does not match the invite.");

            // Claim first so concurrent redeem attempts fail before grants.
            invite.ClaimedAtUtc = DateTime.UtcNow;
            invite.ClaimedByUserId = userId;
            await _context.SaveChangesAsync();

            await _provisioning.GrantUserOrgUnitAdminAsync(userId, invite.OrganizationalUnitId);
            await _provisioning.GrantUserVolumeFullAccessAsync(userId, invite.VolumeId);
        }
    }
}
