using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Viking.Identity.Models;

namespace Viking.Identity.Models
{
    public static class ResourceExtensions
    {
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
    }
}
