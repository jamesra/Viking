using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Viking.Identity.Models
{
    public abstract class GrantedPermissionBase
    {
        /// <summary>
        /// The resource to which the permission applies
        /// </summary>
        [Required]
        [Key, ForeignKey(nameof(Models.Resource))]
        [Display(Name = "Resource", Description = "Group permission is granted for")]
        public long ResourceId { get; set; }

        /// <summary>
        /// The resource to which the permission applies
        /// </summary>
        [ForeignKey(nameof(ResourceId))]
        //[InverseProperty("GroupsWithPermissions")]
        [Display(Name = "Resource", Description = "Resource that permission can be granted for")]
        public virtual Resource Resource { get; set; }
         
        /// <summary>
        /// The type of permission
        /// </summary>
        [Required]
        [Key]
        [Column(Order = 2)]
        [Display(Name = "Permission Granted", Description = "Permission type")]
        public string PermissionId { get; set; }

        //[Display(Name = "Permission", Description = "Permission type")]
        //public virtual ResourceTypePermission Permission { get; set; }
         
        //public virtual ResourceTypePermission Permission { get; set; }

        /// <summary>
        /// Discriminator used by EF Core to determine which derived type is linked in the database.
        /// </summary>
        public string GranteeType { get; protected set; }
    }

    /// <summary>
    /// Table of permissions that have been granted to a specific user/group
    /// </summary> 
    public class GrantedUserPermission : GrantedPermissionBase
    {
        /// <summary>
        /// User being granted permission
        /// </summary>
        [Required]
        [Key, ForeignKey(nameof(ApplicationUser))]
        [Display(Name = "User", Description = "User granted permission")]
        public string UserId { get; set; }

        /// <summary>
        /// User being granted permission
        /// </summary>
        [ForeignKey(nameof(UserId))]
        //[InverseProperty("HasPermissions")]
        public virtual ApplicationUser PermittedUser { get; set; }
    }
}
