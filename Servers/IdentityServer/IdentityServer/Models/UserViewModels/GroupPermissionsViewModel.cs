using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer.Models.UserViewModels
{
    public class CreateGrantedResourcePermissionViewModel
    {
        public Resource Resource { get; set; }

        /// <summary>
        /// Permissions the group the user can select
        /// </summary>
        public IList<NamedItemSelectedViewModel<string>> Permissions { get; set; }

        public IList<NamedItemSelectedViewModel<string>> Users { get; set; }

        public IList<NamedItemSelectedViewModel<long>> Groups { get; set; }
    }

    public class GrantedResourcePermissionsViewModel
    {
        public Resource Group;

        /// <summary>
        /// Permissions the group
        /// </summary>
        public IList<ResourceTypePermission> AvailablePermissions;

        public ICollection<GrantedGroupPermission> HasPermissions { get; } = new List<GrantedGroupPermission>();

        public ICollection<GrantedGroupPermission> GroupsWithPermissions { get; } = new List<GrantedGroupPermission>();

        public ICollection<GrantedUserPermission> UsersWithPermissions { get; } = new List<GrantedUserPermission>();
    }

    public class UsersGrantedResourcePermissionsViewModel
    {
        public Resource Group;

        /// <summary>
        /// Permissions the group
        /// </summary>
        public IList<ResourceTypePermission> AvailablePermissions;

        public ICollection<GrantedUserPermission> HasPermissions { get; } = new List<GrantedUserPermission>();
          
        public ICollection<GrantedUserPermission> UsersWithPermissions { get; } = new List<GrantedUserPermission>();
    }

    public class GroupsGrantedResourcePermissionsViewModel
    {
        public Resource Group;

        /// <summary>
        /// Permissions the group
        /// </summary>
        public IList<ResourceTypePermission> AvailablePermissions;

        public ICollection<GrantedGroupPermission> HasPermissions { get; } = new List<GrantedGroupPermission>();

        public ICollection<GrantedGroupPermission> GroupsWithPermissions { get; } = new List<GrantedGroupPermission>();
         
    }
}
