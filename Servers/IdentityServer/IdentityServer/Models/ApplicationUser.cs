using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;



namespace IdentityServer.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public ICollection<GroupAssignment> GroupAssignments { get; set; }
        [NotMapped]
        public virtual List<Group> Groups => GroupAssignments?.Select(oa => oa.Group).ToList();

        [Display(Name="Registration Date", Description ="Date of registration")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime RegistrationDate { get; set; }

        [Required(AllowEmptyStrings =false)]
        [Display(Name = "First Name", Description = "Given Name")]
        public string GivenName { get; set; }

        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Last Name", Description = "Family Name")]
        public string FamilyName { get; set; }
    }
}
