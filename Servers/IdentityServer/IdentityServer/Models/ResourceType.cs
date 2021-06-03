using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models
{
    /// <summary>
    /// The permissions exposed by a resource are determined by its type
    /// </summary>
    public class ResourceType
    {
        [Key]
        [MaxLength(128)]
        [Display(Name = "Name", Description = "Name of the resource type")]
        public string Id { get; set; }

        [Display(Name = "Description", Description = "Information about the resource and its uses")]
        [MaxLength(4096)]
        public string Description { get; set; }

        [Display(Name = "Permissions", Description = "Permissions that can be set on the resource type")]
        public virtual List<ResourceTypePermission> Permissions { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
