using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

namespace Viking
{
    /// <summary>
    /// Shared parsing for viking://open query strings (startup and single-instance activation).
    /// </summary>
    public static class VikingDeepLinkParser
    {
        public static bool TryGetVolumeUrl(string vikingUrl, out string? volumeUrl)
        {
            volumeUrl = null;
            if (!Uri.TryCreate(vikingUrl, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri?.Query))
                return false;

            Dictionary<string, string> query = ParseQueryString(uri.Query);
            if (!query.TryGetValue("volume", out string? volume) || string.IsNullOrWhiteSpace(volume))
                return false;

            volumeUrl = volume.Trim();
            return true;
        }

        public static Dictionary<string, string> ParseQueryString(string query)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(query) || query[0] != '?')
                return dict;

            foreach (var pair in query.Substring(1).Split('&'))
            {
                var eq = pair.IndexOf('=');
                if (eq < 0)
                    continue;
                var key = Uri.UnescapeDataString(pair.Substring(0, eq).Replace('+', ' '));
                var value = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                dict[key] = value;
            }

            return dict;
        }

        /// <summary>
        /// Copies location / coordinate query params into a NameValueCollection.
        /// Location ID wins over coordinates when both are present.
        /// </summary>
        public static NameValueCollection ParsePlaceArguments(Dictionary<string, string> query)
        {
            var args = new NameValueCollection();

            if (query.TryGetValue("location", out string? locationRaw) && !string.IsNullOrWhiteSpace(locationRaw))
            {
                locationRaw = locationRaw.Trim();
                if (long.TryParse(locationRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long locationId))
                {
                    args["Location"] = locationId.ToString(CultureInfo.InvariantCulture);
                    return args;
                }

                string[] parts = locationRaw.Split(',');
                if (parts.Length >= 3)
                {
                    args["X"] = parts[0].Trim();
                    args["Y"] = parts[1].Trim();
                    args["Z"] = parts[2].Trim();
                    if (parts.Length >= 4)
                        args["DS"] = parts[3].Trim();
                    return args;
                }
            }

            CopyQueryKey(query, args, "x", "X");
            CopyQueryKey(query, args, "y", "Y");
            CopyQueryKey(query, args, "z", "Z");
            CopyQueryKey(query, args, "ds", "DS");
            return args;
        }

        private static void CopyQueryKey(Dictionary<string, string> query, NameValueCollection args, string queryKey, string startupKey)
        {
            if (query.TryGetValue(queryKey, out string? value) && !string.IsNullOrWhiteSpace(value))
                args[startupKey] = value.Trim();
        }
    }
}
