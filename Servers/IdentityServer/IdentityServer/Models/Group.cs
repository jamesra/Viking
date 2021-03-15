using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Models
{
    /// <summary>
    /// A resource that can contain sets of users
    /// Records the lab or organization that the user belongs to.
    /// </summary>
    public class Group : Resource
    {  
       // [InverseProperty(nameof(UserToGroupAssignment.Group))]
        [Display(Name = "Users", Description = "Users assigned to group")]
        public virtual List<UserToGroupAssignment> MemberUsers { get; set; } = new List<UserToGroupAssignment>();

        //public ICollection<GrantedGroupPermission> HasPermissions { get; } = new List<GrantedGroupPermission>();
          
        [NotMapped]
        [Display(Name = "Users", Description = "Users assigned to group")]
        public virtual List<ApplicationUser> Users => MemberUsers.Select(oa => oa.User).ToList();
        

        [Display(Name = "Groups", Description = "Groups assigned to group")]
//        [InverseProperty(nameof(GroupToGroupAssignment.Container))]
        public virtual List<GroupToGroupAssignment> MemberGroups { get; set; } = new List<GroupToGroupAssignment>();

        [NotMapped]
        [Display(Name = "Groups", Description = "Groups assigned to group")]
        public virtual List<Group> Groups => MemberGroups.Select(oa => oa.Member).ToList();

        [Display(Name = "Member Of", Description = "Groups we are a member of")]
        //        [InverseProperty(nameof(GroupToGroupAssignment.Container))]
        public virtual List<GroupToGroupAssignment> MemberOfGroups { get; set; } = new List<GroupToGroupAssignment>();

        [NotMapped]
        [Display(Name = "Member Of", Description = "Groups we are a member of")]
        public virtual List<Group> MemberOf => MemberOfGroups.Select(oa => oa.Container).ToList();

        //public ICollection<GrantedGroupPermission> HasPermissions { get; } = new List<GrantedGroupPermission>();

        [Display(Name = "Permissions Held", Description="Permissions this group has been granted to other resources")]
        public virtual List<GrantedGroupPermission> PermissionsHeld { get; set; }
         

        [NotMapped]
        [Display(Name = "User Count", Description = "Number of users assigned to group")]
        public virtual int UsersCount { get { return MemberUsers.Select(oa => oa.User).Count(); } }

        [InverseProperty(nameof(Resource.Parent))]
        [Display(Name = "Owned resources", Description = "Resources contained within this group")]
        public virtual List<Resource> Children { get; } = new List<Resource>();
    }
}
