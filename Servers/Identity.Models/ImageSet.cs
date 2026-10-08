using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Coordinate space of the pixels in an image set.
    /// </summary>
    public enum ImageSetPixelSpace
    {
        /// <summary>Tiles are in section (mosaic) space; a slice-to-slice transform maps them into the volume.</summary>
        Section = 0,

        /// <summary>Tiles were assembled pre-registered to the volume; no slice-to-slice warp is applied.</summary>
        Volume = 1
    }

    /// <summary>
    /// One build of the images for a volume, described by a VikingXML. Every <see cref="ImageSetMirror"/>
    /// of a set must serve an exact clone, so clients can pick any of them. A different build is a new set.
    /// Not a permissioned resource: read access comes from the annotation server of the volume that uses it.
    /// </summary>
    public class ImageSet
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "ID", Description = "Database generated ID")]
        public long Id { get; set; }

        [Required(AllowEmptyStrings = false)]
        [MaxLength(128)]
        [Display(Name = "Name", Description = "Name of the image set")]
        public string Name { get; set; }

        [MaxLength(2048)]
        [Display(Name = "Description", Description = "Information about the image set")]
        public string Description { get; set; }

        [MaxLength(64)]
        [Display(Name = "Version", Description = "Label that distinguishes this build from other builds of the same images")]
        public string VersionLabel { get; set; }

        [Display(Name = "Pixel Space", Description = "Section: warped into the volume at view time. Volume: tiles are pre-registered.")]
        public ImageSetPixelSpace PixelSpace { get; set; } = ImageSetPixelSpace.Section;

        /// <summary>
        /// SHA-256 (hex) of the VikingXML with the host-specific Volume path removed.
        /// Two sets with the same hash are probably clones and can be merged into one set with two mirrors.
        /// </summary>
        [MaxLength(64)]
        [Display(Name = "Content Hash", Description = "Hash of the VikingXML without its host path, used to spot clones")]
        public string ContentHash { get; set; }

        [Display(Name = "Created", Description = "When the image set was registered (UTC)")]
        public DateTime CreatedUtc { get; set; }

        [InverseProperty(nameof(ImageSetMirror.ImageSet))]
        [Display(Name = "Mirrors", Description = "Hosts serving an exact copy of these images")]
        public virtual List<ImageSetMirror> Mirrors { get; } = new List<ImageSetMirror>();

        [InverseProperty(nameof(Volume.ImageSet))]
        [Display(Name = "Volumes", Description = "Volumes that display this image set")]
        public virtual List<Volume> Volumes { get; } = new List<Volume>();
    }
}
