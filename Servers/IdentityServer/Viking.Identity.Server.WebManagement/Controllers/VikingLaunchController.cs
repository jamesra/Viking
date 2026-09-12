using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        /// </summary>
        /// <param name="volume">Optional volume endpoint URL (used by Identity UI "Open in Viking").</param>
        /// <param name="volumeName">Optional Identity volume name (e.g. RC2); resolved to Endpoint.</param>
        /// <param name="location">Location ID or x,y,z[,downsample] string; appended to viking:// URL only.</param>
        /// <param name="x">Optional volume X when launching by coordinates.</param>
        /// <param name="y">Optional volume Y when launching by coordinates.</param>
        /// <param name="z">Optional section Z when launching by coordinates.</param>
        /// <param name="ds">Optional downsample when launching by coordinates.</param>
        [HttpGet]
        public async Task<IActionResult> CreateCode(
            [FromQuery] string volume = null,
            [FromQuery] string volumeName = null,
            [FromQuery] string location = null,
            [FromQuery] string x = null,
            [FromQuery] string y = null,
            [FromQuery] string z = null,
            [FromQuery] string ds = null)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            string volumeUrl = string.IsNullOrWhiteSpace(volume) ? null : volume.Trim();
            string resolvedVolumeName = null;

            if (!string.IsNullOrWhiteSpace(volumeName))
            {
                var name = volumeName.Trim();
                var namedVolume = await _context.Volume
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Name == name);

                if (namedVolume == null)
                {
                    namedVolume = await _context.Volume
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Name != null && v.Name.ToLower() == name.ToLower());
                }

                if (namedVolume == null || namedVolume.Endpoint == null)
                {
                    return NotFound(new { error = $"Unknown volumeName '{name}'" });
                }

                volumeUrl = namedVolume.Endpoint.ToString();
                resolvedVolumeName = namedVolume.Name;
            }
            else if (!string.IsNullOrWhiteSpace(volumeUrl))
            {
                // Derive Identity name from endpoint when only the URL is supplied (Identity UI buttons).
                var match = await FindVolumeByEndpointAsync(volumeUrl);
                if (match != null)
                    resolvedVolumeName = match.Name;
            }

            var code = Guid.NewGuid().ToString("N");
            var launchCode = new VikingLaunchCode
            {
                Code = code,
                UserId = userId,
                VolumeUrl = volumeUrl,
                VolumeName = resolvedVolumeName,
                ExpiresAtUtc = DateTime.UtcNow.Add(CodeExpiry)
            };
            _context.VikingLaunchCodes.Add(launchCode);
            await _context.SaveChangesAsync();

            var vikingUrl = new StringBuilder("viking://open?code=" + Uri.EscapeDataString(code));
            if (!string.IsNullOrEmpty(launchCode.VolumeUrl))
            {
                vikingUrl.Append("&volume=").Append(Uri.EscapeDataString(launchCode.VolumeUrl));
            }

            AppendQueryIfPresent(vikingUrl, "location", location);
            AppendQueryIfPresent(vikingUrl, "x", x);
            AppendQueryIfPresent(vikingUrl, "y", y);
            AppendQueryIfPresent(vikingUrl, "z", z);
            AppendQueryIfPresent(vikingUrl, "ds", ds);

            return Redirect(vikingUrl.ToString());
        }

        private async Task<Volume> FindVolumeByEndpointAsync(string volumeUrl)
        {
            var volumes = await _context.Volume.AsNoTracking().ToListAsync();
            foreach (var v in volumes)
            {
                if (v.Endpoint == null)
                    continue;
                if (string.Equals(v.Endpoint.ToString().TrimEnd('/'), volumeUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                    return v;
            }
            return null;
        }

        private static void AppendQueryIfPresent(StringBuilder url, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            url.Append('&').Append(key).Append('=').Append(Uri.EscapeDataString(value.Trim()));
        }
    }
}
