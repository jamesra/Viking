using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;
using System.Collections.Generic;
using System.Linq;

namespace IdentityServer
{
    /// <summary>
    /// Helper methods to generate viewmodels related to permissions
    /// </summary>
    public interface IPermissionsViewModelHelper
    {
        /// <summary>
        /// Return a set of unselected permissions for each user
        /// </summary>
        /// <param name="rt"></param>
        /// <returns></returns>
        List<UserResourcePermissionsViewModel> UnpopulatedUserPermissions(ResourceType rt);


        /// <summary>
        /// Return a set of unselected permissions for each group
        /// </summary>
        /// <param name="rt"></param>
        /// <returns></returns>
        List<GroupResourcePermissionsViewModel> UnpopulatedGroupPermissions(ResourceType rt);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        ResourcePermissionsEditGridViewModel UnpopulatedPermisssionsEditGrid(ResourceType rt);

        List<UserResourcePermissionsViewModel> ResourcePermissionsByUser(Resource resource);
         
        List<GroupResourcePermissionsViewModel> ResourcePermissionsByGroup(Resource resource);

        ResourcePermissionsEditGridViewModel ResourcePermissionsEditGrid(Resource resource);
    }

    public class PermissionsViewModelHelper : IPermissionsViewModelHelper
    {
        private readonly ApplicationDbContext _context;

        public PermissionsViewModelHelper(ApplicationDbContext context)
        {
            _context = context;
        }

        public ResourcePermissionsEditGridViewModel ResourcePermissionsEditGrid(Resource resource)
        {
            ResourcePermissionsEditGridViewModel model = new ResourcePermissionsEditGridViewModel
            {
                AvailablePermissions = resource.AvailablePermissions
                    .Select(p => p.PermissionId)
                    .ToList(),
                UserPermissions = ResourcePermissionsByUser(resource),
                GroupPermissions = ResourcePermissionsByGroup(resource)
            };

            return model;
        }

        public List<UserResourcePermissionsViewModel> ResourcePermissionsByUser(Resource resource)
        {
            //var users = resource.UsersWithPermissions;.GroupBy(uwp => uwp.UserId);
            var userNames = _context.Users.ToDictionary(g => g.Id, g => g.UserName);

            var models = userNames.Select(u => new UserResourcePermissionsViewModel()
            {
                Permissions = resource.AvailablePermissions.Select(ap => new ItemSelectedViewModel<string>()
                {
                    Id = ap.PermissionId,
                    Selected = resource.UsersWithPermissions.Any(uwp => uwp.PermissionId == ap.PermissionId && uwp.UserId == u.Key && uwp.ResourceId == resource.Id)
                }).ToList(),
                GranteeId = u.Key,
                Name = userNames[u.Key]
            }).ToList();

            return models;
        }

        public List<GroupResourcePermissionsViewModel> ResourcePermissionsByGroup(Resource resource)
        {
            //var groups = resource.GroupsWithPermissions.GroupBy(gwp => gwp.GroupId);

            var groupNames = _context.Group.ToDictionary(g => g.Id, g => g.Name);

            var models = groupNames.Select(g => new GroupResourcePermissionsViewModel()
            {
                Permissions = resource.AvailablePermissions.Select(ap => new ItemSelectedViewModel<string>()
                {
                    Id = ap.PermissionId,
                    Selected = resource.GroupsWithPermissions.Any(uwp => uwp.PermissionId == ap.PermissionId &&
                                                                         uwp.GroupId == g.Key &&
                                                                         uwp.ResourceId == resource.Id)
                }).ToList(),
                GranteeId = g.Key,
                Name = groupNames[g.Key]
            })
                .Where(m => m.GranteeId != resource.Id) /* Don't allow us to grant access to ourselves? Makes sense at the moment...*/
                .ToList();

            return models;
        }

        public ResourcePermissionsEditGridViewModel UnpopulatedPermisssionsEditGrid(ResourceType rt)
        {
            ResourcePermissionsEditGridViewModel model = new ResourcePermissionsEditGridViewModel
            {
                AvailablePermissions = rt.Permissions.Select(p => p.PermissionId).ToList(),
                UserPermissions = UnpopulatedUserPermissions(rt),
                GroupPermissions = UnpopulatedGroupPermissions(rt)
            };

            return model;
        }

        public List<UserResourcePermissionsViewModel> UnpopulatedUserPermissions(ResourceType rt)
        {
            var userNames = _context.Users.ToDictionary(g => g.Id, g => g.UserName);

            var models = userNames.Select(u => new UserResourcePermissionsViewModel()
            {
                Permissions = rt.Permissions.Select(ap => new ItemSelectedViewModel<string>()
                {
                    Id = ap.PermissionId,
                    Selected = false
                }).ToList(),
                GranteeId = u.Key,
                Name = userNames[u.Key]
            }).ToList();

            return models;
        }

        public List<GroupResourcePermissionsViewModel> UnpopulatedGroupPermissions(ResourceType rt)
        {
            var groupNames = _context.Group.ToDictionary(g => g.Id, g => g.Name);

            var models = groupNames.Select(g => new GroupResourcePermissionsViewModel()
            {
                Permissions = rt.Permissions.Select(ap => new ItemSelectedViewModel<string>()
                {
                    Id = ap.PermissionId,
                    Selected = false
                }).ToList(),
                GranteeId = g.Key,
                Name = groupNames[g.Key]
            }).ToList();

            return models;
        }
    }
}
