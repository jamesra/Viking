using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models.UserViewModels
{
    public class UserRolesViewModel
    {
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Username")]
        public string Username { get; set; }
         
        public List<string> Roles { get; set; }
        
        public List<ApplicationRole> AvailableRoles { get; set; }
    }
}
