using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer.Models.UserViewModels
{
    public class GrantedGroupPermissionsViewModel
    {
        public Group Group;

        /// <summary>
        /// Permissions the group
        /// </summary>
        public IList<ResourceTypePermission> AvailablePermissions;

        public ICollection<GrantedGroupPermission> HasPermissions { get; } = new List<GrantedGroupPermission>();

        public ICollection<GrantedGroupPermission> GroupsWithPermissions { get; } = new List<GrantedGroupPermission>();

        public ICollection<GrantedUserPermission> UsersWithPermissions { get; } = new List<GrantedUserPermission>();
    }
}
