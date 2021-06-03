using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace IdentityServer.Models
{
    /// <summary>
    /// A resource that can contain sets of users
    /// Records the lab or organization that the user belongs to.
    /// </summary>
    public class Group : Resource
    {  
       // [InverseProperty(nameof(UserToGroupAssignment.Group))]
        [Display(Name = "Member Users", Description = "Users assigned to group")]
        public virtual List<UserToGroupAssignment> MemberUsers { get; set; } = new List<UserToGroupAssignment>();

        //public ICollection<GrantedGroupPermission> HasPermissions { get; } = new List<GrantedGroupPermission>();
          
        [NotMapped]
        [Display(Name = "Member Users", Description = "Users assigned to group")]
        public virtual List<ApplicationUser> Users => MemberUsers.Select(oa => oa.User).ToList();
        

        [Display(Name = "Member Groups", Description = "Groups assigned to group")]
//        [InverseProperty(nameof(GroupToGroupAssignment.Container))]
        public virtual List<GroupToGroupAssignment> MemberGroups { get; set; } = new List<GroupToGroupAssignment>();

        /// <summary>
        /// Computed column to save round-trips to the database when recursively determining user group membership
        /// </summary>
        [NotMapped]
        [Display(Name = "Number of member groups")]
        public virtual int NumMemberGroups { get { return MemberGroups.Count(); } }

        [NotMapped]
        [Display(Name = "Member Groups", Description = "Groups assigned to group")]
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
        [Display(Name = "Number of member users", Description = "Number of users assigned to group")]
        public virtual int NumMemberUsers { get { return MemberUsers.Count(); } }
    }
}
