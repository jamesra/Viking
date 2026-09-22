using System.Configuration;
using Viking.Tokens;

namespace Viking.UI.WPF.ViewModels
{
    /// <summary>
    /// Reads the Identity client secret from appSettings (ApiClientSecret) with env/fallback via <see cref="IdentityClientSecret"/>.
    /// </summary>
    internal static class IdentityAppSettings
    {
        public static string ClientSecret =>
            IdentityClientSecret.Resolve(ConfigurationManager.AppSettings["ApiClientSecret"]);
    }
}
