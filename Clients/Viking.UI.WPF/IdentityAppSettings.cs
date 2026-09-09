using System.Configuration;
using Viking.Tokens;

namespace Viking.UI.WPF
{
    internal static class IdentityAppSettings
    {
        public static string ClientSecret =>
            IdentityClientSecret.Resolve(ConfigurationManager.AppSettings["ApiClientSecret"]);
    }
}
