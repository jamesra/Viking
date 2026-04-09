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
         
    }
}