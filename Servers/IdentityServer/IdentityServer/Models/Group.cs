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
    public class Group
    {
        [Key]
        [Display(Name = "ID", Description = "Database generated ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [ForeignKey("Group")]
        [Display(Name = "ParentID", Description = "Optional parent group ID")]
        public long? ParentID { get; set; }

        [ForeignKey("ParentID")]
        public Group Parent { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(450)]
        [Display(Name = "Name", Description ="Name of the organization")]
        public string Name { get; set; }

        [Display(Name = "Short Unique Identifier", Description = "A unique short identifier for the organization")]
        [Required(AllowEmptyStrings = false)]
        [MaxLength(64)]
        public string ShortName { get; set; }

        public ICollection<GroupAssignment> GroupAssignments { get; } = new List<GroupAssignment>();
        [NotMapped]
        public virtual List<ApplicationUser> Users => GroupAssignments.Select(oa => oa.User).ToList();

        public ICollection<Group> Children { get; } = new List<Group>();
    }
}
