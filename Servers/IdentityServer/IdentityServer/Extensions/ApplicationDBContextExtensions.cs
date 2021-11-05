using IdentityServer.Data;
using IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public static IQueryable<ApplicationUser> GetGroupAccessManagers(this ApplicationDbContext context, long ResourceId)
        {
            return context.GetPermittedUsers(ResourceId, Config.GroupAccessManagerPermission);
        }

        public static Task<bool> IsOrgAdministrator(this ApplicationDbContext context, long GroupId, string UserId)
        {
            return context.IsUserPermitted(GroupId, UserId, Config.OrgUnitAdminPermission);
        }

        public static Task<bool> IsGroupAccessManager(this ApplicationDbContext context, long GroupId, string UserId)
        {
            return context.IsUserPermitted(GroupId, UserId, Config.GroupAccessManagerPermission);
        }

        public static async Task<bool> IsUserPermitted(this ApplicationDbContext context, long ResourceId, string UserId, string PermissionId)
        {
            var permitted_users = from user in context.Users
                                  join permit in context.GrantedUserPermissions on user.Id equals permit.UserId
                                  where permit.PermissionId == PermissionId && permit.ResourceId == ResourceId && permit.UserId == UserId
                                  select user;

            if (permitted_users.Any())
                return true;

            var group_memberships = await context.RecursiveMemberOfGroups(UserId);

            var permitted_groups = from g in group_memberships
                                    join ggp in context.GrantedGroupPermissions on g.Id equals ggp.GroupId
                                    where ggp.PermissionId == PermissionId && ggp.ResourceId == ResourceId
                                    select ggp.GroupId;

            var intersection = permitted_groups.Intersect(group_memberships.Select(g => g.Id));

            return intersection.Any();
        }

        /// <summary>
        /// Returns all PermissionIds the user has for the specified resource
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ResourceId"></param>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<string>> UserResourcePermissions(this ApplicationDbContext context, long ResourceId, string UserId)
        {
            var user_permissions = from gup in context.GrantedUserPermissions
                                   where gup.ResourceId == ResourceId && gup.UserId == UserId
                                   select gup.PermissionId;

            var group_memberships = (await context.RecursiveMemberOfGroups(UserId)).Select(g => g.Id);

            var resource_group_permissions = (from ggp in context.GrantedGroupPermissions
                                             where ggp.ResourceId == ResourceId
                                             select ggp);

            var group_permissions = resource_group_permissions.Where(rgp => rgp.ResourceId == ResourceId && group_memberships.Contains(rgp.GroupId)).Select(rgp => rgp.PermissionId);

            var result = new SortedSet<string>(user_permissions);
            result.UnionWith(group_permissions);

            return result;
        }

        public static IQueryable<ApplicationUser> GetPermittedUsers(this ApplicationDbContext context, long ResourceId, string PermissionId)
        {
            var permitted_users = from user in context.Users
                                  join permit in context.GrantedUserPermissions on user.Id equals permit.UserId
                                  where permit.PermissionId == PermissionId && permit.ResourceId == ResourceId
                                  select user;

            var permitted_groups = from ggp in context.GrantedGroupPermissions
                                   where ggp.PermissionId == PermissionId && ggp.ResourceId == ResourceId
                                   select ggp.GroupId;

            var recursive_permitted_groups = context.RecursiveMemberOfGroups(permitted_groups, true).Result;

            var recursive_permitted_group_Ids = recursive_permitted_groups.Select(g => g.Id);

            //Return all members of the groups

            var recursive_permitted_users = from u_to_g in context.UserToGroupAssignments
                                            join g in context.Group on u_to_g.GroupId equals g.Id
                                            join u in context.Users on u_to_g.UserId equals u.Id
                                            where recursive_permitted_group_Ids.Contains(g.Id)
                                            select u;

            /*
            var permitted_group_users = from ggp in context.GrantedGroupPermissions
                                        join usersInGroup in context.UserToGroupAssignments on ggp.GroupId equals usersInGroup.GroupId
                                        join user in context.ApplicationUser on usersInGroup.UserId equals user.Id
                                        where ggp.PermissionId == PermissionId && ggp.ResourceId == ResourceId
                                        select user;
            */


            return permitted_users.Union(recursive_permitted_users).Distinct();
        }
         
        public static IEnumerable<ApplicationUser> GetGroupAccessManagers(this ApplicationDbContext context, IEnumerable<long> ResourceIds, string PermissionId)
        {
            return context.GetPermittedUsers(ResourceIds, Config.GroupAccessManagerPermission);
        }

        /// <summary>
        /// Returns the set of users that have the permission in every single one of the passed resources
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ResourceIds"></param>
        /// <param name="PermissionId"></param>
        /// <returns></returns>
        public static IEnumerable<ApplicationUser> GetPermittedUsers(this ApplicationDbContext context, IEnumerable<long> ResourceIds, string PermissionId)
        {
            var permissionsByResource = ResourceIds.Select(resId => context.GetPermittedUsers(resId, PermissionId)).ToList();

            if (permissionsByResource.Any() == false)
                return new List<ApplicationUser>();

            var intersection = permissionsByResource.First();
            permissionsByResource.RemoveAt(0);

            while(permissionsByResource.Any())
            {
                intersection = intersection.Intersect(permissionsByResource[0]);
                permissionsByResource.RemoveAt(0);
            }

            return intersection;
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
            Dictionary<long, List<ApplicationUser>> result = new Dictionary<long, List<ApplicationUser>>();
           foreach(var org in context.OrgUnit)
           {
                result[org.Id] = context.GetPermittedUsers(org.Id, PermissionId).ToList();
           }

            return result;
           /*
           Dictionary<long, List<ApplicationUser>> OrgAdminMap = new Dictionary<long, List<ApplicationUser>>();
               var AdminUsers = context.GetUsersInAdminRole();
            
               if(AdminUsers.Any() == false)
               {
                   return OrgAdminMap;
               }
            
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
           */
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

        /// <summary>
        /// Recursively returns all groups the passed GroupIds belong to
        /// </summary> 
        /// <param name="groupId">Group we are returning membership info for</param>
        /// <param name="includePassedGroup">True if the passed GroupId should appear in the result set, false if it should not.  Default true</param>
        /// <returns></returns>
        public static async Task<List<Group>> RecursiveMemberOfGroups(this ApplicationDbContext context, IEnumerable<long> groupIds, bool includePassedGroups = true)
        {
            var GroupAssignments = await context.GroupToGroupAssignments
                .Include(gga => gga.Container).ThenInclude(ggam => ggam.MemberOfGroups)
                .Where(gga => groupIds.Contains(gga.MemberGroupId))
                .ToListAsync();

            var Results = GroupAssignments.Select(dmg => dmg.Container).ToList();

            var recursiveResults = GroupAssignments.SelectMany(ga => ga.Container.MemberOfGroups.Select(mog => context.RecursiveMemberOfGroups(mog.ContainerGroupId, false))).ToList();

            await Task.WhenAll(recursiveResults);

            var rr = recursiveResults.SelectMany(rr => rr.Result).ToList();
            Results.AddRange(rr);

            if (includePassedGroups)
                Results.AddRange(context.Group.Where(g => groupIds.Contains(g.Id)));

            return Results;
        }

        /// <summary>
        /// Returns all groups the user belongs to, as well as all groups those are a part of recursivelyfs
        /// </summary>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Resource>> RecursiveChildrenOfOrg(this ApplicationDbContext context, long Id)
        {
            var ou = await context.OrgUnit
                .Include(o => o.Children)
                .FirstOrDefaultAsync(o => o.Id == Id);

            List<Resource> output = new List<Resource>();
            List<Resource> children = new List<Resource>();

            if (ou == null || ou.Children == null)
                return Array.Empty<Resource>();
            else
                output.AddRange(ou.Children);

            foreach(var child in ou.Children.Where(c => c.ResourceTypeId == nameof(OrganizationalUnit)))
            {
                output.AddRange(await context.RecursiveChildrenOfOrg(child.Id));
            }

            return output.Distinct();
        }

        /// <summary>
        /// Returns all groups the user belongs to, as well as all groups those are a part of recursivelyfs
        /// </summary>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<OrganizationalUnit>> RecursiveParentsOfOrg(this ApplicationDbContext context, long Id)
        {
            var ou = await context.OrgUnit
                .Include(o => o.Parent)
                .FirstOrDefaultAsync(o => o.Id == Id);

            List<OrganizationalUnit> parents = new List<OrganizationalUnit>();

            while (ou.ParentID.HasValue)
            {
                parents.Add(ou.Parent);

                var parent = await context.OrgUnit
                .Include(o => o.Parent)
                .FirstOrDefaultAsync(o => o.Id == ou.Id);

                ou = parent;
            }

            return parents;
        }
    }
}
