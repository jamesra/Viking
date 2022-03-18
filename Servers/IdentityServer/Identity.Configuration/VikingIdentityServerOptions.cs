using IdentityServer4.Models;

namespace Viking.Identity.Server
{
    public class VikingIdentityServerOptions
    {
        public string Secret { get; set; } = "CorrectHorseBatteryStaple";

        public string Authority { get; set; }
          
        public ApiScope[] ApiScopes { get; set; } = new ApiScope[]
        {
            new ApiScope(name: "Viking.Annotation", displayName:"Access to Annotate a volume")
        }; 
         
    }
}