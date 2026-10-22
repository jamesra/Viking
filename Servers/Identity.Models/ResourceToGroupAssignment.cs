using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Puts a resource that is not a group (an annotation context, a service, an organizational unit) in a
    /// <see cref="Models.Group"/>, so resources can be grouped by something other than their owning
    /// organizational unit, such as where their servers run. Membership grants no permissions: permission
    /// resolution reads only <see cref="UserToGroupAssignment"/> and <see cref="GroupToGroupAssignment"/>.
    /// Groups nest through <see cref="GroupToGroupAssignment"/>, never through this table.
    /// </summary>
    public class ResourceToGroupAssignment
    {
        /// <summary>The group the resource is a member of.</summary>
        [Key]
        public long GroupId { get; set; }

        [ForeignKey(nameof(GroupId))]
        public virtual Group Group { get; set; }

        /// <summary>The member resource.</summary>
        [Key]
        public long ResourceId { get; set; }

        [ForeignKey(nameof(ResourceId))]
        public virtual Resource Resource { get; set; }
    }
}
