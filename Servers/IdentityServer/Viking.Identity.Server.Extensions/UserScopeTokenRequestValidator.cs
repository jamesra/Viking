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

                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == context.Result.ValidatedRequest.UserName);
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
    }
}
