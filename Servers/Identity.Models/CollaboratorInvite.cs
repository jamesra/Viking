using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// One-time invite for a collaborator to register and receive org admin + full volume access.
    /// </summary>
    public class CollaboratorInvite
    {
        [Key]
        [Required]
        [MaxLength(128)]
        public string Token { get; set; }

        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public long OrganizationalUnitId { get; set; }

        [ForeignKey(nameof(OrganizationalUnitId))]
        public virtual OrganizationalUnit OrganizationalUnit { get; set; }

        [Required]
        public long VolumeId { get; set; }

        [ForeignKey(nameof(VolumeId))]
        public virtual Volume Volume { get; set; }

        [Required]
        [MaxLength(450)]
        public string CreatedByUserId { get; set; }

        [Required]
        public DateTime CreatedAtUtc { get; set; }

        [Required]
        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? ClaimedAtUtc { get; set; }

        [MaxLength(450)]
        public string ClaimedByUserId { get; set; }
    }
}
