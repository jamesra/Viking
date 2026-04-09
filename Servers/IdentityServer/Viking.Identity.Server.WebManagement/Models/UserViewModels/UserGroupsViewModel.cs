using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    public class UserGroupsViewModel
    {
        [Required]
        [Display(Name = "ID")]
        public string Id { get; set; }

        [Required] 
        [Display(Name = "Name")]
        public string Name { get; set; }

        /// <summary>
        /// All users in the database, the selected property is true if they are a member of the organization
        /// </summary>
        [Display(Name = "Organizations")]
        public List<GroupSelectedViewModel> Organizations { get; set; }
    }
}
