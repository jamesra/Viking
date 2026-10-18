using System;
using System.Security.Claims;

namespace Viking.Identity.Server
{
    /// <summary>
    /// Reads the OAuth client id from an introspected or JWT principal.
    /// Duende introspection uses <c>client_id</c>; some JWTs use <c>azp</c>.
    /// </summary>
    public static class OAuthTokenClient
    {
        /// <summary>Client id from <c>client_id</c>, <c>clientId</c>, or <c>azp</c>. Null when none are present.</summary>
        public static string GetClientId(ClaimsPrincipal user)
        {
            if (user == null)
                return null;

            return user.FindFirst("client_id")?.Value
                ?? user.FindFirst("clientId")?.Value
                ?? user.FindFirst("azp")?.Value;
        }

        /// <summary>True when the token was issued to the sbfsem-tools confidential client.</summary>
        public static bool IsSbfsemTools(ClaimsPrincipal user) =>
            string.Equals(GetClientId(user), VikingOAuthClients.SbfsemTools, StringComparison.Ordinal);

        /// <summary>
        /// True when the token was issued to a confidential web client that may open the desktop client for its user:
        /// sbfsem-tools and the SAM2 annotation galleries. A generic Viking.Annotation token is not enough.
        /// </summary>
        public static bool CanMintLaunchCodes(ClaimsPrincipal user)
        {
            var clientId = GetClientId(user);
            return string.Equals(clientId, VikingOAuthClients.SbfsemTools, StringComparison.Ordinal)
                || string.Equals(clientId, VikingOAuthClients.GalleryTem, StringComparison.Ordinal)
                || string.Equals(clientId, VikingOAuthClients.GallerySem, StringComparison.Ordinal);
        }
    }
}
