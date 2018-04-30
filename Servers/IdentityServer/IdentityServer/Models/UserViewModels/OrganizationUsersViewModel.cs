using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models.UserViewModels
{
    public class OrganizationDetailsViewModel
    {
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Organization")]
        public string Name { get; set; }

        public List<string> Users { get; set; }

        public List<ApplicationRole> AvailableUsers { get; set; }
    }
}
