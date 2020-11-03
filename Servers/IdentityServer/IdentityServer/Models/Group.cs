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

        /// <summary>
        /// null if there is no parent
        /// </summary>
        [ForeignKey("ParentID")]
        public Group Parent { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(128)]
        [Remote(action: "VerifyUniqueGroupName", controller: "Groups")]
        [Display(Name = "Name", Description ="Name of the group")]
        public string Name { get; set; }

        [Display(Name = "Description", Description = "Information about the group")] 
        [MaxLength(2048)]
        public string Description { get; set; }

        public ICollection<GroupAssignment> GroupAssignments { get; } = new List<GroupAssignment>();

        [NotMapped]
        public virtual List<ApplicationUser> Users => GroupAssignments.Select(oa => oa.User).ToList();
         
        [NotMapped]
        public virtual int UsersCount { get { return GroupAssignments.Select(oa => oa.User).Count(); } }

        public ICollection<Group> Children { get; } = new List<Group>();
    }
}
