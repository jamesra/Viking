using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Annotation context: one <see cref="Models.ImageSet"/> plus zero or more linked
    /// <see cref="Models.AnnotationServer"/> rows (via <see cref="AnnotationServerLinks"/>).
    /// <see cref="AnnotationServerId"/> is the denormalized default for legacy callers.
    /// </summary>
    public class Volume : Resource
    {
        /// <summary>
        /// URL to access the volume
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL to access resource")]
        public virtual Uri Endpoint { get; set; }

        /// <summary>
        /// Default annotation server (mirrors the <see cref="VolumeAnnotationServer.IsDefault"/> link).
        /// Null when the volume is images-only or not yet linked; Viking then uses VikingXML.
        /// </summary>
        [Display(Name = "Annotation Server", Description = "Default annotation database this volume reads and writes")]
        public virtual long? AnnotationServerId { get; set; }

        [ForeignKey(nameof(AnnotationServerId))]
        [Display(Name = "Annotation Server", Description = "Default annotation database this volume reads and writes")]
        public virtual AnnotationServer AnnotationServer { get; set; }

        /// <summary>All annotation servers available for this context (default and alternates).</summary>
        [InverseProperty(nameof(VolumeAnnotationServer.Volume))]
        public virtual List<VolumeAnnotationServer> AnnotationServerLinks { get; } = new List<VolumeAnnotationServer>();

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
