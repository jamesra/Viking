using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using IdentityServer.Models.UserViewModels;
using IdentityServer.Models;
using IdentityServer.Data;

namespace IdentityServer.Extensions
{
    public static class ApplicationDBContextExtensions
    {
        public static IQueryable<ApplicationUser> GetAdminUsers(this ApplicationDbContext context)
        {
            var AdminRole = context.Roles.FirstOrDefault(r => r.Name == Config.AdminRoleName);
            if (AdminRole == null)
                return new List<ApplicationUser>().AsQueryable(); 

            var AdminUserIds = context.UserRoles.Where(ur => ur.RoleId == AdminRole.Id).Select(ur => ur.UserId);

            //For the startup case, if we only have one user in the database and nobody in the admin role then that single user is the admin
            if(AdminUserIds.Count() == 0 && context.Users.Count() == 1)
            {
                return context.Users;
            }

            return context.Users.Where(u => AdminUserIds.Contains(u.Id));
        }

        /// <summary>
        /// Return a dictionary of admin users for organizations
        /// </summary>
        /// <param name="OrgIds"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static Dictionary<long, List<ApplicationUser>> GetOrganizationAdminMap(this ApplicationDbContext context, List<long> OrgIds = null)
        {
            Dictionary<long, List<ApplicationUser>> OrgAdminMap = new Dictionary<long, List<ApplicationUser>>();
            var AdminUsers = context.GetAdminUsers();

            if(AdminUsers.Any() == false)
            {
                return OrgAdminMap;
            }

            IQueryable<GroupAssignment> AdminOrgAssignments;
            if (OrgIds != null)
            {
                AdminOrgAssignments = context.GroupAssignments.Include("User").Include("Group").Where(oa => OrgIds.Contains(oa.GroupId) && AdminUsers.Any(a => oa.UserId == a.Id));
            }
            else
            {
                AdminOrgAssignments = context.GroupAssignments.Include("User").Include("Group").Where(oa => AdminUsers.Any(a => oa.UserId == a.Id));
            }

            foreach (Group org in AdminOrgAssignments.Select(oa => oa.Group).Distinct())
            {
                var OrgAdmins = AdminOrgAssignments.Where(a => a.GroupId == org.Id).Select(oa => oa.User).ToList();
                OrgAdminMap.Add(org.Id, OrgAdmins);
            }

            return OrgAdminMap;
        }
    }
}
