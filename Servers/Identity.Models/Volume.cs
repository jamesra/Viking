using System;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models
{
    public class Volume : Resource
    {
        /// <summary>
        /// URL to access the volume
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL to access resource")]
        public virtual Uri Endpoint { get; set; } 
    }
}
