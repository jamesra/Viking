using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// One host serving an exact copy of a <see cref="Volume"/>. Clients may load-balance across
    /// enabled mirrors; lower <see cref="Priority"/> values are preferred.
    /// </summary>
    public class VolumeMirror
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "ID", Description = "Database generated ID")]
        public long Id { get; set; }

        [Required]
        [Display(Name = "Volume", Description = "Volume this host serves")]
        public long VolumeId { get; set; }

        [ForeignKey(nameof(VolumeId))]
        [Display(Name = "Volume", Description = "Volume this host serves")]
        public virtual Volume Volume { get; set; }

        [Required]
        [Display(Name = "VikingXML URL", Description = "URL of the VikingXML on this host")]
        public Uri VikingXmlUrl { get; set; }

        [MaxLength(64)]
        [Display(Name = "Region", Description = "Where the host is, for example US-West or EU")]
        public string RegionLabel { get; set; }

        [Display(Name = "Priority", Description = "Lower values are preferred")]
        public int Priority { get; set; }

        [Display(Name = "Enabled", Description = "Disabled mirrors are not offered to clients")]
        public bool Enabled { get; set; } = true;

        [Display(Name = "Last Check", Description = "When the catalog sync last fetched this mirror (UTC)")]
        public DateTime? LastCheckUtc { get; set; }

        [MaxLength(256)]
        [Display(Name = "Last Status", Description = "Result of the last fetch")]
        public string LastStatus { get; set; }
    }
}
