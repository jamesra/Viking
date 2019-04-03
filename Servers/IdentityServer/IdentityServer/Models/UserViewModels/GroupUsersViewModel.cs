using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models.UserViewModels
{
    public class GroupDetailsViewModel
    {
        [Key]
        [Required]
        [Display(Name = "ID", Description ="Database generated ID")]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Organization")]
        [MaxLength(450)]
        public string Name { get; set; }

        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Short Unique Identifier", Description = "A unique short identifier for the organization.  This value is used for affiliation claims placed in tokens.")]
        [MaxLength(64)]
        public string ShortName { get; set; }

        /// <summary>
        /// All users in the database, the selected property is true if they are a member of the organization
        /// </summary>
        [Display(Name = "Users")]
        public List<UserSelectedViewModel> UserList { get; set; }
    }
}
