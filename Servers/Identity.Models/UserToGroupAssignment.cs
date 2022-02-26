using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    public class UserToGroupAssignment
    {
        [Key]
        [ForeignKey(nameof(ApplicationUser))]
        public string UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser User { get; set; }

        [Key]
        [ForeignKey(nameof(Models.Group))]
        public long GroupId { get; set; }

        [ForeignKey(nameof(GroupId))]
        public virtual Group Group { get; set; } 
    }
}
