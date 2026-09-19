using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using Viking.UI;

namespace WebAnnotation
{
    /// <summary>
    /// Volume JWT roles used for first-run Auto Polygonize defaults.
    /// Review and admin default on; annotate (and everyone else) default off.
    /// </summary>
    internal static class VolumeAccessRoles
    {
        /// <summary>
        /// True when the current session token (or legacy Admin access level) is review-class.
        /// Called by <see cref="Global.AnnotationSettings.AutoPolygonizeCircles"/> before the user has chosen.
        /// </summary>
        public static bool HasReviewAccess()
        {
            string? token = State.UserBearerToken?.AccessToken;
            string? volumeName = State.IdentityVolumeName ?? State.volume?.Name;
            if (TokenGrantsReviewAccess(token, volumeName))
                return true;

            return string.Equals(State.userAccessLevel, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Review or admin on the volume token. Login requests scopes as <c>{volume}.{permission}</c>.
        /// </summary>
        public static bool TokenGrantsReviewAccess(string? accessToken, string? volumeName)
        {
            foreach (string role in EnumerateTokenRoles(accessToken, volumeName))
            {
                if (role.Equals("review", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("admin", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Roles from JWT <c>scope</c> and <c>role</c> claims. Volume scopes keep the suffix after <c>{volume}.</c>.
        /// </summary>
        public static IEnumerable<string> EnumerateTokenRoles(string? accessToken, string? volumeName)
        {
            if (!TryReadJwtPayload(accessToken, out JObject? payload) || payload is null)
                yield break;

            foreach (string raw in ReadClaimValues(payload, "scope"))
            {
                foreach (string part in raw.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries))
                    yield return NormalizeVolumeRole(part, volumeName);
            }

            foreach (string role in ReadClaimValues(payload, "role"))
                yield return NormalizeVolumeRole(role, volumeName);
        }

        private static string NormalizeVolumeRole(string value, string? volumeName)
        {
            string role = value.Trim();
            if (string.IsNullOrEmpty(volumeName))
                return role;

            string prefixDot = volumeName + ".";
            if (role.StartsWith(prefixDot, StringComparison.OrdinalIgnoreCase))
                return role.Substring(prefixDot.Length);

            string prefixSlash = volumeName + "/";
            if (role.StartsWith(prefixSlash, StringComparison.OrdinalIgnoreCase))
                return role.Substring(prefixSlash.Length);

            return role;
        }

        private static IEnumerable<string> ReadClaimValues(JObject payload, string claimName)
        {
            JToken token = payload[claimName];
            if (token is null)
                yield break;

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken item in token)
                {
                    string text = item.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        yield return text;
                }

                yield break;
            }

            string value = token.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                yield return value;
        }

        private static bool TryReadJwtPayload(string? accessToken, out JObject? payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(accessToken))
                return false;

            string[] parts = accessToken.Split('.');
            if (parts.Length < 2)
                return false;

            try
            {
                string json = Encoding.UTF8.GetString(FromBase64Url(parts[1]));
                payload = JObject.Parse(json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static byte[] FromBase64Url(string value)
        {
            string padded = value.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return Convert.FromBase64String(padded);
        }
    }
}
