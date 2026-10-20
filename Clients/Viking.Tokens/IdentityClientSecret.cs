using System;

namespace Viking.Tokens
{
    /// <summary>
    /// Resolves the Identity Server client secret used by Viking desktop login.
    /// App setting and IDENTITY_SERVER_SECRET win; the shipped default is last so existing installs keep working.
    /// </summary>
    public static class IdentityClientSecret
    {
        public const string EnvironmentVariableName = "IDENTITY_SERVER_SECRET";

        /// <summary>
        /// Secret baked into previous Viking desktop builds. Used only when neither an app setting nor the environment variable is set.
        /// </summary>
        public const string DesktopFallback = "Correct Horse Battery Staple";

        /// <summary>
        /// Returns <paramref name="configuredValue"/> when set; otherwise IDENTITY_SERVER_SECRET; otherwise <see cref="DesktopFallback"/>.
        /// </summary>
        public static string Resolve(string configuredValue = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue))
                return configuredValue;

            var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;

            return DesktopFallback;
        }
    }
}
