using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Viking.GrpcSectionCorrectionService
{
    /// <summary>
    /// Settings that locate Identity and name this correction service. Bound from <c>IdentityServer</c> and
    /// <c>CorrectionService</c> configuration, which Docker supplies through environment variables.
    /// </summary>
    public sealed class IdentityAssignmentOptions
    {
        /// <summary>Identity server authority (token endpoint host), e.g. https://identity.example:5001/.</summary>
        public string Endpoint { get; init; }

        /// <summary>Permissions WebApi base URL that serves AssignedAnnotationContexts. Defaults to <see cref="Endpoint"/>.</summary>
        public string ApiEndpoint { get; init; }

        /// <summary>OAuth client id; the Identity "correction" client by default.</summary>
        public string ClientId { get; init; } = "correction";

        /// <summary>Secret for <see cref="ClientId"/>. Supplied by Docker configuration, never stored in the repo.</summary>
        public string ClientSecret { get; init; }

        /// <summary>Name of the CorrectionService resource in Identity whose assignments this container serves.</summary>
        public string ServiceName { get; init; }

        /// <summary>True when Identity, a secret and a service name are configured, so assignments come from Identity.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(ServiceName) && !string.IsNullOrWhiteSpace(ClientSecret);

        /// <summary>Reads <c>IdentityServer:*</c> and <c>CorrectionService:Name</c>.</summary>
        public static IdentityAssignmentOptions FromConfiguration(IConfiguration configuration) => new()
        {
            Endpoint = configuration["IdentityServer:Endpoint"],
            ApiEndpoint = configuration["IdentityServer:ApiEndpoint"],
            ClientId = string.IsNullOrWhiteSpace(configuration["IdentityServer:ClientId"]) ? "correction" : configuration["IdentityServer:ClientId"],
            ClientSecret = configuration["IdentityServer:ClientSecret"],
            ServiceName = configuration["CorrectionService:Name"]
        };
    }

    /// <summary>
    /// Stands in when Identity assignment settings are missing. Serving existing corrections does not need Identity,
    /// so the host still starts; listing or rebuilding volumes reports exactly what to configure.
    /// </summary>
    public sealed class UnconfiguredIdentityVolumeSource : IIdentityVolumeSource
    {
        public Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "The correction service is not connected to Identity. Set IdentityServer:Endpoint, "
                + "IdentityServer:ClientSecret and CorrectionService:Name (environment variables IdentityServer__Endpoint, "
                + "IdentityServer__ClientSecret, CorrectionService__Name); IdentityServer:ApiEndpoint is optional.");
    }

    /// <summary>
    /// Lists the annotation contexts Identity assigned to this correction service. Authenticates with the
    /// client-credentials grant, then calls <c>Permissions/AssignedAnnotationContexts</c>. Because the container may
    /// start before Identity is reachable, <see cref="ListAsync"/> retries with capped exponential backoff.
    /// Thread-safe: holds no state between calls and caches no token.
    /// </summary>
    public sealed class IdentityAssignmentClient : IIdentityVolumeSource
    {
        readonly HttpClient _http;
        readonly IdentityAssignmentOptions _options;
        readonly ILogger<IdentityAssignmentClient> _logger;
        readonly int _maxAttempts;
        readonly TimeSpan _firstDelay;

        /// <param name="http">Client used for token and API calls.</param>
        /// <param name="options">Identity location, credentials and this service's name.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="maxAttempts">Attempts before the failure is surfaced to the caller.</param>
        /// <param name="firstDelay">Delay after the first failure; doubles up to one minute.</param>
        public IdentityAssignmentClient(
            HttpClient http,
            IdentityAssignmentOptions options,
            ILogger<IdentityAssignmentClient> logger,
            int maxAttempts = 8,
            TimeSpan? firstDelay = null)
        {
            _http = http;
            _options = options;
            _logger = logger;
            _maxAttempts = Math.Max(1, maxAttempts);
            _firstDelay = firstDelay ?? TimeSpan.FromSeconds(2);
        }

        /// <summary>
        /// Returns one row per assigned annotation context. Throws after the final failed attempt, or
        /// <see cref="OperationCanceledException"/> when cancelled while waiting to retry.
        /// </summary>
        public async Task<IReadOnlyList<IdentityVolumeRow>> ListAsync(CancellationToken cancellationToken)
        {
            TimeSpan delay = _firstDelay;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    return await FetchAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < _maxAttempts && IsTransient(ex))
                {
                    _logger.LogWarning(ex, "Identity assignments unavailable (attempt {Attempt}/{Max}); retrying in {Delay}.",
                        attempt, _maxAttempts, delay);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    delay = TimeSpan.FromSeconds(Math.Min(60, delay.TotalSeconds * 2));
                }
            }
        }

        // A wrong secret or an unknown service name will not fix itself, so only network-level and 5xx failures retry.
        static bool IsTransient(Exception ex) =>
            ex is not HttpRequestException http
            || http.StatusCode is null
            || (int)http.StatusCode >= 500
            || http.StatusCode == HttpStatusCode.RequestTimeout;

        async Task<IReadOnlyList<IdentityVolumeRow>> FetchAsync(CancellationToken cancellationToken)
        {
            string token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);
            Uri api = new(_options.ApiEndpoint is { Length: > 0 } ? _options.ApiEndpoint : _options.Endpoint);
            Uri url = new(api, $"Permissions/AssignedAnnotationContexts?service={Uri.EscapeDataString(_options.ServiceName)}");

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            List<AssignedContext> assigned = await response.Content
                .ReadFromJsonAsync<List<AssignedContext>>(cancellationToken).ConfigureAwait(false) ?? [];
            return [.. assigned.Select(a => new IdentityVolumeRow(
                a.Name, a.Endpoint, a.AnnotationEndpoint, a.AnnotationDatabaseName, a.RegistrationName, a.ODataEndpoint))];
        }

        async Task<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            Uri tokenUrl = new(new Uri(_options.Endpoint), "connect/token");
            using FormUrlEncodedContent form = new(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = "Viking.Annotation"
            });
            using HttpResponseMessage response = await _http.PostAsync(tokenUrl, form, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            TokenResponse token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken).ConfigureAwait(false);
            return token?.AccessToken ?? throw new InvalidOperationException("Identity returned no access_token.");
        }

        sealed record TokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

        // Mirrors Viking.Identity.Server.Extensions.Services.AssignedAnnotationContext (camelCase JSON).
        sealed class AssignedContext
        {
            public string Name { get; set; }
            public string Endpoint { get; set; }
            public string RegistrationName { get; set; }
            public string AnnotationDatabaseName { get; set; }
            public string AnnotationEndpoint { get; set; }
            public string ODataEndpoint { get; set; }
        }
    }
}
