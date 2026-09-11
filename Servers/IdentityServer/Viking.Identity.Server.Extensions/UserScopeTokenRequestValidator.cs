using System.Diagnostics;
using System.Threading.Tasks;
using Duende.IdentityServer.Validation;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement.Extensions
{
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
                var parts = s.Name.Split('.');
                if (parts.Length != 2)
                {
                    continue;
                }

                string ResourceName = parts[0];
                string ScopeName = parts[1];

                //The scope name should match a permissionId 
                var resource = await _context.Resource.FirstOrDefaultAsync(r => r.Name == ResourceName);
                if (resource == null)
                    continue;

                var user = await ResolveUserAsync(context);
                if (user == null)
                {
                    context.Result.IsError = true;
                    context.Result.Error = "user not found";
                    return;
                }

                if (false == await _context.IsUserPermitted(resource.Id, user.Id, ScopeName))
                {
                    context.Result.IsError = true;
                    context.Result.Error = $"{user.UserName} does not have access to scope {s.Name}";
                    context.Result.ErrorDescription = "Most likely the user is lacking a permission on the resource that was requested";
                    return;
                }
            }
        }

        /// <summary>
        /// ROPC sets UserName; viking_user_token extension grant only sets Subject (user id).
        /// </summary>
        private async Task<ApplicationUser> ResolveUserAsync(CustomTokenRequestValidationContext context)
        {
            var request = context.Result.ValidatedRequest;
            if (!string.IsNullOrEmpty(request.UserName))
            {
                return await _context.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName);
            }

            var subject = request.Subject?.FindFirst("sub")?.Value
                ?? request.Subject?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subject))
                return null;

            return await _context.Users.FirstOrDefaultAsync(u => u.Id == subject);
        }
    }
}
