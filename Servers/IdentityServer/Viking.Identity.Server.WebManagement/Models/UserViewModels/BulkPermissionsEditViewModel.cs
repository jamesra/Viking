using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class BulkPermissionsEditViewModel
    {
        [Required]
        [Display(Name = "Selected Volume IDs")]
        public List<long> SelectedVolumeIds { get; set; } = new List<long>();

        [Display(Name = "Volumes")]
        public List<VolumeInfo> Volumes { get; set; } = new List<VolumeInfo>();

        [Display(Name = "Available Permissions")]
        public IList<string> AvailablePermissions { get; set; } = new List<string>();

        [Display(Name = "Users with Permissions")]
        public IList<UserResourcePermissionsViewModel> UserPermissions { get; set; } = new List<UserResourcePermissionsViewModel>();

        [Display(Name = "Groups with Permissions")]
        public IList<GroupResourcePermissionsViewModel> GroupPermissions { get; set; } = new List<GroupResourcePermissionsViewModel>();

        public class VolumeInfo
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string OrganizationName { get; set; }
        }
    }
}


