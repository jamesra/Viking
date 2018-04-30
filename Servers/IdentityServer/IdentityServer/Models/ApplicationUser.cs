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
        public ICollection<OrganizationAssignment> OrganizationAssignments { get; set; }
        [NotMapped]
        public virtual IEnumerable<Organization> Organizations => OrganizationAssignments.Select(oa => oa.Organization);

        [Display(Name="Registration Date", Description ="Date of registration")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime RegistrationDate { get; set; }
    }
}
