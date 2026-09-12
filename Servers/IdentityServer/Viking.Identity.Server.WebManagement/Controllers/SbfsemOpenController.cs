using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    /// <summary>
    /// Identity-first bounce into SBFSEM-tools /open so the browser holds an Identity cookie
    /// before SBFSEM's OIDC flow (silent SSO when already signed in).
    /// </summary>
    [Authorize(AuthenticationSchemes = Config.AuthenticationSchemes)]
    public class SbfsemOpenController : Controller
    {
        private readonly IConfiguration _configuration;

        public SbfsemOpenController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Requires an Identity browser session (Challenge if missing), then redirects to SBFSEM-tools /open.
        /// </summary>
        [HttpGet]
        public IActionResult Redirect(
            [FromQuery] string volume = null,
            [FromQuery] string cells = null,
            [FromQuery] string location = null)
        {
            if (string.IsNullOrWhiteSpace(volume))
                return BadRequest(new { error = "volume is required" });

            var openBase = _configuration["SbfsemToolsOptions:OpenUrl"]?.Trim();
            if (string.IsNullOrWhiteSpace(openBase))
                openBase = "https://sbfsem-tools.com/open";

            var uriBuilder = new UriBuilder(openBase);
            var query = $"volume={Uri.EscapeDataString(volume.Trim())}";
            if (!string.IsNullOrWhiteSpace(cells))
                query += $"&cells={Uri.EscapeDataString(cells.Trim())}";
            if (!string.IsNullOrWhiteSpace(location))
                query += $"&location={Uri.EscapeDataString(location.Trim())}";
            uriBuilder.Query = query;

            return Redirect(uriBuilder.Uri.AbsoluteUri);
        }
    }
}
