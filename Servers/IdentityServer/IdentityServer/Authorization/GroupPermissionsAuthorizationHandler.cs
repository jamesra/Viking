using Viking.Identity.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Authorization
{
    public static class Operations
    {
        public static ResourcePermissionRequirement GroupAccessManager = new ResourcePermissionRequirement(Special.Permissions.Group.AccessManager);
        public static ResourcePermissionRequirement OrgUnitAdmin = new ResourcePermissionRequirement(Special.Permissions.OrgUnit.Admin);
    }

    public static class AuthorizationServiceExtensions
    {
        public static async Task<bool> IsGroupAccessManagerAsync(this IAuthorizationService _authorizationService, System.Security.Claims.ClaimsPrincipal User, Group group)
        {
            if (User.IsInRole(Special.Roles.Admin))
                return true; 

            var result = await _authorizationService.AuthorizeAsync(User, group, Operations.GroupAccessManager);
            return result.Succeeded;
        }

        public static async Task<bool> IsOrgUnitAdminAsync(this IAuthorizationService _authorizationService, System.Security.Claims.ClaimsPrincipal User, OrganizationalUnit orgUnit)
        {
            if (User.IsInRole(Special.Roles.Admin))
                return true;

            var result = await _authorizationService.AuthorizeAsync(User, orgUnit, Operations.OrgUnitAdmin);
            return result.Succeeded;
        }

        /// <summary>
        /// The ability to manage a volume is controlled by the administrator of the org unit it belongs to, or the site manager if not
        /// </summary>
        /// <param name="_authorizationService"></param>
        /// <param name="User"></param>
        /// <param name="resource"></param>
        /// <returns></returns>
        public static async Task<bool> IsParentOrgUnitAdminAsync(this IAuthorizationService _authorizationService, System.Security.Claims.ClaimsPrincipal User, Resource resource)
        {
            if (User.IsInRole(Special.Roles.Admin))
                return true;

            if (resource.ParentID.HasValue == false)
                return false;

            var result = await _authorizationService.AuthorizeAsync(User, resource.Parent == null ? resource.ParentID.Value : resource.Parent, Operations.OrgUnitAdmin);
            return result.Succeeded;
        } 
    }
     

    public class ResourcePermissionRequirement : IAuthorizationRequirement
    {
        public readonly string Permission;
         
        public ResourcePermissionRequirement(string permission)
        {
            Permission = permission; 
        }

        public override string ToString()
        {
            return Permission;
        }

        public override bool Equals(object obj)
        {
            if(obj is ResourcePermissionRequirement other)
            {
                return other.Permission.Equals(this.Permission);
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Permission.GetHashCode();
        }
    }

    public class ResourcePermissionsAuthorizationHandler : AuthorizationHandler<ResourcePermissionRequirement, Resource>
    {
        private ApplicationDbContext DbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResourcePermissionsAuthorizationHandler(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            DbContext = dbContext;
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ResourcePermissionRequirement requirement, Resource resource_requested)
        { 
            if (context.User.Identity == null)
            {
                context.Fail();
                return;
            }

            if (context.User.Identity.IsAuthenticated == false)
            {
                context.Fail();
                return;
            }
            
            //If the user is a site admin they can do anything, note this also covers the case
            //where resource_requested is null because we asked about the parent org of an OrgUnit 
            //with no parent. i.e. Only admins can edit OrgUnits/Resources at the root of the heirarchy.
            if (context.User.IsInRole(Special.Roles.Admin))
            {
                context.Succeed(requirement);
                return;
            }

            //If there is no resource to check and we are not an admin the requirement is not met
            if (resource_requested == null)
            {
                context.Fail();
                return;
            }

            var UserId = _userManager.GetUserId(context.User);

            if (await DbContext.IsUserPermitted(resource_requested.Id, UserId, requirement.Permission))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
    }

    public class ResourceIdPermissionsAuthorizationHandler : AuthorizationHandler<ResourcePermissionRequirement, long>
    {
        private ApplicationDbContext DbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public ResourceIdPermissionsAuthorizationHandler(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            DbContext = dbContext;
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ResourcePermissionRequirement requirement, long resource_id)
        { 
            if (context.User.Identity == null)
            {
                context.Fail();
                return;
            }

            if (context.User.Identity.IsAuthenticated == false)
            {
                context.Fail();
                return;
            }

            if (context.User.IsInRole(Special.Roles.Admin))
            {
                context.Succeed(requirement);
                return;
            }

            var UserId = _userManager.GetUserId(context.User);

            if (await DbContext.IsUserPermitted(resource_id, UserId, requirement.Permission))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
    }
}
