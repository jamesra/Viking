using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Models
{
    /// <summary>
    /// A generic resource we want to set permissions on
    /// </summary>
    public class Resource
    {
        public override string ToString()
        {
            return $"{Id}: {Name} {ResourceTypeId}";
        }

        [Key]
        [Display(Name = "ID", Description = "Database generated ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(128)]
        [Remote(action: "VerifyUniqueName", controller: "Resources", AdditionalFields ="Id")]
        [Display(Name = "Name", Description = "Name of the group")]
        public string Name { get; set; }

        [Display(Name = "Description", Description = "Information about the group")]
        [MaxLength(2048)]
        public string Description { get; set; }

        /// <summary>
        /// Resource ownership can be assigned to groups, , , 
        /// </summary>
        [ForeignKey(nameof(OrganizationalUnit))]
        [Display(Name = nameof(ParentID), Description = "Optional parent/owner of this resource ID")]
        public long? ParentID { get; set; }

        /// <summary>
        /// null if there is no parent
        /// </summary>
        [ForeignKey(nameof(ParentID))]
        [Display(Name = nameof(Parent), Description = "Optional parent/owner of this resource ID")]
        public virtual OrganizationalUnit Parent { get; set; }
         
        /// <summary>
        /// Resources can be assigned a type, this determines which permissions are available
        /// </summary>
        [ForeignKey(nameof(ResourceType))]
        [Display(Name = "Resource Type", Description = "Describes what type of resource this entity represents")]
        public virtual string ResourceTypeId { get; set; }

        [ForeignKey(nameof(ResourceTypeId))]
        [Display(Name = "Resource Type", Description = "Describes what type of resource this entity represents")]
        public virtual ResourceType ResourceType { get; set; }
         
        [Display(Name = "Groups with Permissions", Description = "Groups that have been assigned a group permission")]
        public virtual ICollection<GrantedGroupPermission> GroupsWithPermissions { get; } = new List<GrantedGroupPermission>();

        [Display(Name = "Users with Permissions", Description = "Users that have been assigned a group permission")]
        public virtual ICollection<GrantedUserPermission> UsersWithPermissions { get; } = new List<GrantedUserPermission>();

        /// <summary>
        /// The permissions this group can grant to others
        /// </summar
        [NotMapped]
        [Display(Name = "Permissions", Description = "Permissions that can be granted to this resource")]
        public virtual ICollection<ResourceTypePermission> AvailablePermissions => this.ResourceType == null ? new List<ResourceTypePermission>() : this.ResourceType.Permissions;

    }
}
