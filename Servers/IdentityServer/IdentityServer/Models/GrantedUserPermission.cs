using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace IdentityServer.Models
{
    public abstract class GrantedPermissionBase
    {
        [Required]
        [Key, ForeignKey(nameof(Group))]
        [Display(Name = "Resource", Description = "Group permission is granted for")]
        public long ResourceId { get; set; }
         
        [ForeignKey(nameof(ResourceId))]
        //[InverseProperty("GroupsWithPermissions")]
        [Display(Name = "Resource", Description = "Resource that permission can be granted for")]
        public virtual Resource Resource { get; set; }
         
        [Required]
        [Key]
        [Column(Order = 2)]
        [Display(Name = "Permission", Description = "Permission type")]
        public string PermissionId { get; set; }

        public virtual ResourceTypePermission Permission { get; set; }
    }

    /// <summary>
    /// Table of permissions that have been granted to a specific user/group
    /// </summary> 
    public class GrantedUserPermission : GrantedPermissionBase
    {
        [Required]
        [Key, ForeignKey(nameof(ApplicationUser))]
        [Display(Name = "User", Description = "User granted permission")]
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        //[InverseProperty("HasPermissions")]
        public virtual ApplicationUser PermittedUser { get; set; }
    }
}
