using Viking.Identity.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Viking.Identity.Models;

namespace Viking.Identity.Data
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
                                  join role in context.Roles on ur.RoleId equals role.Id
                                  where role.Name == Special.Roles.Admin
                                  select user;

            return permitted_users;
        }

        public static IQueryable<ApplicationUser> GetGroupAccessManagers(this ApplicationDbContext context, long ResourceId)
        {
            return context.GetPermittedUsers(ResourceId, Special.Permissions.Group.AccessManager);
        }

        public static Task<bool> IsOrgAdministrator(this ApplicationDbContext context, long GroupId, string UserId)
        {
            return context.IsUserPermitted(GroupId, UserId, Special.Permissions.OrgUnit.Admin);
        }

        public static Task<bool> IsGroupAccessManager(this ApplicationDbContext context, long GroupId, string UserId)
        {
            return context.IsUserPermitted(GroupId, UserId, Special.Permissions.Group.AccessManager);
        }

        private static readonly string[] ApiFacingResourceTypeIds = Special.ResourceTypes.ApiFacing;

        /// <summary>
        /// Resolves a resource by numeric id or by name. When looking up by name, prefers
        /// API-facing types so Group/OrgUnit name collisions (e.g. "Yiu") do not win, and among those
        /// prefers AnnotationServer, then Volume, then SegmentationService.
        /// </summary>
        public static async Task<Resource> FindApiFacingResourceAsync(this ApplicationDbContext context, string resourceIdOrName)
        {
            if (long.TryParse(resourceIdOrName, out var resourceId))
            {
                return await context.Resource.FirstOrDefaultAsync(r =>
                    r.Id == resourceId && ApiFacingResourceTypeIds.Contains(r.ResourceTypeId));
            }

            var apiFacing = await context.Resource
                .Where(r => r.Name == resourceIdOrName && ApiFacingResourceTypeIds.Contains(r.ResourceTypeId))
                .ToListAsync();

            return apiFacing
                .OrderBy(r => Array.IndexOf(ApiFacingResourceTypeIds, r.ResourceTypeId))
                .ThenBy(r => r.Id)
                .FirstOrDefault()
                ?? await context.Resource
                    .Where(r => r.Name == resourceIdOrName)
                    .OrderBy(r => r.Id)
                    .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Annotate and Review on a volume or annotation server imply Read: anyone who can edit annotations
        /// must be able to see the images under them.
        /// </summary>
        public static string[] WithImpliedRead(IEnumerable<string> permissionIds)
        {
            var set = new HashSet<string>(permissionIds ?? Array.Empty<string>());
            if (set.Contains(Special.Permissions.AnnotationServer.Annotate) || set.Contains(Special.Permissions.AnnotationServer.Review))
                set.Add(Special.Permissions.AnnotationServer.Read);

            return set
                .OrderBy(p => Array.IndexOf(Special.Permissions.AnnotationServer.All, p) is var i && i >= 0 ? i : int.MaxValue)
                .ThenBy(p => p, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Effective permissions on every volume the user can reach: grants on the volume itself (legacy rows
        /// and image-only volumes) unioned with grants on every linked annotation server, with Read implied.
        /// Site administrators get every permission on every volume.
        /// </summary>
        public static async Task<Dictionary<long, string[]>> UserAnnotationContextPermissionsAsync(this ApplicationDbContext context, [NotNull] string userId)
        {
            var grants = await context.UserResourcePermissionsByType(userId,
                new[] { nameof(AnnotationContext), nameof(AnnotationServer) });
            return await MergeVolumePermissionsAsync(context, grants);
        }

        /// <summary>
        /// Same as <see cref="UserAnnotationContextPermissionsAsync"/> for callers without a session: Anonymous group grants only.
        /// </summary>
        public static async Task<Dictionary<long, string[]>> AnonymousVolumePermissionsAsync(this ApplicationDbContext context)
        {
            var grants = await context.UserResourcePermissionsByTypeForAnonymous(
                new[] { nameof(AnnotationContext), nameof(AnnotationServer) });
            return await MergeVolumePermissionsAsync(context, grants);
        }

        private static async Task<Dictionary<long, string[]>> MergeVolumePermissionsAsync(ApplicationDbContext context, Dictionary<long, string[]> grants)
        {
            var result = new Dictionary<long, string[]>();
            if (grants.Count == 0)
                return result;

            var volumes = await context.AnnotationContexts
                .Select(v => new { v.Id, v.AnnotationServerId })
                .ToListAsync();

            var linkedServers = await context.AnnotationContextServers
                .Select(l => new { l.AnnotationContextId, l.AnnotationServerId })
                .ToListAsync();
            var serversByVolume = linkedServers
                .GroupBy(l => l.AnnotationContextId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.AnnotationServerId).ToList());

            foreach (var volume in volumes)
            {
                grants.TryGetValue(volume.Id, out var onVolume);

                var serverIds = new List<long>();
                if (serversByVolume.TryGetValue(volume.Id, out var linked))
                    serverIds.AddRange(linked);
                else if (volume.AnnotationServerId.HasValue)
                    serverIds.Add(volume.AnnotationServerId.Value);

                var onServers = serverIds
                    .Distinct()
                    .SelectMany(id => grants.TryGetValue(id, out var perms) ? perms : Array.Empty<string>());

                if (onVolume == null && !serverIds.Any(id => grants.ContainsKey(id)))
                    continue;

                result[volume.Id] = WithImpliedRead((onVolume ?? Array.Empty<string>()).Concat(onServers));
            }

            return result;
        }

        /// <summary>
        /// Effective permissions on one resource from explicit grants (no site-admin shortcut, matching
        /// <see cref="UserResourcePermissions(ApplicationDbContext, string, long)"/>). A volume also gets
        /// grants from every linked annotation server; volumes and annotation servers get Read implied.
        /// </summary>
        public static async Task<string[]> UserEffectiveResourcePermissionsAsync(this ApplicationDbContext context, [NotNull] string userId, [NotNull] Resource resource)
        {
            var resourceIds = new List<long> { resource.Id };
            foreach (var serverId in await AnnotationServerIdsOfAsync(context, resource))
                resourceIds.Add(serverId);

            var permissions = await (await context.UserResourcePermissions(userId, resourceIds)).Distinct().ToListAsync();

            return resource.ResourceTypeId == nameof(AnnotationContext) || resource.ResourceTypeId == nameof(AnnotationServer)
                ? WithImpliedRead(permissions)
                : permissions.ToArray();
        }

        /// <summary>
        /// <see cref="IsUserPermitted"/> extended for volumes (annotation server grants count) and for
        /// implied Read on volumes and annotation servers. Site administrators are always permitted.
        /// </summary>
        public static async Task<bool> IsUserPermittedEffectiveAsync(this ApplicationDbContext context, [NotNull] Resource resource, string userId, string permissionId)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            if (await context.GetUsersInAdminRole().AnyAsync(u => u.Id == userId))
                return true;

            var permissions = await context.UserEffectiveResourcePermissionsAsync(userId, resource);
            return permissions.Contains(permissionId);
        }

        private static async Task<List<long>> AnnotationServerIdsOfAsync(ApplicationDbContext context, Resource resource)
        {
            if (resource.ResourceTypeId != nameof(AnnotationContext) && resource is not AnnotationContext)
                return new List<long>();

            long contextId = resource.Id;
            var linked = await context.AnnotationContextServers
                .Where(l => l.AnnotationContextId == contextId)
                .Select(l => l.AnnotationServerId)
                .ToListAsync();
            if (linked.Count > 0)
                return linked;

            long? defaultId = resource is AnnotationContext annotationContext
                ? annotationContext.AnnotationServerId
                : await context.AnnotationContexts.Where(v => v.Id == contextId).Select(v => v.AnnotationServerId).FirstOrDefaultAsync();

            return defaultId.HasValue ? new List<long> { defaultId.Value } : new List<long>();
        }

        public static async Task<bool> IsUserPermitted(this ApplicationDbContext context, long ResourceId, string UserId, string PermissionId)
        {
            if (await context.GetUsersInAdminRole().AnyAsync(u => u.Id == UserId))
                return true;

            var permitted_users = from user in context.Users
                join permit in context.GrantedUserPermissions on user.Id equals permit.UserId
                where permit.PermissionId == PermissionId && permit.ResourceId == ResourceId && permit.UserId == UserId
                select user;

            if (await permitted_users.AnyAsync())
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
        /// <param name="userId"></param>
        /// <returns></returns>
        public static async Task<Dictionary<long, string[]>> UserResourcePermissionsByType(this ApplicationDbContext context, [NotNull] string userId, [NotNull] string[] resourceTypeIds = null)
        {
            if (resourceTypeIds == null || resourceTypeIds.Length == 0)
            {
                return new Dictionary<long, string[]>();
            }

            // Site administrators have full access to all resources of the requested types.
            if (await context.GetUsersInAdminRole().AnyAsync(u => u.Id == userId))
            {
                var permissionsByType = await context.Permissions
                    .Where(p => resourceTypeIds.Contains(p.ResourceTypeId))
                    .GroupBy(p => p.ResourceTypeId)
                    .ToDictionaryAsync(g => g.Key, g => g.Select(p => p.PermissionId).ToArray());

                var resources = await context.Resource
                    .Where(r => resourceTypeIds.Contains(r.ResourceTypeId))
                    .Select(r => new { r.Id, r.ResourceTypeId })
                    .ToListAsync();

                return resources
                    .Where(r => permissionsByType.ContainsKey(r.ResourceTypeId))
                    .ToDictionary(
                        r => r.Id,
                        r => permissionsByType[r.ResourceTypeId]);
            }

            var user_permissions = from gup in context.GrantedUserPermissions.Include(nameof(GrantedGroupPermission.Resource))
                where gup.UserId == userId
                select new { gup.ResourceId, gup.Resource.Name, gup.PermissionId, gup.Resource.ResourceTypeId};
            
            var group_memberships = (await context.RecursiveMemberOfGroups(userId)).Select(g => g.Id).ToList();
             
            var group_permissions = from ggp in context.GrantedGroupPermissions.Include(nameof(GrantedGroupPermission.Resource))
                where group_memberships.Contains(ggp.GroupId)
                select new { ggp.ResourceId, ggp.Resource.Name, ggp.PermissionId, ggp.Resource.ResourceTypeId };

            var upl = await user_permissions.ToListAsync();
            var gpl = await group_permissions.ToListAsync();

            var permissions = upl.Union(gpl);
            permissions = permissions.Where(p => resourceTypeIds.Contains(p.ResourceTypeId)); 

            var result = permissions.GroupBy(p => p.ResourceId, p => p.PermissionId).ToDictionary(d => d.Key, d => d.Distinct().ToArray());

                //var result = new SortedSet<string>(user_permissions);
//            result.UnionWith(group_permissions);

            return result;
        }

        public static Task<IQueryable<string>> UserResourcePermissions(this ApplicationDbContext context,
            [NotNull] string UserId, [NotNull] long ResourceId)
        {
            return context.UserResourcePermissions(UserId, new long[] { ResourceId });
        }

        /// <summary>
        /// Returns all PermissionIds the user has for the specified resource
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ResourceIds"></param>
        /// <param name="UserId"></param>
        /// <returns></returns>
        public static async Task<IQueryable<string>> UserResourcePermissions(this ApplicationDbContext context, [NotNull] string UserId, [NotNull] ICollection<long> ResourceIds)
        {
            var resources = (from r in context.Resource where ResourceIds.Contains(r.Id) select r);

            var user_permissions = from gup in context.GrantedUserPermissions   
                                    join r in resources on gup.ResourceId equals r.Id
                                    where gup.UserId == UserId
                                   select new { gup.ResourceId, gup.Resource.Name, gup.PermissionId, gup.Resource.ResourceTypeId };
             
            var group_memberships = (await context.RecursiveMemberOfGroups(UserId)).Select(g => g.Id);

            var resource_group_permissions = (from ggp in context.GrantedGroupPermissions
                                                join r in resources on ggp.ResourceId equals r.Id
                                                select ggp);

            var group_permissions = resource_group_permissions.Where(rgp => group_memberships.Contains(rgp.GroupId)).Select(rgp => new { rgp.ResourceId, rgp.Resource.Name, rgp.PermissionId, rgp.Resource.ResourceTypeId});

            /*
            var upl = await user_permissions.ToListAsync();
            var gpl = await group_permissions.ToListAsync();

            var permissions = upl.Union(gpl);
            */

            var permissions = user_permissions.Union(group_permissions);
             
            return permissions.Select(p => p.PermissionId);
        }

        public static async Task<IQueryable<ApplicationUser>> GetPermittedUsersAsync(this ApplicationDbContext context, long ResourceId, string PermissionId)
        {
            var permitted_users = from user in context.Users
                                  join permit in context.GrantedUserPermissions on user.Id equals permit.UserId
                                  where permit.PermissionId == PermissionId && permit.ResourceId == ResourceId
                                  select user;

            var permitted_groups = from ggp in context.GrantedGroupPermissions
                                   where ggp.PermissionId == PermissionId && ggp.ResourceId == ResourceId
                                   select ggp.GroupId;

            var recursive_permitted_groups = await context.RecursiveMemberOfGroups(permitted_groups, true);

            var recursive_permitted_group_Ids = recursive_permitted_groups.Select(g => g.Id);

            var recursive_permitted_users = from u_to_g in context.UserToGroupAssignments
                                            join g in context.Group on u_to_g.GroupId equals g.Id
                                            join u in context.Users on u_to_g.UserId equals u.Id
                                            where recursive_permitted_group_Ids.Contains(g.Id)
                                            select u;

            return permitted_users.Union(recursive_permitted_users).Distinct();
        }

        public static IQueryable<ApplicationUser> GetPermittedUsers(this ApplicationDbContext context, long ResourceId, string PermissionId)
        {
            return context.GetPermittedUsersAsync(ResourceId, PermissionId).GetAwaiter().GetResult();
        }
         
        public static IQueryable<ApplicationUser> GetGroupAccessManagers(this ApplicationDbContext context, IEnumerable<long> ResourceIds, string PermissionId)
        {
            return context.GetPermittedUsers(ResourceIds, Special.Permissions.Group.AccessManager);
        }

        /// <summary>
        /// Returns the set of users that have the permission in every single one of the passed resources
        /// </summary>
        /// <param name="context"></param>
        /// <param name="ResourceIds"></param>
        /// <param name="PermissionId"></param>
        /// <returns></returns>
        public static IQueryable<ApplicationUser> GetPermittedUsers(this ApplicationDbContext context, IEnumerable<long> ResourceIds, string PermissionId)
        {
            var permissionsByResource = ResourceIds.Select(resId => context.GetPermittedUsers(resId, PermissionId)).ToList();

            if (permissionsByResource.Any() == false)
                return Array.Empty<ApplicationUser>().AsQueryable();

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
       var AdminRole = context.Roles.FirstOrDefault(r => r.Name == Special.Roles.Admin);
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
       public static async Task<Dictionary<long, List<ApplicationUser>>> GetOrganizationAdminMapAsync(this ApplicationDbContext context, string PermissionId)
       {
            Dictionary<long, List<ApplicationUser>> result = new Dictionary<long, List<ApplicationUser>>();
           foreach(var org in context.OrgUnit)
           {
                result[org.Id] = await (await context.GetPermittedUsersAsync(org.Id, PermissionId)).ToListAsync();
           }

            return result;
       }

       public static Dictionary<long, List<ApplicationUser>> GetOrganizationAdminMap(this ApplicationDbContext context, string PermissionId)
       {
           return context.GetOrganizationAdminMapAsync(PermissionId).GetAwaiter().GetResult();
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

            // Every user is considered a member of the Anonymous group (virtual membership)
            var anonymousGroup = await context.Group.FindAsync(Special.Groups.Anonymous.Id);
            if (anonymousGroup != null && !Results.Any(g => g.Id == Special.Groups.Anonymous.Id))
            {
                Results.Add(anonymousGroup);
            }

            return Results.Distinct();
        }

        /// <summary>
        /// Returns permissions for the Anonymous group only (for unauthenticated callers).
        /// Same dictionary shape as UserResourcePermissionsByType: resource Id -> permission ids.
        /// </summary>
        public static async Task<Dictionary<long, string[]>> UserResourcePermissionsByTypeForAnonymous(this ApplicationDbContext context, [NotNull] string[] resourceTypeIds)
        {
            if (resourceTypeIds == null || !resourceTypeIds.Any())
            {
                return new Dictionary<long, string[]>();
            }

            var group_permissions = await context.GrantedGroupPermissions
                .Include(ggp => ggp.Resource)
                .Where(ggp => ggp.GroupId == Special.Groups.Anonymous.Id && resourceTypeIds.Contains(ggp.Resource.ResourceTypeId))
                .Select(ggp => new { ggp.ResourceId, ggp.Resource.Name, ggp.PermissionId, ggp.Resource.ResourceTypeId })
                .ToListAsync();

            var result = group_permissions
                .GroupBy(p => p.ResourceId, p => p.PermissionId)
                .ToDictionary(d => d.Key, d => d.ToArray());

            return result;
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
            if (ou == null)
                return parents;

            while (ou.ParentID.HasValue)
            {
                var parent = await context.OrgUnit
                    .Include(o => o.Parent)
                    .FirstOrDefaultAsync(o => o.Id == ou.ParentID.Value);

                if (parent == null)
                    break;

                parents.Add(parent);
                ou = parent;
            }

            return parents;
        }

        /// <summary>
        /// True when another resource of the same type already uses <paramref name="name"/>.
        /// Used to keep Duende ApiResource names unique for Volume/SegmentationService.
        /// </summary>
        public static bool IsResourceNameTaken(this ApplicationDbContext context, string name, string resourceTypeId, long? excludeId = null)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(resourceTypeId))
            {
                return false;
            }

            var query = context.Resource.Where(r =>
                r.ResourceTypeId == resourceTypeId &&
                r.Name == name);

            if (excludeId.HasValue)
            {
                query = query.Where(r => r.Id != excludeId.Value);
            }

            return query.Any();
        }
    }
}
