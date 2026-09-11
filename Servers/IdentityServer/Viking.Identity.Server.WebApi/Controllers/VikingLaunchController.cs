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
    /// Viking calls POST /api/viking/launch-exchange with the code and receives a volume-scoped
    /// access_token + identity_server_url + volume_url (+ volume_name when known).
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
            [System.Text.Json.Serialization.JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("identity_server_url")]
            public string IdentityServerUrl { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("volume_url")]
            public string VolumeUrl { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("volume_name")]
            public string VolumeName { get; set; }
        }

        /// <summary>
        /// Exchanges a one-use launch code for a volume-scoped API token and optional volume URL.
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
            var volumeName = await ResolveVolumeNameAsync(launchCode);

            string accessToken;
            try
            {
                accessToken = await MintAccessTokenAsync(httpClient: null, tokenEndpoint, launchCode, volumeName);
            }
            catch (LaunchTokenException ex)
            {
                await ReleaseLaunchCodeAsync(code, now);
                _logger.LogWarning("Launch exchange token mint failed: {Error}", ex.Message);
                return StatusCode(ex.StatusCode, new { error = ex.Message });
            }
            catch (Exception ex)
            {
                await ReleaseLaunchCodeAsync(code, now);
                _logger.LogError(ex, "Launch exchange: failed to call Identity Server token endpoint");
                return StatusCode(503, new { error = "identity service unavailable" });
            }

            return Ok(new LaunchExchangeResponse
            {
                AccessToken = accessToken,
                IdentityServerUrl = authority,
                VolumeUrl = launchCode.VolumeUrl ?? "",
                VolumeName = volumeName ?? ""
            });
        }

        private async Task<string> ResolveVolumeNameAsync(VikingLaunchCode launchCode)
        {
            if (!string.IsNullOrWhiteSpace(launchCode.VolumeName))
                return launchCode.VolumeName.Trim();

            if (string.IsNullOrWhiteSpace(launchCode.VolumeUrl))
                return null;

            var url = launchCode.VolumeUrl.Trim().TrimEnd('/');
            var volumes = await _context.Volume.AsNoTracking().ToListAsync();
            foreach (var v in volumes)
            {
                if (v.Endpoint == null)
                    continue;
                if (string.Equals(v.Endpoint.ToString().TrimEnd('/'), url, StringComparison.OrdinalIgnoreCase))
                    return v.Name;
            }

            return null;
        }

        private async Task<string> MintAccessTokenAsync(HttpClient httpClient, string tokenEndpoint, VikingLaunchCode launchCode, string volumeName)
        {
            using var ownedClient = httpClient == null ? _httpClientFactory.CreateClient() : null;
            var client = httpClient ?? ownedClient;

            // Prefer a Viking client token with volume scopes when we know the volume.
            if (!string.IsNullOrWhiteSpace(volumeName))
            {
                var resource = await _context.Resource
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Name == volumeName);

                if (resource != null)
                {
                    var permissionsQuery = await _context.UserResourcePermissions(launchCode.UserId, resource.Id);
                    var permissions = await permissionsQuery.ToListAsync();
                    if (permissions != null && permissions.Count > 0)
                    {
                        var scopeParts = new List<string>
                        {
                            "openid",
                            "profile",
                            "Viking.Annotation"
                        };
                        foreach (var p in permissions)
                            scopeParts.Add($"{volumeName}.{p}");

                        var volumeToken = await RequestTokenAsync(client, tokenEndpoint, new Dictionary<string, string>
                        {
                            ["grant_type"] = "viking_user_token",
                            ["user_id"] = launchCode.UserId,
                            ["client_id"] = "Viking",
                            ["client_secret"] = _identityOptions.Secret ?? "",
                            ["scope"] = string.Join(" ", scopeParts)
                        });

                        if (!string.IsNullOrEmpty(volumeToken))
                            return volumeToken;

                        _logger.LogWarning("Launch exchange: Viking volume-scoped token request failed; falling back to api token");
                    }
                    else
                    {
                        _logger.LogWarning("Launch exchange: user {UserId} has no permissions on volume {VolumeName}", launchCode.UserId, volumeName);
                    }
                }
            }

            // Fallback: api client token (openid/profile + configured API scopes).
            var apiScopes = "openid profile " + _identityOptions.ApiScopeNames;
            var apiToken = await RequestTokenAsync(client, tokenEndpoint, new Dictionary<string, string>
            {
                ["grant_type"] = "viking_user_token",
                ["user_id"] = launchCode.UserId,
                ["client_id"] = "api",
                ["client_secret"] = _identityOptions.Secret ?? "",
                ["scope"] = apiScopes
            });

            if (string.IsNullOrEmpty(apiToken))
                throw new LaunchTokenException(502, "invalid token response");

            return apiToken;
        }

        private async Task<string> RequestTokenAsync(HttpClient httpClient, string tokenEndpoint, Dictionary<string, string> form)
        {
            using var content = new FormUrlEncodedContent(form);
            using var tokenResponse = await httpClient.PostAsync(tokenEndpoint, content);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var body = await tokenResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Launch exchange: token request failed {StatusCode} {Body}", tokenResponse.StatusCode, body);
                return null;
            }

            var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(tokenJson);
            return doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
        }

        private async Task ReleaseLaunchCodeAsync(string code, DateTime claimedAtUtc)
        {
            await _context.VikingLaunchCodes
                .Where(c => c.Code == code && c.UsedAtUtc == claimedAtUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAtUtc, (DateTime?)null));
        }

        private sealed class LaunchTokenException : Exception
        {
            public int StatusCode { get; }
            public LaunchTokenException(int statusCode, string message) : base(message) => StatusCode = statusCode;
        }
    }
}
