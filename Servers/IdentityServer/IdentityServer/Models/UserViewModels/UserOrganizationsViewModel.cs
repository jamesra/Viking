using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models.UserViewModels
{
    public class UserOrganizationsViewModel
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
        public List<OrganizationSelectedViewModel> Organizations { get; set; }
    }
}
