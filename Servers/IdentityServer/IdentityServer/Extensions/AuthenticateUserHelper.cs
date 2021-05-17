using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IdentityServer.Authorization;

namespace IdentityServer.Extensions
{
    /// <summary>
    /// Makes calls to IAuthorizationService using the ClaimsPrincipal from the current context
    /// </summary>
    public class AuthorizationHelper
    {
        private ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;
        private readonly ClaimsPrincipal _principal;

        public AuthorizationHelper(ApplicationDbContext dbContext, ClaimsPrincipal principal, IAuthorizationService authorization)
        {
            _context = dbContext;
            _authorization = authorization;
            _principal = principal;
        }

        public bool IsOrgUnitAdmin(long? Id)
        {
            return IsOrgUnitAdminAsync(Id, _principal).Result;
        }

        public async Task<bool> IsOrgUnitAdminAsync(long? Id, ClaimsPrincipal user=null)
        {
            OrganizationalUnit orgUnit = Id.HasValue ? _context.OrgUnit.FirstOrDefault(o => o.Id == Id) : null;
            return await IsOrgUnitAdminAsync(orgUnit, user);
        }

        public async Task<bool> IsOrgUnitAdminAsync(OrganizationalUnit orgUnit, ClaimsPrincipal user = null)
        {
            if (user == null)
                user = _principal;
              
            var authResult = await _authorization.AuthorizeAsync(user, orgUnit, IdentityServer.Authorization.Operations.OrgUnitAdmin);
            return authResult.Succeeded;
        }

        public async Task<bool> IsParentOrgUnitAdminAsync(Resource model)
        {
            return await _authorization.IsParentOrgUnitAdminAsync(_principal, model);
        }

        public bool IsGroupAccessManagerAsync(long Id)
        {
            return IsGroupAccessManagerAsync(Id, _principal).Result;
        }

        public async Task<bool> IsGroupAccessManagerAsync(long Id, ClaimsPrincipal user = null)
        {
            if (user == null)
                user = _principal;

            Group group = _context.Group.FirstOrDefault(o => o.Id == Id);
            return await IsGroupAccessManagerAsync(group, user);
        }

        public async Task<bool> IsGroupAccessManagerAsync(Group group, ClaimsPrincipal user = null)
        {
            if (user == null)
                user = _principal;

            var authResult = await _authorization.AuthorizeAsync(user, group, IdentityServer.Authorization.Operations.GroupAccessManager);
            return authResult.Succeeded;
        }
    }
}
