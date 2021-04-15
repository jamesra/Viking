using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IdentityServer.Models.UserViewModels
{
    public class GroupMembershipViewModel
    {
        /// <summary>
        /// All users in the database, the selected property is true if they are a member of the Group
        /// </summary>
        [Display(Name = "Member Users")]
        public IList<UserSelectedViewModel> UserList { get; set; }

        /// <summary>
        /// All groups in the database, the selected property is true if they are a member of the Group
        /// </summary>
        [Display(Name = "Member Groups")]
        public IList<GroupSelectedViewModel> GroupList { get; set; }
    }

    [BindProperties]
    public class GroupEditViewModel
    {
        [Display(Name = "Group")]
        public Group Group { get; set; }

        /*
        [Key]
        [Required]
        [Display(Name = "ID", Description = "Database generated ID")]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [Display(Name = "Name")]
        [MaxLength(450)]
        public string Name { get; set; }

        [Display(Name = "Parent ID")]
        public long? ParentId { get; set; }


        [Display(Name = "Parent")]
        public string Parent { get; set; }

        [Display(Name = "Description", Description = "Information about the group")]
        [MaxLength(2048)]
        public string Description { get; set; }
        */

        [Display(Name = "Members")]
        public GroupMembershipViewModel Members { get; set; }

        [Display(Name = "Owner of")]
        public IList<GroupDetailsViewModel> Children { get; set; }
    }
}
