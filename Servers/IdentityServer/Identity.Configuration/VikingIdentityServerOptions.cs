using System.Linq;
using Duende.IdentityServer.Models;

namespace Viking.Identity.Server
{
    public class VikingIdentityServerOptions
    {
        public string Secret { get; set; } = string.Empty; // Must be configured via appsettings or environment variable

        /// <summary>Optional per-client secret for ro.viking (resource owner password grant). If empty, <see cref="Secret"/> is used.</summary>
        public string RoVikingSecret { get; set; } = string.Empty;

        /// <summary>
        /// Secret for the third-party sbfsem-tools client. This is deliberately separate from
        /// <see cref="Secret"/> so an external site never receives the first-party shared secret.
        /// When empty the client is not served at all.
        /// </summary>
        public string SbfsemToolsClientSecret { get; set; } = string.Empty;

        /// <summary>Redirect URIs accepted for the sbfsem-tools authorization code flow.</summary>
        public string[] SbfsemToolsRedirectUris { get; set; } = new[]
        {
            "https://sbfsem-tools.com/auth/callback"
        };

        /// <summary>Post logout redirect URIs accepted for the sbfsem-tools client.</summary>
        public string[] SbfsemToolsPostLogoutRedirectUris { get; set; } = new[]
        {
            "https://sbfsem-tools.com/"
        };

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
    }
}