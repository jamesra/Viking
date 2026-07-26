using System;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models
{
    /// <summary>
    /// One-use, short-lived code for viking://open protocol.
    /// Stored when user clicks "Open in Viking"; exchanged by the client for an API token.
    /// </summary>
    public class VikingLaunchCode
    {
        /// <summary>Opaque code (e.g. GUID or random bytes, base64url). Primary key.</summary>
        [Key]
        [Required]
        [MaxLength(128)]
        public string Code { get; set; }

        /// <summary>User id the code is bound to.</summary>
        [Required]
        [MaxLength(450)]
        public string UserId { get; set; }

        /// <summary>Optional volume URL to open.</summary>
        [MaxLength(2048)]
        public string VolumeUrl { get; set; }

        /// <summary>When the code expires (UTC).</summary>
        [Required]
        public DateTime ExpiresAtUtc { get; set; }

        /// <summary>When the code was used (UTC); null until first successful exchange.</summary>
        public DateTime? UsedAtUtc { get; set; }
    }
}
