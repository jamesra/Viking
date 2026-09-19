using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Viking.Identity.Models;
using Viking.Identity.Server.Extensions.Services;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    /// <summary>
    /// Creates one-use launch codes for the viking://open protocol.
    /// Browser cookie only (same as <see cref="SbfsemOpenController"/>). Listing Bearer/Introspection
    /// here 500s an unauthenticated browser: JwtBearer ForwardDefaultSelector returns an empty
    /// scheme name when there is no Authorization header, and ASP.NET throws before it can challenge.
    /// </summary>
    [Authorize(AuthenticationSchemes = Config.ApplicationCookieScheme)]
    public class VikingLaunchController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly VikingLaunchCodeService _launchCodes;
        private readonly WebApiOptions _webApiOptions;

        public VikingLaunchController(
            UserManager<ApplicationUser> userManager,
            VikingLaunchCodeService launchCodes,
            IOptions<WebApiOptions> webApiOptions)
        {
            _userManager = userManager;
            _launchCodes = launchCodes;
            _webApiOptions = webApiOptions?.Value ?? new WebApiOptions();
        }

        /// <summary>
        /// Creates a one-use launch code for the current user and redirects to viking://open.
        /// </summary>
        /// <param name="volume">Optional volume id, endpoint URL, or Identity name.</param>
        /// <param name="volumeName">Preferred Identity volume name (e.g. RC2). Takes precedence over <paramref name="volume"/> when both are set.</param>
        /// <param name="location">Optional Location ID, or <c>x,y,z[,downsample]</c> for a camera jump. Passed through on the viking:// URL (not stored).</param>
        [HttpGet]
        public async Task<IActionResult> CreateCode(
            [FromQuery] string volume = null,
            [FromQuery] string volumeName = null,
            [FromQuery] string location = null)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge(IdentityConstants.ApplicationScheme);
            }

            var volumeKey = !string.IsNullOrWhiteSpace(volumeName) ? volumeName.Trim() : volume?.Trim();
            Volume resolved = null;

            if (!string.IsNullOrWhiteSpace(volumeKey))
            {
                resolved = await _launchCodes.ResolveVolumeAsync(volumeKey);
                if (resolved == null)
                    return NotFound();

                if (!await _launchCodes.UserCanAccessVolumeAsync(resolved, User, userId))
                    return Forbid();
            }

            var launchCode = await _launchCodes.CreateAsync(userId, resolved);
            return Redirect(VikingLaunchCodeService.BuildOpenUrl(launchCode, location, _webApiOptions.BaseUrl));
        }
    }
}
