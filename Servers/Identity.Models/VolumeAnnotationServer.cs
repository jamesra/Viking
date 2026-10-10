using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Links a <see cref="Volume"/> (annotation context) to an <see cref="AnnotationServer"/>.
    /// A volume may have several servers; exactly one link may be <see cref="IsDefault"/> when any exist.
    /// The denormalized <see cref="Volume.AnnotationServerId"/> mirrors the default for legacy callers.
    /// </summary>
    public class VolumeAnnotationServer
    {
        [Required]
        public virtual long VolumeId { get; set; }

        [ForeignKey(nameof(VolumeId))]
        public virtual Volume Volume { get; set; }

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
