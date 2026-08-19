using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    /// <summary>
    /// Creates one-use launch codes for the viking://open protocol.
    /// When the user clicks "Open in Viking", we create a code and redirect to viking://open?code=...&volume=...
    /// </summary>
    [Authorize(AuthenticationSchemes = Config.AuthenticationSchemes)]
    public class VikingLaunchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorization;

        /// <summary>Default expiry for a launch code (e.g. 5 minutes).</summary>
        private static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(5);

        public VikingLaunchController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorization)
        {
            _context = context;
            _userManager = userManager;
            _authorization = authorization;
        }

        /// <summary>
        /// Creates a one-use launch code for the current user and redirects to viking://open.
        /// Optional <paramref name="volume"/> is the volume URL to open (e.g. .vikingxml endpoint).
        /// </summary>
        /// <param name="volume">Optional volume URL (will be URL-encoded in the redirect).</param>
        [HttpGet]
        public async Task<IActionResult> CreateCode([FromQuery] string volume = null)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var volumeUrl = string.IsNullOrWhiteSpace(volume) ? null : volume.Trim();
            if (volumeUrl != null)
            {
                var resolved = await ResolveVolumeAsync(volumeUrl);
                if (resolved == null)
                {
                    return NotFound();
                }

                if (false == await UserCanAccessVolume(resolved, userId))
                {
                    return Forbid();
                }

                volumeUrl = resolved.Endpoint?.ToString() ?? volumeUrl;
            }

            var code = Guid.NewGuid().ToString("N");
            var launchCode = new VikingLaunchCode
            {
                Code = code,
                UserId = userId,
                VolumeUrl = volumeUrl,
                ExpiresAtUtc = DateTime.UtcNow.Add(CodeExpiry)
            };
            _context.VikingLaunchCodes.Add(launchCode);
            await _context.SaveChangesAsync();

            var vikingUrl = "viking://open?code=" + Uri.EscapeDataString(code);
            if (!string.IsNullOrEmpty(launchCode.VolumeUrl))
            {
                vikingUrl += "&volume=" + Uri.EscapeDataString(launchCode.VolumeUrl);
            }

            return Redirect(vikingUrl);
        }

        private async Task<Volume> ResolveVolumeAsync(string volume)
        {
            var volumes = _context.Volume
                .Include(v => v.Parent)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions);

            if (long.TryParse(volume, out var id))
            {
                return await volumes.FirstOrDefaultAsync(v => v.Id == id);
            }

            return await volumes.FirstOrDefaultAsync(v => v.Endpoint != null && v.Endpoint.ToString() == volume)
                ?? await volumes.FirstOrDefaultAsync(v => v.Name == volume);
        }

        private async Task<bool> UserCanAccessVolume(Volume volume, string userId)
        {
            if (User.IsInRole(Special.Roles.Admin))
                return true;

            var isParentAdmin = await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume);
            var hasDirectPermissions = volume.UsersWithPermissions?.Any(p => p.UserId == userId) == true;

            var hasGroupPermissions = false;
            var userGroups = await _context.RecursiveMemberOfGroups(userId);
            var userGroupIds = userGroups.Select(g => g.Id).ToList();
            hasGroupPermissions = userGroupIds.Any(groupId =>
                volume.GroupsWithPermissions?.Any(p => p.GroupId == groupId) == true);

            return isParentAdmin || hasDirectPermissions || hasGroupPermissions;
        }
    }
}
