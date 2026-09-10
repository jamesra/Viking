using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Viking.Identity.Server
{
    /// <summary>
    /// Registers the shared Data Protection key ring and Identity application cookie used by
    /// Standalone and WebManagement so OIDC authorize on :5001 accepts a login issued on :4001.
    /// </summary>
    public static class SharedIdentityDataProtection
    {
        /// <summary>
        /// PersistKeysToFileSystem disables default at-rest encryption. Docker and Production must
        /// wrap the key ring with the SSL cert. Existing unencrypted key XML remains readable;
        /// new keys are written encrypted.
        /// </summary>
        public static void AddSharedIdentityDataProtection(
            this IServiceCollection services,
            X509Certificate2 sslCert,
            bool allowUnencryptedKeys)
        {
            var isDockerEnvironment = string.Equals(
                Environment.GetEnvironmentVariable("HOSTING_ENVIRONMENT"),
                "Docker",
                StringComparison.OrdinalIgnoreCase);
            var dataProtectionKeysPath = isDockerEnvironment
                ? "/app/DataProtectionKeys"
                : @"./DataProtectionKeys/";

            var dataProtectionBuilder = services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
                .SetApplicationName(SharedIdentityAuthentication.DataProtectionApplicationName);

            if (sslCert != null)
            {
                try
                {
                    dataProtectionBuilder.ProtectKeysWithCertificate(sslCert);
                    Log.Information(
                        "Data Protection configured with certificate encryption. Subject: {Subject}, Thumbprint: {Thumbprint}",
                        sslCert.Subject, sslCert.Thumbprint);
                    return;
                }
                catch (Exception ex) when (allowUnencryptedKeys)
                {
                    Log.Warning(ex, "Failed to configure Data Protection with certificate, using file system protection only");
                    return;
                }
            }

            if (allowUnencryptedKeys)
            {
                Log.Warning("No certificate found for Data Protection, using file system protection only");
                return;
            }

            throw new InvalidOperationException(
                "Data Protection requires the SSL certificate to encrypt keys at rest outside local Development.");
        }

        /// <summary>
        /// Aligns the Identity application cookie name and path so browsers send the same ticket
        /// to both management (:4001) and issuer (:5001) on the same hostname.
        /// Also maps NameIdentifier to Duende's required "sub" claim on every cookie validation;
        /// WebManagement sign-in does not emit "sub", and authorize on :5001 fails without it.
        /// </summary>
        public static void ConfigureSharedApplicationCookie(this IServiceCollection services)
        {
            services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = SharedIdentityAuthentication.CookieName;
                options.Cookie.Path = SharedIdentityAuthentication.CookiePath;
                options.Events.OnValidatePrincipal = EnsureSubjectClaimAsync;
            });
        }

        private static Task EnsureSubjectClaimAsync(CookieValidatePrincipalContext context)
        {
            if (context.Principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            {
                return Task.CompletedTask;
            }

            var renewed = false;

            if (!identity.HasClaim(c => c.Type == MapNameIdentifierToSubClaimsTransformation.SubjectClaimType))
            {
                var nameId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(nameId))
                {
                    identity.AddClaim(new Claim(MapNameIdentifierToSubClaimsTransformation.SubjectClaimType, nameId));
                    renewed = true;
                }
            }

            // Duende authorize requires idp/auth_time; ASP.NET Identity cookies from :4001 omit them.
            if (!identity.HasClaim(c => c.Type == MapNameIdentifierToSubClaimsTransformation.IdentityProviderClaimType))
            {
                identity.AddClaim(new Claim(
                    MapNameIdentifierToSubClaimsTransformation.IdentityProviderClaimType,
                    MapNameIdentifierToSubClaimsTransformation.LocalIdentityProvider));
                renewed = true;
            }

            if (!identity.HasClaim(c => c.Type == MapNameIdentifierToSubClaimsTransformation.AuthenticationTimeClaimType))
            {
                var authTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                identity.AddClaim(new Claim(
                    MapNameIdentifierToSubClaimsTransformation.AuthenticationTimeClaimType,
                    authTime,
                    ClaimValueTypes.Integer64));
                renewed = true;
            }

            if (renewed)
            {
                context.ShouldRenew = true;
            }

            return Task.CompletedTask;
        }
    }
}
