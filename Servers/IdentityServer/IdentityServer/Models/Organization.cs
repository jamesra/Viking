using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models
{
    /// <summary>
    /// Records the lab or organization that the user belongs to.
    /// </summary>
    public class Organization
    {
        [Key]
        [Display(Name = "ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string Name { get; set; }

        public ICollection<OrganizationAssignment> OrganizationAssignments { get; } = new List<OrganizationAssignment>();
        [NotMapped]
        public virtual IEnumerable<ApplicationUser> Users => OrganizationAssignments.Select(oa => oa.User); 
    }
}
