using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace IdentityServer.Models
{
    /// <summary>
    /// Table of permissions that have been granted to a specific user/group
    /// </summary>
    public class GrantedGroupPermission : GrantedPermissionBase
    {
        /// <summary>
        /// The group permission grants access to
        /// </summary>
        [Required]
        [Key, ForeignKey(nameof(IdentityServer.Models.Group))]
        [Display(Name = "Group", Description = "Group granted permission")]
        public long GroupId { get; set; }

        /// <summary>
        /// The group being granted permission
        /// </summary>
        [ForeignKey(nameof(GroupId))]
        //[InverseProperty("HasPermissions")]
        public virtual Group PermittedGroup { get; set; }
        
    }
}
