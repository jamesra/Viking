using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.Authorization
{
    public interface IAuthorizationHelper
    {
        bool IsOrgUnitAdmin(long? Id);
        Task<bool> IsOrgUnitAdminAsync(long? Id, ClaimsPrincipal user=null);
        Task<bool> IsOrgUnitAdminAsync(OrganizationalUnit orgUnit, ClaimsPrincipal user = null);
        Task<bool> IsParentOrgUnitAdminAsync(Resource model);
        bool IsGroupAccessManagerAsync(long Id);
        Task<bool> IsGroupAccessManagerAsync(long Id, ClaimsPrincipal user = null);
        Task<bool> IsGroupAccessManagerAsync(Group group, ClaimsPrincipal user = null);
    }

    /// <summary>
    /// Makes calls to IAuthorizationService using the ClaimsPrincipal from the current context
    /// </summary>
    public class AuthorizationHelper : IAuthorizationHelper
    {
        private readonly ApplicationDbContext _context;
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
            user ??= _principal;
              
            var authResult = await _authorization.AuthorizeAsync(user, orgUnit, Operations.OrgUnitAdmin);
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
            user ??= _principal;

            Group group = _context.Group.FirstOrDefault(o => o.Id == Id);
            return await IsGroupAccessManagerAsync(group, user);
        }

        public async Task<bool> IsGroupAccessManagerAsync(Group group, ClaimsPrincipal user = null)
        {
            user ??= _principal;

            var authResult = await _authorization.AuthorizeAsync(user, group, Operations.GroupAccessManager);
            return authResult.Succeeded;
        }
    }
}
