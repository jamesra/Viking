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

namespace Viking.Identity.Server.WebApi.ApiControllers
{
    /// <summary>
    /// One-use launch code exchange for the viking://open protocol.
    /// Viking calls POST /api/viking/launch-exchange with the code and receives access_token + identity_server_url + volume_url.
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

        public VikingLaunchController(
            ApplicationDbContext context,
            IHttpClientFactory httpClientFactory,
            IOptions<VikingIdentityServerOptions> identityOptions,
            ILogger<VikingLaunchController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _identityOptions = identityOptions?.Value ?? throw new ArgumentNullException(nameof(identityOptions));
            _logger = logger;
        }

        /// <summary>
        /// Request body for launch code exchange.
        /// </summary>
        public class LaunchExchangeRequest
        {
            public string Code { get; set; }
        }

        /// <summary>
        /// Response body for successful launch code exchange.
        /// </summary>
        public class LaunchExchangeResponse
        {
            public string AccessToken { get; set; }
            public string IdentityServerUrl { get; set; }
            public string VolumeUrl { get; set; }
        }

        /// <summary>
        /// Exchanges a one-use launch code for an API token and optional volume URL.
        /// No bearer auth required. Code is invalidated after first successful use.
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

            launchCode.UsedAtUtc = now;
            await _context.SaveChangesAsync();

            var authority = _identityOptions.Authority?.TrimEnd('/') ?? "";
            var tokenEndpoint = authority + "/connect/token";
            var scopes = "openid profile " + _identityOptions.ApiScopeNames;

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "viking_user_token",
                ["user_id"] = launchCode.UserId,
                ["client_id"] = "api",
                ["client_secret"] = _identityOptions.Secret ?? "",
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
                _logger.LogError(ex, "Launch exchange: failed to call Identity Server token endpoint");
                return StatusCode(503, new { error = "identity service unavailable" });
            }

            if (!tokenResponse.IsSuccessStatusCode)
            {
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
                _logger.LogWarning("Launch exchange: no access_token in response");
                return StatusCode(502, new { error = "invalid token response" });
            }

            var response = new LaunchExchangeResponse
            {
                AccessToken = accessToken,
                IdentityServerUrl = authority,
                VolumeUrl = launchCode.VolumeUrl ?? ""
            };

            return Ok(response);
        }
    }
}
