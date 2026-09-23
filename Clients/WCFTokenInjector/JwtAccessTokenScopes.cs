using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Viking.Common;

namespace Viking.Tokens
{
    /// <summary>
    /// Reads the unverified JWT payload enough to check OAuth scope claims.
    /// Used by the launch path to log when a token is missing volume Read before WCF fails.
    /// </summary>
    public static class JwtAccessTokenScopes
    {
        /// <summary>
        /// True when the JWT payload includes a volume Read scope for <paramref name="volumeName"/>,
        /// accepting both raw and hyphenated names (Identity launch vs ResourceScopeNames).
        /// </summary>
        public static bool ContainsVolumeRead(string accessToken, string volumeName)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(volumeName))
                return false;

            if (!TryGetScopes(accessToken, out IReadOnlyList<string> scopes))
                return false;

            string hyphenated = ResourceScopeNames.ToScope(volumeName, "Read");
            string raw = $"{volumeName.Trim()}.Read";
            foreach (string scope in scopes)
            {
                if (string.Equals(scope, hyphenated, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(scope, raw, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="accessToken"/> has a JWT header and payload.
        /// Reference-token handles are opaque and return false, so scope claims cannot be read.
        /// </summary>
        public static bool IsCompactJwt(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            string[] parts = accessToken.Split('.');
            return parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0;
        }

        /// <summary>
        /// Parses scope claims from a compact JWT without validating the signature.
        /// </summary>
        public static bool TryGetScopes(string accessToken, out IReadOnlyList<string> scopes)
        {
            scopes = Array.Empty<string>();
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            string[] parts = accessToken.Split('.');
            if (parts.Length < 2)
                return false;

            try
            {
                string json = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("scope", out JsonElement scopeElement))
                    return false;

                var list = new List<string>();
                if (scopeElement.ValueKind == JsonValueKind.String)
                {
                    string value = scopeElement.GetString() ?? "";
                    foreach (string piece in value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
                        list.Add(piece);
                }
                else if (scopeElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in scopeElement.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            string value = item.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                                list.Add(value);
                        }
                    }
                }

                scopes = list;
                return list.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        static byte[] Base64UrlDecode(string input)
        {
            string padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }

            return Convert.FromBase64String(padded);
        }
    }
}
