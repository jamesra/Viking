using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Links one <see cref="Models.ImageSet"/> to one <see cref="Models.AnnotationServer"/>.
    /// Several volumes may share an annotation server, for example copies of the same images on
    /// different hosts or different builds of them.
    /// </summary>
    public class Volume : Resource
    {
        /// <summary>
        /// URL to access the volume
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL to access resource")]
        public virtual Uri Endpoint { get; set; }

        [Display(Name = "Annotation Server", Description = "Annotation database this volume reads and writes")]
        public virtual long? AnnotationServerId { get; set; }

        [ForeignKey(nameof(AnnotationServerId))]
        [Display(Name = "Annotation Server", Description = "Annotation database this volume reads and writes")]
        public virtual AnnotationServer AnnotationServer { get; set; }

        [Display(Name = "Image Set", Description = "Images displayed for this volume")]
        public virtual long? ImageSetId { get; set; }

        [ForeignKey(nameof(ImageSetId))]
        [Display(Name = "Image Set", Description = "Images displayed for this volume")]
        public virtual ImageSet ImageSet { get; set; }

        /// <summary>
        /// Slice-to-slice transform group (VikingXML StosGroup) used to align the image set with the
        /// annotation database. Null uses the VikingXML default; "None" means the images are already in volume space.
        /// </summary>
        [MaxLength(128)]
        [Display(Name = "Registration", Description = "StosGroup name. Empty uses the VikingXML default; None means no slice-to-slice warp")]
        public virtual string RegistrationName { get; set; }

        [Display(Name = "Catalog Synced", Description = "When the VikingXML was last read by the catalog sync (UTC)")]
        public virtual DateTime? CatalogSyncedUtc { get; set; }

        [MaxLength(1024)]
        [Display(Name = "Catalog Status", Description = "Result of the last catalog sync")]
        public virtual string CatalogSyncMessage { get; set; }
    }
}
