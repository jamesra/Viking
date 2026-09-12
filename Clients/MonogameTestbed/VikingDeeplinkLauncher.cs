using System;
using System.Diagnostics;
using System.Globalization;

namespace MonogameTestbed
{
    /// <summary>
    /// Builds and launches <c>viking://open?volume=…&amp;location=…</c> URLs so the OS protocol
    /// handler starts Viking at an annotation or volume-space coordinate.
    /// </summary>
    static class VikingDeeplinkLauncher
    {
        /// <summary>
        /// Maps a testbed OData endpoint (e.g. <c>…/RC1/OData</c>) to the volume directory URL Viking expects.
        /// Viking appends <c>volume.vikingxml</c> when the path has no file name.
        /// </summary>
        public static bool TryGetVolumeUrlFromODataEndpoint(Uri endpointUri, out string volumeUrl)
        {
            volumeUrl = null;
            if (endpointUri is null)
                return false;

            string s = endpointUri.GetLeftPart(UriPartial.Path).TrimEnd('/');
            if (s.EndsWith("/OData", StringComparison.OrdinalIgnoreCase))
                s = s[..^"/OData".Length];

            if (string.IsNullOrWhiteSpace(s))
                return false;

            volumeUrl = s;
            return true;
        }

        public static string BuildOpenLocationUrl(string volumeUrl, ulong locationId) =>
            $"viking://open?volume={Uri.EscapeDataString(volumeUrl)}&location={locationId.ToString(CultureInfo.InvariantCulture)}";

        public static string BuildOpenCoordinateUrl(string volumeUrl, double x, double y, double z)
        {
            string coords = string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2}",
                x, y, z);
            return $"viking://open?volume={Uri.EscapeDataString(volumeUrl)}&location={Uri.EscapeDataString(coords)}";
        }

        /// <summary>
        /// Starts the registered <c>viking://</c> handler. Returns false when the OS cannot launch the URL
        /// (often because Viking is not registered); <paramref name="error"/> is then a short user-facing reason.
        /// </summary>
        public static bool TryLaunch(string vikingUrl, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(vikingUrl))
            {
                error = "No Viking URL to open.";
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo(vikingUrl) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                error = $"Could not open Viking ({ex.Message}). Is viking:// registered?";
                Trace.WriteLine($"[VikingDeeplink] {error}");
                return false;
            }
        }
    }
}
