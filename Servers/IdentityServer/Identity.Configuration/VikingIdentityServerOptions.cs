using System;
using System.Linq;
using Duende.IdentityServer.Models;

namespace Viking.Identity.Server
{
    public class VikingIdentityServerOptions
    {
        /// <summary>
        /// OAuth client secret for the Viking desktop client only.
        /// Must not be reused for api, mvc, or ro.viking.
        /// </summary>
        public string Secret { get; set; } = string.Empty;

        /// <summary>
        /// Confidential secret for the api client (launch-exchange, viking_user_token, introspection).
        /// </summary>
        public string ApiSecret { get; set; } = string.Empty;

        /// <summary>
        /// Confidential secret for the mvc (WebManagement) client.
        /// </summary>
        public string MvcSecret { get; set; } = string.Empty;

        /// <summary>
        /// Confidential secret for the ro.viking client.
        /// </summary>
        public string RoVikingSecret { get; set; } = string.Empty;

        public string Authority { get; set; }

        public string MetadataAddress { get; set; }

        public ApiScope[] ApiScopes { get; set; } = new ApiScope[]
        {
            new ApiScope(name: "Viking.Annotation", displayName:"Access to Annotate a volume")
        };

        /// <summary>Space-separated API scope names for token requests (e.g. launch code exchange).</summary>
        public string ApiScopeNames => ApiScopes != null && ApiScopes.Length > 0
            ? string.Join(" ", ApiScopes.Select(s => s.Name))
            : "Viking.Annotation";

        /// <summary>
        /// Returns the dedicated secret for an OAuth client id.
        /// api/mvc/ro.viking must not fall back to <see cref="Secret"/>; a leaked Viking
        /// desktop secret must not authenticate those clients.
        /// </summary>
        public string GetClientSecret(string clientId)
        {
            ArgumentException.ThrowIfNullOrEmpty(clientId);

            var (value, propertyName) = clientId switch
            {
                "Viking" => (Secret, nameof(Secret)),
                "api" => (ApiSecret, nameof(ApiSecret)),
                "mvc" or "web" => (MvcSecret, nameof(MvcSecret)),
                "ro.viking" => (RoVikingSecret, nameof(RoVikingSecret)),
                _ => throw new InvalidOperationException($"Unknown OAuth client id '{clientId}'.")
            };

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"VikingIdentityServerOptions.{propertyName} must be configured for client '{clientId}'.");
            }

            return value;
        }
    }
}
