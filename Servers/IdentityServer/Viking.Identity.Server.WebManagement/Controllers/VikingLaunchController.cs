using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Viking.Identity.Data;
using Viking.Identity.Models;

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

        /// <summary>Default expiry for a launch code (e.g. 5 minutes).</summary>
        private static readonly TimeSpan CodeExpiry = TimeSpan.FromMinutes(5);

        public VikingLaunchController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

            var code = Guid.NewGuid().ToString("N");
            var launchCode = new VikingLaunchCode
            {
                Code = code,
                UserId = userId,
                VolumeUrl = string.IsNullOrWhiteSpace(volume) ? null : volume.Trim(),
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
    }
}
