using System;
using System.Diagnostics;
using System.Security.Claims;
using System.Threading.Tasks;
using Duende.IdentityServer.Validation;
using IdentityModel;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Extensions
{
    /// <summary>
    /// Checks volume/segmentation scopes on token requests against the user's grants.
    /// Used for ROPC and for viking_user_token (launch-exchange).
    /// </summary>
    public class UserScopeTokenRequestValidator : ICustomTokenRequestValidator
    {
        ApplicationDbContext _context;

        public UserScopeTokenRequestValidator(ApplicationDbContext context)
        {
            _context = context; 
        }

        public async Task ValidateAsync(CustomTokenRequestValidationContext context)
        {
            foreach (var s in context.Result.ValidatedRequest.ValidatedResources.Resources.ApiScopes)
            {
                Trace.WriteLine(s.Name);
                if (string.Equals(s.Name, "Viking.Annotation", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Name, "openid", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s.Name, "profile", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var parts = s.Name.Split('.');
                string ResourceName;
                string ScopeName;
                if (parts.Length != 2)
                {
                    continue;
                }

                ResourceName = parts[0];
                ScopeName = parts[1];

                // Prefer Volume/SegmentationService when names collide with Group/OrgUnit (e.g. "Yiu").
                var resource = await _context.FindApiFacingResourceAsync(ResourceName);
                if (resource == null || (resource.ResourceTypeId != nameof(Volume) && resource.ResourceTypeId != nameof(SegmentationService)))
                    continue;

                var user = await FindTokenUserAsync(context.Result.ValidatedRequest);
                if (user == null)
                {
                    context.Result.IsError = true;
                    context.Result.Error = "user not found";
                    return;
                }

                if(false == await _context.IsUserPermitted(resource.Id, user.Id, ScopeName))
                {
                    context.Result.IsError = true;
                    context.Result.Error = $"{user.UserName} does not have access to scope {s.Name}";
                    context.Result.ErrorDescription = "Most likely the user is lacking a permission on the resource that was requested";
                    return;
                }
            }
        }

        /// <summary>
        /// Resolves the token user for volume-scope checks. ROPC sets UserName;
        /// viking_user_token (launch-exchange) leaves UserName empty and sets Subject to the user id.
        /// </summary>
        private async Task<ApplicationUser> FindTokenUserAsync(ValidatedTokenRequest request)
        {
            if (!string.IsNullOrEmpty(request?.UserName))
            {
                var byName = await _context.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
                if (byName != null)
                    return byName;
            }

            var subjectId = request?.Subject?.FindFirst(JwtClaimTypes.Subject)?.Value
                ?? request?.Subject?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subjectId))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.Id == subjectId);
        }
    }
}
