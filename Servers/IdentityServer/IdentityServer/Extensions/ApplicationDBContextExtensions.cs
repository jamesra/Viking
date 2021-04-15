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
        /// <summary>
        /// Users in an administrative role for the entire site
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ResourceId"></param>
        /// <param name="PermissionId"></param>
        /// <returns></returns>
        public static IQueryable<ApplicationUser> GetUsersInAdminRole(this ApplicationDbContext context)
        {
            var permitted_users = from user in context.Users
                                  join ur in context.UserRoles on user.Id equals ur.UserId
                                  where ur.RoleId == Config.AdminRoleId
                                  select user;

            return permitted_users;
        }

        public static IQueryable<ApplicationUser> GetGroupAdmins(this ApplicationDbContext context, long ResourceId, string PermissionId)
        {
            return context.GetPermittedUsers(ResourceId, Config.GroupAccessManagerPermission);
        }


        public static IQueryable<ApplicationUser> GetPermittedUsers(this ApplicationDbContext context, long ResourceId, string PermissionId)
        {
            var permitted_users = from user in context.Users
                                  join permit in context.GrantedUserPermissions on user.Id equals permit.UserId
                                  where permit.PermissionId == PermissionId && permit.ResourceId == ResourceId
                                  select user;

            var permitted_group_users = from pg in context.GrantedGroupPermissions
                                        join usersInGroup in context.UserToGroupAssignments on pg.GroupId equals usersInGroup.GroupId
                                        join user in context.ApplicationUser on usersInGroup.UserId equals user.Id
                                        where pg.PermissionId == PermissionId && pg.ResourceId == ResourceId
                                        select user;

            return permitted_users.Union(permitted_group_users);
        }


        public static IEnumerable<ApplicationUser> GetGroupsAdmins(this ApplicationDbContext context, IEnumerable<long> ResourceIds, string PermissionId)
        {
            return context.GetPermittedUsers(ResourceIds, Config.GroupAccessManagerPermission);
        }

        public static IEnumerable<ApplicationUser> GetPermittedUsers(this ApplicationDbContext context, IEnumerable<long> ResourceIds, string PermissionId)
        {
            return ResourceIds.SelectMany(resId => context.GetPermittedUsers(resId, PermissionId));
        }

        /*
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

   } */

   
       /// <summary>
       /// Return a dictionary of admin users for groups
       /// </summary>
       /// <param name="OrgIds"></param>
       /// <param name="context"></param>
       /// <returns></returns>
       public static Dictionary<long, List<ApplicationUser>> GetOrganizationAdminMap(this ApplicationDbContext context, string PermissionId)
       {
           Dictionary<long, List<ApplicationUser>> OrgAdminMap = new Dictionary<long, List<ApplicationUser>>();
            /*   var AdminUsers = context.GetUsersInAdminRole();

               if(AdminUsers.Any() == false)
               {
                   return OrgAdminMap;
               }
            */
            var permitted_users = from user in context.Users
                                  join permit in context.GrantedUserPermissions on user.Id equals permit.UserId
                                  where permit.PermissionId == PermissionId
                                  select new
                                  {
                                      User = user,
                                      Group = permit.Resource
                                  };

            var permitted_group_users = from pg in context.GrantedGroupPermissions
                                        join usersInGroup in context.UserToGroupAssignments on pg.GroupId equals usersInGroup.GroupId
                                        join user in context.ApplicationUser on usersInGroup.UserId equals user.Id
                                        where pg.PermissionId == PermissionId
                                        select new
                                        {
                                            User = user,
                                            Group = pg.Resource
                                        };

           var all_permitted_users = permitted_users.Union(permitted_group_users).GroupBy(g => g.Group.Id);

            foreach(var group in all_permitted_users)
            {
                OrgAdminMap.Add(group.Key, group.Select(g => g.User).ToList());
            }

            return OrgAdminMap;

            /*

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
            */
       }

        /// <summary>
        /// Returns all groups the user belongs to, as well as all groups those are a part of recursivelyfs
        /// </summary>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Group>> RecursiveMemberOfGroups(this ApplicationDbContext context, string userId)
        {
            var GroupAssignments = await context.UserToGroupAssignments
                .Include(uga => uga.Group).ThenInclude(uga => uga.MemberOfGroups)
                .Where(uga => uga.UserId == userId).ToListAsync();

            var Results = GroupAssignments.Select(dmg => dmg.Group).ToList();

            //Recursivly add any groups our direct groups are a member of

            var recursiveResults = GroupAssignments.SelectMany(ga => ga.Group.MemberOfGroups.Select(mog => context.RecursiveMemberOfGroups(mog.ContainerGroupId))).ToList();

            await Task.WhenAll(recursiveResults);

            var rr = recursiveResults.SelectMany(rr => rr.Result);
            Results.AddRange(rr);
            return Results.Distinct();
        }

        /// <summary>
        /// Recursively returns all groups the passed GroupId belongs to
        /// </summary> 
        /// <param name="groupId">Group we are returning membership info for</param>
        /// <param name="includePassedGroup">True if the passed GroupId should appear in the result set, false if it should not.  Default true</param>
        /// <returns></returns>
        public static async Task<List<Group>> RecursiveMemberOfGroups(this ApplicationDbContext context, long groupId, bool includePassedGroup = true)
        { 
            var GroupAssignments = await context.GroupToGroupAssignments
                .Include(gga => gga.Container).ThenInclude(ggam => ggam.MemberOfGroups)
                .Where(gga => gga.MemberGroupId == groupId)
                .ToListAsync();

            var Results = GroupAssignments.Select(dmg => dmg.Container).ToList();

            var recursiveResults = GroupAssignments.SelectMany(ga => ga.Container.MemberOfGroups.Select(mog => context.RecursiveMemberOfGroups(mog.ContainerGroupId, false))).ToList();
              
            await Task.WhenAll(recursiveResults);

            var rr = recursiveResults.SelectMany(rr => rr.Result).ToList();
            Results.AddRange(rr);

            if (includePassedGroup)
                Results.Insert(0, await context.Group.FindAsync(groupId));

            return Results;
        }
    }
}
