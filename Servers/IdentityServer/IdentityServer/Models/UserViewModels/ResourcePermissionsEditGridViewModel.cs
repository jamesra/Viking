using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class ResourcePermissionsEditGridViewModel
    {
        [Display(Name = "Available Permissions")]
        
        public IList<string> AvailablePermissions { get; set; }

        [Display(Name = "Users with Permissions")]
        public IList<UserResourcePermissionsViewModel> UserPermissions { get; set; }

        [Display(Name = "Groups with permissions")]
        public IList<GroupResourcePermissionsViewModel> GroupPermissions { get; set; }
    }
}
