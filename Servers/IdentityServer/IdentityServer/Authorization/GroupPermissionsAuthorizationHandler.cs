using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using IdentityServer.Models;
using IdentityServer.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Authorization
{
    public static class Operations
    {
        public static GroupPermissionRequirement AccessManager = new GroupPermissionRequirement(Config.AdminRoleName);
    }

    public static class AuthorizationServiceExtensions
    {
        public static async Task<bool> IsGroupAccessManager(this IAuthorizationService _authorizationService, System.Security.Claims.ClaimsPrincipal User, Group group)
        {
            if (User.IsInRole(Config.AdminRoleName))
                return true; 

            var result = await _authorizationService.AuthorizeAsync(User, group, IdentityServer.Authorization.Operations.AccessManager);
            return result.Succeeded;
        }
    }

    public class GroupPermissionsAuthorizationHandler : AuthorizationHandler<GroupPermissionRequirement, Group>
    {
        private ApplicationDbContext DbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public GroupPermissionsAuthorizationHandler(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
        {
            DbContext = dbContext;
            _userManager = userManager;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, GroupPermissionRequirement requirement, Group group_requested)
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

            if (context.User.IsInRole(Config.AdminRoleName))
            {
                context.Succeed(requirement);
                return;
            }

            var UserId = _userManager.GetUserId(context.User);
            /*

            var userPermissionInGroup = (from ur in DbContext.Granted
                                   where ur.UserId == context.User.Identity.Name &&
                                         ur.GroupID == group_requested.Id &&
                                         ur.RoleId == requirement.Role
                                   select ur);

            if (userRoleInGroup.Any())
                context.Succeed(requirement);
            else
                context.Fail();
            */
        }
    }

    public class GroupPermissionRequirement : IAuthorizationRequirement
    {
        public readonly string Permission;

        public GroupPermissionRequirement(string permission)
        {
            Permission = permission; 
        }
    }
}
