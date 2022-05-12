using System.Collections.Generic;
using System.Linq;
using Viking.Identity.Models;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Extensions
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
                resource.UpdateUserPermissions(userId, permission.Id, permission.Selected);
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
                resource.UpdateGroupPermissions(groupId, permission.Id, permission.Selected);
            }
        }

        
        #endregion
    }
}
