using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Links a <see cref="AnnotationContext"/> (annotation context) to an <see cref="AnnotationServer"/>.
    /// A context may have several servers; exactly one link may be <see cref="IsDefault"/> when any exist.
    /// The denormalized <see cref="AnnotationContext.AnnotationServerId"/> mirrors the default for legacy callers.
    /// </summary>
    public class AnnotationContextServer
    {
        [Required]
        public virtual long AnnotationContextId { get; set; }

        [ForeignKey(nameof(AnnotationContextId))]
        public virtual AnnotationContext AnnotationContext { get; set; }

        [Required]
        public virtual long AnnotationServerId { get; set; }

        [ForeignKey(nameof(AnnotationServerId))]
        public virtual AnnotationServer AnnotationServer { get; set; }

        /// <summary>
        /// When true, Viking uses this server unless the user picks another linked server at login.
        /// </summary>
        public virtual bool IsDefault { get; set; }
    }
}
