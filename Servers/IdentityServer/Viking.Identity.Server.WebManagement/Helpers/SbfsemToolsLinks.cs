using System;

namespace Viking.Identity.Server.WebManagement.Helpers
{
    /// <summary>
    /// Builds deep links into sbfsem-tools using Identity volume names (not renderer ids).
    /// </summary>
    public static class SbfsemToolsLinks
    {
        public const string DefaultOpenBaseUrl = "https://sbfsem-tools.com/open";

        /// <summary>
        /// Returns https://sbfsem-tools.com/open?volume={escaped Identity name}.
        /// </summary>
        public static string OpenVolume(string volumeName, string openBaseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(volumeName))
            {
                throw new ArgumentException("Volume name is required.", nameof(volumeName));
            }

            var baseUrl = string.IsNullOrWhiteSpace(openBaseUrl) ? DefaultOpenBaseUrl : openBaseUrl.TrimEnd('/');
            return $"{baseUrl}?volume={Uri.EscapeDataString(volumeName.Trim())}";
        }
    }
}
