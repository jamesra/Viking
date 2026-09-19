using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;

namespace Viking.Identity.Server.Extensions.Services
{
    /// <summary>
    /// Shared minting for viking:// launch codes. Used by WebManagement CreateCode (cookie)
    /// and WebApi POST /api/viking/launch-code (sbfsem-tools bearer).
    /// </summary>
    public class VikingLaunchCodeService
    {
        /// <summary>One-use code lifetime. Returned as expires_in seconds on the API.</summary>
        public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;

        public VikingLaunchCodeService(ApplicationDbContext context, IAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        /// <summary>
        /// Resolves a volume by Identity name, numeric id, or endpoint URL.
        /// Name wins over endpoint so AccessibleVolumes names are unambiguous.
        /// </summary>
        public async Task<Volume> ResolveVolumeAsync(string volumeKey)
        {
            if (string.IsNullOrWhiteSpace(volumeKey))
                return null;

            var key = volumeKey.Trim();
            var volumes = _context.Volume
                .Include(v => v.Parent)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions);

            if (long.TryParse(key, out var id))
                return await volumes.FirstOrDefaultAsync(v => v.Id == id);

            return await volumes.FirstOrDefaultAsync(v => v.Name == key)
                ?? await volumes.FirstOrDefaultAsync(v => v.Endpoint != null && v.Endpoint.ToString() == key);
        }

        /// <summary>
        /// Same access rule as the management CreateCode action: site admin, parent OrgUnit admin,
        /// or any direct/group grant on the volume.
        /// </summary>
        public async Task<bool> UserCanAccessVolumeAsync(Volume volume, ClaimsPrincipal user, string userId)
        {
            if (volume == null || string.IsNullOrEmpty(userId))
                return false;

            if (user?.IsInRole(Special.Roles.Admin) == true
                || await _context.GetUsersInAdminRole().AnyAsync(u => u.Id == userId))
                return true;

            if (user != null && await _authorization.IsParentOrgUnitAdminAsync(user, volume))
                return true;

            if (volume.UsersWithPermissions?.Any(p => p.UserId == userId) == true)
                return true;

            var userGroups = await _context.RecursiveMemberOfGroups(userId);
            var userGroupIds = userGroups.Select(g => g.Id).ToList();
            return userGroupIds.Any(groupId =>
                volume.GroupsWithPermissions?.Any(p => p.GroupId == groupId) == true);
        }

        /// <summary>Persists a one-use code bound to <paramref name="userId"/> and optional volume.</summary>
        public async Task<VikingLaunchCode> CreateAsync(string userId, Volume volume)
        {
            ArgumentException.ThrowIfNullOrEmpty(userId);

            var launchCode = new VikingLaunchCode
            {
                Code = Guid.NewGuid().ToString("N"),
                UserId = userId,
                VolumeUrl = volume?.Endpoint?.ToString(),
                VolumeName = volume?.Name,
                ExpiresAtUtc = DateTime.UtcNow.Add(CodeLifetime)
            };
            _context.VikingLaunchCodes.Add(launchCode);
            await _context.SaveChangesAsync();
            return launchCode;
        }

        /// <summary>
        /// Builds viking://open. <paramref name="location"/> and <paramref name="apiBase"/> are
        /// query-only (not stored). Omit apiBase on the bearer-minted URL; the desktop uses
        /// LaunchExchangeBaseUrl and must not take an exchange host from the link.
        /// </summary>
        public static string BuildOpenUrl(VikingLaunchCode launchCode, string location = null, string apiBase = null)
        {
            ArgumentNullException.ThrowIfNull(launchCode);

            var vikingUrl = "viking://open?code=" + Uri.EscapeDataString(launchCode.Code);
            if (!string.IsNullOrEmpty(launchCode.VolumeUrl))
                vikingUrl += "&volume=" + Uri.EscapeDataString(launchCode.VolumeUrl);
            if (!string.IsNullOrWhiteSpace(launchCode.VolumeName))
                vikingUrl += "&volumeName=" + Uri.EscapeDataString(launchCode.VolumeName);
            if (!string.IsNullOrWhiteSpace(location))
                vikingUrl += "&location=" + Uri.EscapeDataString(location.Trim());
            if (!string.IsNullOrWhiteSpace(apiBase))
                vikingUrl += "&api=" + Uri.EscapeDataString(apiBase.Trim().TrimEnd('/'));
            return vikingUrl;
        }
    }
}
