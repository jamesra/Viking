using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models.UserViewModels
{
    public class UserClaimRequestViewModel
    {
        [Required]
        public string UserId { get; set; }

        [Display(Name = "Available Organizations")]
        public List<NamedItemSelectedViewModel<long>> AvailableOrganizations { get; set; }

        //public List<RoleSelectedViewModel> AvailableRoles { get; set; }

        [Display(Name ="Message", Description = "This text will be included in a message to admins.  Feel free to include special requests or an introduction")]
        public string UserComments { get; set; }

        [Display(Name = "Request a new organization", Description = "Proposed name for a new organization")]
        public string NewOrganization { get; set; }
    }
}
