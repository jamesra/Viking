using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// One openable binding of a <see cref="Models.Volume"/> to an <see cref="Models.AnnotationServer"/>
    /// under a <see cref="Models.Connectome"/>. Clients launch a context; the connectome's default
    /// context keeps today's {Name}.Read|Annotate|Review token scopes.
    /// </summary>
    public class AnnotationContext : Resource
    {
        /// <summary>
        /// Preferred VikingXML / volume URL for clients that only read <see cref="Endpoint"/>.
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL to access resource")]
        public virtual Uri Endpoint { get; set; }

        [Display(Name = "Connectome", Description = "Connectome this annotation context belongs to")]
        public virtual long? ConnectomeId { get; set; }

        [ForeignKey(nameof(ConnectomeId))]
        [Display(Name = "Connectome", Description = "Connectome this annotation context belongs to")]
        public virtual Connectome Connectome { get; set; }

        [Display(Name = "Annotation Server", Description = "Annotation database this context reads and writes")]
        public virtual long? AnnotationServerId { get; set; }

        [ForeignKey(nameof(AnnotationServerId))]
        [Display(Name = "Annotation Server", Description = "Annotation database this context reads and writes")]
        public virtual AnnotationServer AnnotationServer { get; set; }

        /// <summary>
        /// True when an administrator chose <see cref="AnnotationServerId"/> by hand. The catalog sync then
        /// keeps that server instead of re-linking from the VikingXML VolumeToEndpoint.
        /// </summary>
        [Display(Name = "Annotation Server Override", Description = "When set, the catalog sync does not replace the annotation server from the VikingXML")]
        public virtual bool AnnotationServerPinned { get; set; }

        /// <summary>All annotation servers available for this context (default and alternates).</summary>
        [InverseProperty(nameof(AnnotationContextServer.AnnotationContext))]
        public virtual List<AnnotationContextServer> AnnotationServerLinks { get; } = new List<AnnotationContextServer>();

        [Display(Name = "Volume", Description = "Images and transforms displayed for this context")]
        public virtual long? VolumeId { get; set; }

        [ForeignKey(nameof(VolumeId))]
        [Display(Name = "Volume", Description = "Images and transforms displayed for this context")]
        public virtual Volume Volume { get; set; }

        /// <summary>
        /// Slice-to-slice transform group (VikingXML StosGroup) used to align the volume with the
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
