using System;
using System.Threading.Tasks;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Viking.Identity.Models;

namespace Viking.Identity.Server
{
    /// <summary>
    /// Extension grant validator for "viking_user_token".
    /// Used by the WebApi launch-exchange endpoint to obtain an API token for a user by user_id.
    /// Only the "api" client (client credentials) should be configured to use this grant.
    /// </summary>
    public class VikingUserTokenGrantValidator : IExtensionGrantValidator
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<VikingUserTokenGrantValidator> _logger;

        public const string VikingUserTokenGrantType = "viking_user_token";

        public VikingUserTokenGrantValidator(UserManager<ApplicationUser> userManager, ILogger<VikingUserTokenGrantValidator> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        public string GrantType => VikingUserTokenGrantType;

        public async Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            if (!string.Equals(context.Request.ClientId, "api", StringComparison.Ordinal))
            {
                _logger.LogWarning("Rejected viking_user_token from client {ClientId}", context.Request.ClientId);
                context.Result = new GrantValidationResult(
                    Duende.IdentityServer.Models.TokenRequestErrors.UnauthorizedClient,
                    "client is not permitted to use this grant");
                return;
            }

            var userId = context.Request.Raw["user_id"];
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new GrantValidationResult(
                    Duende.IdentityServer.Models.TokenRequestErrors.InvalidGrant,
                    "user_id is required");
                return;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                context.Result = new GrantValidationResult(
                    Duende.IdentityServer.Models.TokenRequestErrors.InvalidGrant,
                    "user not found");
                return;
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                context.Result = new GrantValidationResult(
                    Duende.IdentityServer.Models.TokenRequestErrors.InvalidGrant,
                    "user is locked out");
                return;
            }

            if (_userManager.Options.SignIn.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
            {
                context.Result = new GrantValidationResult(
                    Duende.IdentityServer.Models.TokenRequestErrors.InvalidGrant,
                    "email is not confirmed");
                return;
            }

            _logger.LogInformation("Issued viking_user_token for user {UserId} to client {ClientId}",
                user.Id, context.Request.ClientId);

            context.Result = new GrantValidationResult(
                user.Id,
                VikingUserTokenGrantType);
        }
    }
}
