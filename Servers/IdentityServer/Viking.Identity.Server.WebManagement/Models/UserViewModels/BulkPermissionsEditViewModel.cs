using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class BulkPermissionsEditViewModel
    {
        [Required]
        [Display(Name = "Selected Resource IDs")]
        public List<long> SelectedResourceIds { get; set; } = new List<long>();

        [Display(Name = "Resources")]
        public List<ResourceInfo> Resources { get; set; } = new List<ResourceInfo>();

        public string ResourcePluralDisplayName { get; set; } = "Resources";
        public string ResourceSingularDisplayName { get; set; } = "Resource";
        public string ResourceIconClass { get; set; } = "bi bi-folder";
        public string ReturnController { get; set; } = "Resources";

        [Display(Name = "Available Permissions")]
        public IList<string> AvailablePermissions { get; set; } = new List<string>();

        [Display(Name = "Users with Permissions")]
        public IList<UserResourcePermissionsViewModel> UserPermissions { get; set; } = new List<UserResourcePermissionsViewModel>();

        [Display(Name = "Groups with Permissions")]
        public IList<GroupResourcePermissionsViewModel> GroupPermissions { get; set; } = new List<GroupResourcePermissionsViewModel>();

        /// <summary>
        /// When true, server proceeds despite a large-removal confirmation requirement.
        /// </summary>
        public bool ConfirmLargeRemoval { get; set; }

        /// <summary>
        /// JSON snapshot of existing grants (resource × grantee × permission) for client-side removal %.
        /// </summary>
        public string InitialGrantsJson { get; set; }

        public class ResourceInfo
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string OrganizationName { get; set; }
        }
    }
}


