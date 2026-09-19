using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;

namespace Viking
{
    /// <summary>
    /// Parsed <c>viking://open</c> query used at startup and when activating an existing instance.
    /// </summary>
    public sealed class VikingDeepLink
    {
        public string? Code { get; set; }
        public string? VolumeUrl { get; set; }
        public string? VolumeName { get; set; }
        public NameValueCollection Place { get; set; } = [];
    }

    /// <summary>
    /// Shared parsing for viking://open query strings (startup and single-instance activation).
    /// Identity may send <c>volume</c> as an endpoint URL or as a volume name; <c>volumeName</c> is also accepted.
    /// </summary>
    public static class VikingDeepLinkParser
    {
        /// <summary>
        /// Finds a viking:// URL in process args and reassembles query pieces if the OS split on <c>&amp;</c>.
        /// </summary>
        public static bool TryFindOpenUrl(string[]? args, out string? vikingUrl)
        {
            vikingUrl = null;
            if (args is null || args.Length == 0)
                return false;

            int start = -1;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i]?.StartsWith("viking:", StringComparison.OrdinalIgnoreCase) == true)
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
                return false;

            string assembled = args[start] ?? string.Empty;
            for (int i = start + 1; i < args.Length; i++)
            {
                string? part = args[i];
                if (string.IsNullOrWhiteSpace(part) || part.StartsWith("-", StringComparison.Ordinal))
                    break;

                part = part.Trim();
                if (part.StartsWith("&", StringComparison.Ordinal))
                    part = part.Substring(1);
                if (part.IndexOf('=') < 0)
                    break;

                assembled += (assembled.IndexOf('?') >= 0 ? "&" : "?") + part;
            }

            vikingUrl = assembled;
            return true;
        }

        public static bool TryParse(string? vikingUrl, out VikingDeepLink? link)
        {
            link = null;
            if (string.IsNullOrWhiteSpace(vikingUrl))
                return false;

            if (!Uri.TryCreate(vikingUrl, UriKind.Absolute, out Uri? uri) || uri is null)
                return false;

            Dictionary<string, string> query = string.IsNullOrEmpty(uri.Query)
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ParseQueryString(uri.Query);

            link = FromQuery(query);
            return true;
        }

        public static VikingDeepLink FromQuery(Dictionary<string, string> query)
        {
            var link = new VikingDeepLink
            {
                Place = ParsePlaceArguments(query)
            };

            if (query.TryGetValue("code", out string? code) && !string.IsNullOrWhiteSpace(code))
                link.Code = code.Trim();

            if (query.TryGetValue("volumeName", out string? volumeName) && !string.IsNullOrWhiteSpace(volumeName))
                link.VolumeName = volumeName.Trim();

            if (query.TryGetValue("volume", out string? volume) && !string.IsNullOrWhiteSpace(volume))
            {
                volume = volume.Trim();
                if (LooksLikeVolumeUrl(volume))
                    link.VolumeUrl = volume;
                else if (string.IsNullOrWhiteSpace(link.VolumeName))
                    link.VolumeName = volume;
            }

            return link;
        }

        public static bool TryGetVolumeUrl(string vikingUrl, out string? volumeUrl)
        {
            volumeUrl = null;
            if (!TryParse(vikingUrl, out VikingDeepLink? link) || link is null)
                return false;

            if (string.IsNullOrWhiteSpace(link.VolumeUrl))
                return false;

            volumeUrl = link.VolumeUrl;
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

        /// <summary>
        /// Fills empty place keys from <paramref name="source"/> without replacing values already present.
        /// </summary>
        public static void MergePlace(NameValueCollection target, NameValueCollection? source)
        {
            if (source is null)
                return;

            foreach (string? key in source.AllKeys)
            {
                if (string.IsNullOrEmpty(key))
                    continue;
                if (string.IsNullOrWhiteSpace(target[key]) && !string.IsNullOrWhiteSpace(source[key]))
                    target[key] = source[key];
            }
        }

        /// <summary>
        /// Builds a viking://open URL for same-instance activation (no one-use code).
        /// </summary>
        public static string BuildActivationUrl(VikingDeepLink link)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(link.VolumeUrl))
                parts.Add("volume=" + Uri.EscapeDataString(link.VolumeUrl));
            if (!string.IsNullOrWhiteSpace(link.VolumeName))
                parts.Add("volumeName=" + Uri.EscapeDataString(link.VolumeName));

            string? location = link.Place?["Location"];
            if (!string.IsNullOrWhiteSpace(location))
                parts.Add("location=" + Uri.EscapeDataString(location));
            else
            {
                CopyPlaceQuery(parts, link.Place, "X", "x");
                CopyPlaceQuery(parts, link.Place, "Y", "y");
                CopyPlaceQuery(parts, link.Place, "Z", "z");
                CopyPlaceQuery(parts, link.Place, "DS", "ds");
            }

            return parts.Count == 0 ? "viking://open" : "viking://open?" + string.Join("&", parts);
        }

        /// <summary>
        /// True when the incoming link targets the volume already open (URL and/or Identity name).
        /// </summary>
        public static bool VolumeTargetsMatch(string? linkVolumeUrl, string? linkVolumeName, string? openVolumeUrl, string? openVolumeName)
        {
            if (!string.IsNullOrWhiteSpace(linkVolumeUrl) && !string.IsNullOrWhiteSpace(openVolumeUrl)
                && VolumeUrlsEqual(linkVolumeUrl, openVolumeUrl))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(linkVolumeName) && !string.IsNullOrWhiteSpace(openVolumeName)
                && string.Equals(linkVolumeName.Trim(), openVolumeName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static bool VolumeUrlsEqual(string? a, string? b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;
            return string.Equals(NormalizeVolumeUrl(a), NormalizeVolumeUrl(b), StringComparison.Ordinal);
        }

        public static string NormalizeVolumeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            string normalized = url.Trim();
            try
            {
                normalized = Common.Util.AppendDefaultVolumeFilenameIfMissing(normalized) ?? normalized;
            }
            catch
            {
                // Keep trimmed URL if append fails (malformed URI).
            }

            return normalized.TrimEnd('/').ToLowerInvariant();
        }

        public static bool LooksLikeVolumeUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && uri != null
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        public static bool HasPlace(NameValueCollection? place)
        {
            if (place is null)
                return false;
            return !string.IsNullOrWhiteSpace(place["Location"])
                || (!string.IsNullOrWhiteSpace(place["X"])
                    && !string.IsNullOrWhiteSpace(place["Y"])
                    && !string.IsNullOrWhiteSpace(place["Z"]));
        }

        private static void CopyQueryKey(Dictionary<string, string> query, NameValueCollection args, string queryKey, string startupKey)
        {
            if (query.TryGetValue(queryKey, out string? value) && !string.IsNullOrWhiteSpace(value))
                args[startupKey] = value.Trim();
        }

        private static void CopyPlaceQuery(List<string> parts, NameValueCollection? place, string placeKey, string queryKey)
        {
            string? value = place?[placeKey];
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add(queryKey + "=" + Uri.EscapeDataString(value));
        }
    }
}
