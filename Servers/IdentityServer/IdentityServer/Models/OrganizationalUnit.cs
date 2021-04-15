using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks; 
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models
{
    /// <summary>
    /// A collection of resources
    /// </summary>
    public class OrganizationalUnit : Resource
    {
        [InverseProperty(nameof(Resource.Parent))]
        [Display(Name = "Owned resources", Description = "Resources contained within this organization unit")]
        public virtual List<Resource> Children { get; } = new List<Resource>();
    }
}
