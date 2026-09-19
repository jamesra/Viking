using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server;
using Viking.Identity.Server.Extensions.Services;

namespace Viking.Identity.Server.WebApi.ApiControllers
{
    /// <summary>
    /// viking://open launch codes: anonymous exchange for the desktop client, and
    /// bearer minting for sbfsem-tools (POST launch-code).
    /// </summary>
    [ApiController]
    [Route("api/viking")]
    [Produces(MediaTypeNames.Application.Json)]
    public class VikingLaunchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly VikingIdentityServerOptions _identityOptions;
        private readonly ILogger<VikingLaunchController> _logger;
        private readonly IAuthenticationService _authService;
        private readonly VikingLaunchCodeService _launchCodes;

        public VikingLaunchController(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IOptions<VikingIdentityServerOptions> identityOptions,
            ILogger<VikingLaunchController> logger,
            IAuthenticationService authService,
            VikingLaunchCodeService launchCodes)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _identityOptions = identityOptions?.Value ?? throw new ArgumentNullException(nameof(identityOptions));
            _logger = logger;
            _authService = authService;
            _launchCodes = launchCodes;
        }

        /// <summary>
        /// Mints a one-use launch code for the token subject. Restricted to the sbfsem-tools
        /// client so a generic Viking.Annotation token cannot open a desktop session.
        /// Callers append <c>&amp;location=</c> to <c>viking_url</c> themselves.
        /// </summary>
        [HttpPost("launch-code")]
        public async Task<IActionResult> CreateLaunchCode([FromBody] LaunchCodeRequest request)
        {
            if (!OAuthTokenClient.IsSbfsemTools(User))
            {
                _logger.LogWarning("launch-code refused: client {ClientId} is not sbfsem-tools",
                    OAuthTokenClient.GetClientId(User) ?? "(none)");
                return Forbid();
            }

            var caller = await _authService.GetApplicationUserAsync(User, HttpContext);
            if (caller == null)
                return Unauthorized();

            var volumeName = request?.ResolvedVolumeName;
            if (string.IsNullOrWhiteSpace(volumeName))
                return BadRequest(new { error = "volume_name is required" });

            var volume = await _launchCodes.ResolveVolumeAsync(volumeName);
            if (volume == null)
                return NotFound();

            if (!await _launchCodes.UserCanAccessVolumeAsync(volume, User, caller.Id))
                return Forbid();

            var launchCode = await _launchCodes.CreateAsync(caller.Id, volume);
            return Ok(new LaunchCodeResponse
            {
                Code = launchCode.Code,
                ExpiresIn = (int)VikingLaunchCodeService.CodeLifetime.TotalSeconds,
                VikingUrl = VikingLaunchCodeService.BuildOpenUrl(launchCode)
            });
        }

        /// <summary>
        /// Exchanges a one-use launch code for an API token and optional volume URL.
        /// No bearer auth required. Code is invalidated after first successful use.
        /// Response keys are snake_case (<c>access_token</c>, …) to match installed Viking 1.2.61.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("launch-exchange")]
        public async Task<IActionResult> LaunchExchange([FromBody] LaunchExchangeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { error = "code is required" });
            }

            var code = request.Code.Trim();
            var now = DateTime.UtcNow;

            var launchCode = await _context.VikingLaunchCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Code == code);

            if (launchCode == null)
            {
                _logger.LogWarning("Launch exchange failed: code not found");
                return Unauthorized(new { error = "invalid or expired code" });
            }

            if (launchCode.UsedAtUtc.HasValue)
            {
                _logger.LogWarning("Launch exchange failed: code already used");
                return Unauthorized(new { error = "code already used" });
            }

            if (launchCode.ExpiresAtUtc < now)
            {
                _logger.LogWarning("Launch exchange failed: code expired");
                return Unauthorized(new { error = "code expired" });
            }

            // Atomically claim the code before requesting a token (prevents concurrent double-exchange).
            var claimed = await _context.VikingLaunchCodes
                .Where(c => c.Code == code && c.UsedAtUtc == null && c.ExpiresAtUtc >= now)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAtUtc, now));

            if (claimed == 0)
            {
                _logger.LogWarning("Launch exchange failed: code already claimed");
                return Unauthorized(new { error = "code already used" });
            }

            var authority = _identityOptions.Authority?.TrimEnd('/') ?? "";
            var tokenEndpoint = authority + "/connect/token";
            var scopes = "openid profile " + _identityOptions.ApiScopeNames;
            if (!string.IsNullOrWhiteSpace(launchCode.VolumeName))
            {
                var n = launchCode.VolumeName.Trim();
                scopes += $" {n}.Read {n}.Annotate {n}.Review";
            }

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "viking_user_token",
                ["user_id"] = launchCode.UserId,
                ["client_id"] = "api",
                ["client_secret"] = _identityOptions.GetClientSecret("api"),
                ["scope"] = scopes
            };

            using var httpClient = _httpClientFactory.CreateClient();
            using var content = new FormUrlEncodedContent(form);
            HttpResponseMessage tokenResponse;
            try
            {
                tokenResponse = await httpClient.PostAsync(tokenEndpoint, content);
            }
            catch (Exception ex)
            {
                await ReleaseLaunchCodeAsync(code, now);
                _logger.LogError(ex, "Launch exchange: failed to call Identity Server token endpoint");
                return StatusCode(503, new { error = "identity service unavailable" });
            }

            if (!tokenResponse.IsSuccessStatusCode)
            {
                await ReleaseLaunchCodeAsync(code, now);
                var body = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Launch exchange: token request failed {StatusCode} {Body}", tokenResponse.StatusCode, body);
                return Unauthorized(new { error = "token request failed" });
            }

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(tokenJson);
            var root = doc.RootElement;
            var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            if (string.IsNullOrEmpty(accessToken))
            {
                await ReleaseLaunchCodeAsync(code, now);
                _logger.LogWarning("Launch exchange: no access_token in response");
                return StatusCode(502, new { error = "invalid token response" });
            }

            var response = new LaunchExchangeResponse
            {
                AccessToken = accessToken,
                IdentityServerUrl = authority,
                VolumeUrl = launchCode.VolumeUrl ?? "",
                VolumeName = launchCode.VolumeName ?? ""
            };

            return Ok(response);
        }

        private async Task ReleaseLaunchCodeAsync(string code, DateTime claimedAtUtc)
        {
            await _context.VikingLaunchCodes
                .Where(c => c.Code == code && c.UsedAtUtc == claimedAtUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAtUtc, (DateTime?)null));
        }
    }
}
