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
using IdentityServer.Extensions;

namespace IdentityServer.Authorization
{
    public static class Operations
    {
        public static ResourcePermissionRequirement GroupAccessManager = new ResourcePermissionRequirement(Config.GroupAccessManagerPermission);
        public static ResourcePermissionRequirement OrgUnitAdmin = new ResourcePermissionRequirement(Config.OrgUnitAdminPermission);
    }

    public static class AuthorizationServiceExtensions
    {
        public static async Task<bool> IsGroupAccessManager(this IAuthorizationService _authorizationService, System.Security.Claims.ClaimsPrincipal User, Group group)
        {
            if (User.IsInRole(Config.AdminRoleName))
                return true; 

            var result = await _authorizationService.AuthorizeAsync(User, group, IdentityServer.Authorization.Operations.GroupAccessManager);
            return result.Succeeded;
        }

        public static async Task<bool> IsOrgUnitAdmin(this IAuthorizationService _authorizationService, System.Security.Claims.ClaimsPrincipal User, OrganizationalUnit orgUnit)
        {
            if (User.IsInRole(Config.AdminRoleName))
                return true;

            var result = await _authorizationService.AuthorizeAsync(User, orgUnit, IdentityServer.Authorization.Operations.OrgUnitAdmin);
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

            if (context.User.IsInRole(Config.AdminRoleName))
            {
                context.Succeed(requirement);
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

            if (context.User.IsInRole(Config.AdminRoleName))
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
