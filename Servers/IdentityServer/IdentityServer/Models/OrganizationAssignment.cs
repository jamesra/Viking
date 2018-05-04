using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdentityServer.Models
{
    public class OrganizationAssignment
    {
        [Key]
        [ForeignKey("Id")]
        public string UserId { get; set; }

        public virtual ApplicationUser User { get; set; }

        [Key]
        [ForeignKey("Id")]
        public long OrganizationId { get; set; }

        public virtual Organization Organization { get; set; } 
    }
}
