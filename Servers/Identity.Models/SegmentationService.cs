using System;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models
{
    public class SegmentationService : Resource
    {
        // Inherits all properties from Resource base class
        
        /// <summary>
        /// URL to access the segmentation service
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL to access resource")]
        public virtual Uri Endpoint { get; set; }
    }
}
