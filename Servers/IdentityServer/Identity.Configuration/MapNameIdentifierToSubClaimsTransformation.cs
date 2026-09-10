using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

namespace Viking.Identity.Server
{
    /// <summary>
    /// Maps ASP.NET Identity cookie claims to the set Duende IdentityServer requires on the
    /// authorize endpoint (sub, idp, auth_time). Login on WebManagement (:4001) only emits
    /// NameIdentifier; without these, authorize on Standalone (:5001) throws.
    /// Also registered as <see cref="IClaimsTransformation"/>; cookie OnValidatePrincipal is the
    /// path that actually runs for Duende's user-session authenticate.
    /// </summary>
    public sealed class MapNameIdentifierToSubClaimsTransformation : IClaimsTransformation
    {
        public const string SubjectClaimType = "sub";
        public const string IdentityProviderClaimType = "idp";
        public const string AuthenticationTimeClaimType = "auth_time";
        public const string LocalIdentityProvider = "local";

        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            {
                return Task.FromResult(principal);
            }

            var mapped = new ClaimsIdentity(identity);
            var changed = false;

            if (!mapped.HasClaim(c => c.Type == SubjectClaimType))
            {
                var nameId = mapped.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(nameId))
                {
                    mapped.AddClaim(new Claim(SubjectClaimType, nameId));
                    changed = true;
                }
            }

            if (!mapped.HasClaim(c => c.Type == IdentityProviderClaimType))
            {
                mapped.AddClaim(new Claim(IdentityProviderClaimType, LocalIdentityProvider));
                changed = true;
            }

            if (!mapped.HasClaim(c => c.Type == AuthenticationTimeClaimType))
            {
                mapped.AddClaim(new Claim(
                    AuthenticationTimeClaimType,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64));
                changed = true;
            }

            return Task.FromResult(changed ? new ClaimsPrincipal(mapped) : principal);
        }
    }
}
