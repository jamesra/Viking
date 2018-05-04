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
        [Display(Name = "ID", Description = "Database generated ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(450)]
        [Display(Name = "Name", Description ="Name of the organization")]
        public string Name { get; set; }

        [Display(Name = "Short Unique Identifier", Description = "A unique short identifier for the organization")]
        [Required(AllowEmptyStrings = false)]
        [MaxLength(64)]
        public string ShortName { get; set; }

        public ICollection<OrganizationAssignment> OrganizationAssignments { get; } = new List<OrganizationAssignment>();
        [NotMapped]
        public virtual List<ApplicationUser> Users => OrganizationAssignments.Select(oa => oa.User).ToList();
    }
}
