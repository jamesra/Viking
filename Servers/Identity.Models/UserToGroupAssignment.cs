using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    public class UserToGroupAssignment
    {
        [Key]
        [ForeignKey(nameof(ApplicationUser))]
        public string UserId { get; init; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; init; }

        [Key]
        [ForeignKey(nameof(Models.Group))]
        public long GroupId { get; init; }

        [ForeignKey(nameof(GroupId))]
        public virtual Group Group { get; init; } 
    }
}
