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
    /// <summary>
    /// A table of permissions that a resource offers
    /// </summary> 
    public class ResourceTypePermission
    {
        /// <summary>
        /// If not null, the permission is specific to a group.  If null the permission is visible to all groups
        /// </summary>
        [Key]
        [ForeignKey(nameof(Models.ResourceType))]
        [Display(Name = "Resource Type", Description = "Type of resource permission can apply to")]
        public string ResourceTypeId { get; set; }

        [Display(Name = "Resource Type", Description = "Type of resource permission can apply to")]
        [ForeignKey(nameof(ResourceTypeId))]
        public virtual ResourceType ResourceType { get; set; }

        [Key]
        [Required]
        [Display(Name = "Permission", Description = "Permission type")]
        public string PermissionId { get; set; }

        [Display(Name ="Description", Description = "Description of permission")]
        [MaxLength(2048)]
        public string Description { get; set; }
    }
}
