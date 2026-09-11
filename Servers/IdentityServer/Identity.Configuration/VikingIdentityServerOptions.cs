using System.Linq;
using Duende.IdentityServer.Models;

namespace Viking.Identity.Server
{
    public class VikingIdentityServerOptions
    {
        public string Secret { get; set; } = string.Empty; // Must be configured via appsettings or environment variable

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