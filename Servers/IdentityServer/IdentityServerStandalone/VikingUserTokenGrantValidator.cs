using System.Threading.Tasks;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Identity;
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

        public const string VikingUserTokenGrantType = "viking_user_token";

        public VikingUserTokenGrantValidator(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public string GrantType => VikingUserTokenGrantType;

        public async Task ValidateAsync(ExtensionGrantValidationContext context)
        {
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

            context.Result = new GrantValidationResult(
                user.Id,
                VikingUserTokenGrantType);
        }
    }
}
