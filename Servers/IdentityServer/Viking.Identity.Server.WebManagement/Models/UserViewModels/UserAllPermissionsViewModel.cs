using System.Collections.Generic;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    /// <summary>One resource a user holds direct grants on, with the permissions granted (rows the admin can revoke).</summary>
    public class UserDirectGrantRow
    {
        public long ResourceId { get; set; }
        public string ResourceName { get; set; }
        public string ResourceType { get; set; }
        public List<string> Permissions { get; set; } = [];
    }

    /// <summary>A permission the user holds only through membership of a group; revoked on the group, not the user.</summary>
    public class UserInheritedGrantRow
    {
        public long ResourceId { get; set; }
        public string ResourceName { get; set; }
        public string ResourceType { get; set; }
        public string PermissionId { get; set; }
        public long GroupId { get; set; }
        public string GroupName { get; set; }
    }

    /// <summary>Everything one user is permitted to do, for the admin "All permissions" page on a user's profile.</summary>
    public class UserAllPermissionsViewModel
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public List<UserDirectGrantRow> Direct { get; set; } = [];
        public List<UserInheritedGrantRow> Inherited { get; set; } = [];
    }
}
