using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.UserViewModels
{
    /// <summary>
    /// Lists all available roles, and lists the roles of each user
    /// </summary>
    public class ListUserRolesViewModel
    {
        public IList<ApplicationRole> AvailableRoles { get; set; }

        public IList<UserRolesViewModel> UsersRoles { get; set; }
    }

    /// <summary>
    /// Lists the roles a user has
    /// </summary>
    public class UserRolesViewModel
    {
        /// <summary>
        /// Id of the user
        /// </summary>
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Username")]
        public string Username { get; set; }
         
        /// <summary>
        /// Roles the user has
        /// </summary>
        public IList<string> Roles { get; set; } 
    }

    /// <summary>
    /// Includes available roles to the user and which roles the user has
    /// </summary>
    public class EditUserRolesViewModel
    {
        /// <summary>
        /// Id of the user
        /// </summary>
        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Username")]
        public string Username { get; set; }

        /// <summary>
        /// Roles the user has
        /// </summary>
        public IList<string> Roles { get; set; }

        /// <summary>
        /// Roles available to the user
        /// </summary>
        public IList<ApplicationRole> AvailableRoles { get; set; }
    }
}
