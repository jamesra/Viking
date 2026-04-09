using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;



namespace Viking.Identity.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public virtual ICollection<UserToGroupAssignment> GroupAssignments { get; set; }
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

        [Display(Name = "Permissions Held", Description = "Permissions this user has been granted directly")]
        public virtual List<GrantedUserPermission> PermissionsHeld { get; set; }
    }
}
