using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Coordinate space of the pixels in a volume.
    /// </summary>
    public enum VolumePixelSpace
    {
        /// <summary>Tiles are in section (mosaic) space; a slice-to-slice transform maps them into the volume.</summary>
        Section = 0,

        /// <summary>Tiles were assembled pre-registered to the volume; no slice-to-slice warp is applied.</summary>
        Volume = 1
    }

    /// <summary>
    /// One build of images and transforms, described by a VikingXML. Every <see cref="VolumeMirror"/>
    /// of a volume must serve an exact clone, so clients can pick any of them. A different build is a new volume.
    /// Not a permissioned resource: read access comes from the annotation server of the context that uses it.
    /// </summary>
    public class Volume
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "ID", Description = "Database generated ID")]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(128)]
        [Display(Name = "Name", Description = "Name of the volume")]
        public string Name { get; set; }

        [MaxLength(2048)]
        [Display(Name = "Description", Description = "Information about the volume")]
        public string Description { get; set; }

        [MaxLength(64)]
        [Display(Name = "Version", Description = "Label that distinguishes this build from other builds of the same images")]
        public string VersionLabel { get; set; }

        [Display(Name = "Pixel Space", Description = "Section: warped into the volume at view time. Volume: tiles are pre-registered.")]
        public VolumePixelSpace PixelSpace { get; set; } = VolumePixelSpace.Section;

        /// <summary>
        /// SHA-256 (hex) of the VikingXML with the host-specific Volume path removed.
        /// Two volumes with the same hash are probably clones and can be merged into one volume with two mirrors.
        /// </summary>
        [MaxLength(64)]
        [Display(Name = "Content Hash", Description = "Hash of the VikingXML without its host path, used to spot clones")]
        public string ContentHash { get; set; }

        [Display(Name = "Created", Description = "When the volume was registered (UTC)")]
        public DateTime CreatedUtc { get; set; }

        [Display(Name = "Connectome", Description = "Connectome this volume belongs to")]
        public virtual long? ConnectomeId { get; set; }

        [ForeignKey(nameof(ConnectomeId))]
        [Display(Name = "Connectome", Description = "Connectome this volume belongs to")]
        public virtual Connectome Connectome { get; set; }

        [InverseProperty(nameof(VolumeMirror.Volume))]
        [Display(Name = "Mirrors", Description = "Hosts serving an exact copy of these images")]
        public virtual List<VolumeMirror> Mirrors { get; } = new List<VolumeMirror>();

        [InverseProperty(nameof(AnnotationContext.Volume))]
        [Display(Name = "Annotation Contexts", Description = "Contexts that display this volume")]
        public virtual List<AnnotationContext> AnnotationContexts { get; } = new List<AnnotationContext>();
    }
}
