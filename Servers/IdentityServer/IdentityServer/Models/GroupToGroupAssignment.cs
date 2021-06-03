using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models
{
    /// <summary>
    /// Allows adding a group as a member of another group
    /// </summary>
    public class GroupToGroupAssignment
    {
        /// <summary>
        /// The group we are assigning to another group
        /// </summary>
        [Key]
        [ForeignKey(nameof(Group.Id))]
        public long MemberGroupId { get; set; }

        [ForeignKey(nameof(MemberGroupId))]
        public virtual Group Member { get; set; }

        /// <summary>
        /// The group that our member is being assigned to.
        /// </summary>
        [Key]
        [ForeignKey(nameof(Group.Id))]
        public long ContainerGroupId { get; set; }

        [ForeignKey(nameof(ContainerGroupId))]
        public virtual Group Container { get; set; }
    }
}
