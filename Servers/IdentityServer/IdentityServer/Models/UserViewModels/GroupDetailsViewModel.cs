using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Models.UserViewModels
{
    public class GroupDetailsViewModel
    {
        [Key]
        [Required]
        [Display(Name = "ID", Description ="Database generated ID")]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Name")]
        [MaxLength(450)]
        public string Name { get; set; }

        [Display(Name = "Description", Description = "Information about the group")]
        [MaxLength(2048)]
        public string Description { get; set; }

        /// <summary>
        /// All users in the database, the selected property is true if they are a member of the Group
        /// </summary>
        [Display(Name = "Members")]
        public List<UserSelectedViewModel> UserList { get; set; }

        /// <summary>
        /// All groups in the database, the selected property is true if they are a member of the Group
        /// </summary>
        [Display(Name = "Member Groups")]
        public List<GroupSelectedViewModel> GroupList { get; set; }

        [Display(Name = "Owner of")]
        public List<GroupDetailsViewModel> Children { get; set; }
    }
}
