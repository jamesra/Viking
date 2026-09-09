using System;

namespace Viking.Tokens
{
    /// <summary>
    /// Resolves the shared Identity Server client secret used by Viking, VikingAU, and AnnotationService.
    /// Callers may pass an explicit app-setting value; otherwise IDENTITY_SERVER_SECRET is used.
    /// </summary>
    public static class IdentityClientSecret
    {
        public const string EnvironmentVariableName = "IDENTITY_SERVER_SECRET";

        /// <summary>
        /// Returns <paramref name="configuredValue"/> when set; otherwise the IDENTITY_SERVER_SECRET environment variable.
        /// </summary>
        /// <exception cref="InvalidOperationException">Neither an explicit value nor the environment variable is set.</exception>
        public static string Resolve(string configuredValue = null)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue))
                return configuredValue;

            var fromEnv = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            if (!string.IsNullOrWhiteSpace(fromEnv))
                return fromEnv;

            throw new InvalidOperationException(
                $"Identity client secret is not configured. Set the {EnvironmentVariableName} environment variable, or ApiClientSecret / VikingClientSecret / IdentityServerClientSecret in app settings.");
        }
    }
}
