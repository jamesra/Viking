using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models.UserViewModels
{
    public class EditGroupPermissionsViewModel
    {
        /// <summary>
        /// Resoruce we are editing our permissions for
        /// </summary> 
        [Required]
        [Display(Name = "Group")]
        public Group Group { get; set; }
         
        /// <summary>
        /// Grants the group has
        /// </summary>
        public IList<GrantedGroupPermission> GroupPermissions { get; set; }

        /// <summary>
        /// Grants the user has
        /// </summary>
        public IList<GrantedUserPermission> UserPermissions { get; set; }

        /// <summary>
        /// Roles available to the user
        /// </summary>
        public IList<string> AvailablePermissions { get; set; }
    }

    public abstract class EditGrantedPermissionsViewModelBase
    {
        /// <summary>
        /// Resoruce we are editing our permissions for
        /// </summary>
        [Required]
        public long ResourceId { get; set; }

        /// <summary>
        /// Roles available to the user
        /// </summary>
        public IList<string> AvailablePermissions { get; set; }
    }

    public class EditGrantedUserPermissionsViewModel : EditGrantedPermissionsViewModelBase
    {
        /// <summary>
        /// User being granted permission
        /// </summary>
        [Required]
        public string UserId { get; set; }

        /// <summary>
        /// Grants the group has
        /// </summary>
        public IList<GrantedUserPermission> Permissions { get; set; }
    }


    public class EditGrantedGroupPermissionsViewModel : EditGrantedPermissionsViewModelBase
    { 
        /// <summary>
        /// Id of the user
        /// </summary>
        [Required]
        [Display(Name = "Group")]
        public Group Group { get; set; }

        /// <summary>
        /// Grants the group has
        /// </summary>
        public IList<GrantedGroupPermission> Permissions { get; set; }

        
    }
}
