using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer.Models;
using IdentityServer.Models.UserViewModels;

namespace IdentityServer.Extensions
{
    public static class UpdateResourcePermissionExtensions
    {
        #region Users

        public static void UpdateUsersPermissions(this Resource resource, IEnumerable<UserResourcePermissionsViewModel> models)
        {
            foreach (var model in models)
            {
                resource.UpdateUserPermissions(model);
            }
        }

        public static void UpdateUserPermissions(this Resource resource, UserResourcePermissionsViewModel model)
        {
            foreach(var permission in model.Permissions)
            {
                resource.UpdateUserPermissions(model.GranteeId, model.Permissions);
            }
        }

        /// <summary>
        /// Add or remove the user from the group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="org"></param>
        public static void UpdateUserPermissions(this Resource resource, string userId, IList<ItemSelectedViewModel<string>> permissions)
        { 
            foreach(var permission in permissions)
            {
                UpdateUserPermissions(resource, userId, permission.Id, permission.Selected);
            }
        }

        /// <summary>
        /// Add or remove the user from the group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="org"></param>
        public static void UpdateUserPermissions(this Resource resource, string userId, string permissionId, bool HasPermission)
        {
            var ExistingMapping = resource.UsersWithPermissions.FirstOrDefault(o => o.ResourceId == resource.Id &&
                                                                                    o.PermissionId == permissionId &&
                                                                                    o.UserId == userId);

            if (HasPermission)
            {
                if (ExistingMapping == null)
                {
                    //Create the mapping
                    GrantedUserPermission row = new GrantedUserPermission() { ResourceId = resource.Id, 
                                                                             PermissionId = permissionId,
                                                                             UserId = userId};
                    resource.UsersWithPermissions.Add(row);
                }
            }
            else
            {
                if (ExistingMapping != null)
                {
                    //Remove the mapping
                    resource.UsersWithPermissions.Remove(ExistingMapping);
                }
            }
        }
        #endregion

        #region Groups

        public static void UpdateGroupsPermissions(this Resource resource, IEnumerable<GroupResourcePermissionsViewModel> models)
        {
            foreach (var model in models)
            {
                resource.UpdateGroupPermissions(model);
            }
        }

        public static void UpdateGroupPermissions(this Resource resource, GroupResourcePermissionsViewModel model)
        {
            foreach (var permission in model.Permissions)
            {
                resource.UpdateGroupPermissions(model.GranteeId, model.Permissions);
            }
        }

        /// <summary>
        /// Add or remove the user from the group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="org"></param>
        public static void UpdateGroupPermissions(this Resource resource, long groupId, IList<ItemSelectedViewModel<string>> permissions)
        {
            foreach (var permission in permissions)
            {
                UpdateGroupPermissions(resource, groupId, permission.Id, permission.Selected);
            }
        }

        /// <summary>
        /// Add or remove the user from the group
        /// </summary>
        /// <param name="user"></param>
        /// <param name="org"></param>
        public static void UpdateGroupPermissions(this Resource resource, long groupId, string permissionId, bool HasPermission)
        {
            var ExistingMapping = resource.GroupsWithPermissions.FirstOrDefault(o => o.ResourceId == resource.Id &&
                                                                                     o.PermissionId == permissionId &&
                                                                                     o.GroupId == groupId);

            if (HasPermission)
            {
                if (ExistingMapping == null)
                {
                    //Create the mapping
                    GrantedGroupPermission row = new GrantedGroupPermission()
                    {
                        ResourceId = resource.Id,
                        PermissionId = permissionId,
                        GroupId = groupId
                    };
                    resource.GroupsWithPermissions.Add(row);
                }
            }
            else
            {
                if (ExistingMapping != null)
                {
                    //Remove the mapping
                    resource.GroupsWithPermissions.Remove(ExistingMapping);
                }
            }
        }
        #endregion
    }
}
