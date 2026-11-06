using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// A resource assigned to a <see cref="ContextService"/>. The service serves every annotation context reached from
    /// its assigned resources: a context directly, a group through its member resources and member groups, an
    /// organizational unit through its children. Assigning a container saves an administrator from adding each of its
    /// contexts one at a time.
    /// </summary>
    public class ContextServiceAssignment
    {
        [Key]
        public long ServiceId { get; set; }

        [ForeignKey(nameof(ServiceId))]
        public virtual ContextService Service { get; set; }

        [Key]
        public long ResourceId { get; set; }

        [ForeignKey(nameof(ResourceId))]
        public virtual Resource Resource { get; set; }
    }
}
