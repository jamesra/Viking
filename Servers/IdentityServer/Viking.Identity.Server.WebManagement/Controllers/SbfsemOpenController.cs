using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.WebManagement.Helpers;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    /// <summary>
    /// Identity-first bounce for Viking → SBFSEM-tools deep links.
    /// Browser cookie only, then 302 to sbfsem-tools /open with the same query.
    /// </summary>
    [Authorize(AuthenticationSchemes = Config.ApplicationCookieScheme)]
    public class SbfsemOpenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorization;
        private readonly SbfsemToolsOptions _options;

        public SbfsemOpenController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorization,
            IOptions<SbfsemToolsOptions> options)
        {
            _context = context;
            _userManager = userManager;
            _authorization = authorization;
            _options = options?.Value ?? new SbfsemToolsOptions();
        }

        /// <summary>
        /// Bounce entry used by Viking's "Open in SBFSEM-tools" menu.
        /// </summary>
        /// <param name="volume">Identity volume name (required).</param>
        /// <param name="cells">Top-level structure id(s), comma-separated.</param>
        /// <param name="location">Optional Location ID that was right-clicked.</param>
        /// <param name="technique">Optional SBFSEM render pathway.</param>
        [HttpGet]
        public async Task<IActionResult> Redirect(
            [FromQuery] string volume,
            [FromQuery] string cells = null,
            [FromQuery] string location = null,
            [FromQuery] string technique = null)
        {
            if (string.IsNullOrWhiteSpace(volume))
            {
                return BadRequest("volume is required");
            }

            var volumeName = volume.Trim();
            var resolved = await ResolveVolumeByNameAsync(volumeName);
            if (resolved == null)
            {
                return NotFound();
            }

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge(IdentityConstants.ApplicationScheme);
            }

            if (false == await UserCanAccessVolume(resolved, userId))
            {
                return Forbid();
            }

            var openBase = string.IsNullOrWhiteSpace(_options.OpenUrl)
                ? SbfsemToolsLinks.DefaultOpenBaseUrl
                : _options.OpenUrl.TrimEnd('/');

            var query = new List<string>
            {
                "volume=" + Uri.EscapeDataString(resolved.Name)
            };
            if (!string.IsNullOrWhiteSpace(cells))
            {
                query.Add("cells=" + Uri.EscapeDataString(cells.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                query.Add("location=" + Uri.EscapeDataString(location.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(technique))
            {
                query.Add("technique=" + Uri.EscapeDataString(technique.Trim()));
            }

            // Use RedirectResult: this action is also named Redirect and would recurse.
            return new RedirectResult(openBase + "?" + string.Join("&", query));
        }

        private async Task<Volume> ResolveVolumeByNameAsync(string volumeName)
        {
            return await _context.Volume
                .Include(v => v.Parent)
                .Include(v => v.UsersWithPermissions)
                .Include(v => v.GroupsWithPermissions)
                .FirstOrDefaultAsync(v => v.Name == volumeName);
        }

        private async Task<bool> UserCanAccessVolume(Volume volume, string userId)
        {
            if (User.IsInRole(Special.Roles.Admin))
                return true;

            var isParentAdmin = await _authorization.IsParentOrgUnitAdminAsync(HttpContext.User, volume);
            var hasDirectPermissions = volume.UsersWithPermissions?.Any(p => p.UserId == userId) == true;

            var userGroups = await _context.RecursiveMemberOfGroups(userId);
            var userGroupIds = userGroups.Select(g => g.Id).ToList();
            var hasGroupPermissions = userGroupIds.Any(groupId =>
                volume.GroupsWithPermissions?.Any(p => p.GroupId == groupId) == true);

            return isParentAdmin || hasDirectPermissions || hasGroupPermissions;
        }
    }
}
