using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// A resource a <see cref="Models.SegmentationService"/> treats as local. The service serves every annotation
    /// context reached from its local resources: a context directly, a group through its member resources and
    /// member groups, an organizational unit through its children. Listing a container saves an administrator
    /// from adding each of its contexts one at a time.
    /// </summary>
    public class SegmentationServiceLocalResource
    {
        [Key]
        public long SegmentationServiceId { get; set; }

        [ForeignKey(nameof(SegmentationServiceId))]
        public virtual SegmentationService SegmentationService { get; set; }

        [Key]
        public long ResourceId { get; set; }

        [ForeignKey(nameof(ResourceId))]
        public virtual Resource Resource { get; set; }
    }
}
