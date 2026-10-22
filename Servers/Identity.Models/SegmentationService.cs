using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models
{
    /// <summary>
    /// A segmentation server (SegmentationServer). The container authenticates to Identity as the shared
    /// "segmentation" OAuth client and asks for the annotation contexts reached from the
    /// <see cref="LocalResources"/> of the service whose <see cref="Resource.Name"/> it is configured with;
    /// for those contexts it reads tiles itself instead of asking the Viking client for them.
    /// </summary>
    public class SegmentationService : Resource
    {
        /// <summary>
        /// URL to access the segmentation service
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL to access resource")]
        public virtual Uri Endpoint { get; set; }

        /// <summary>Contexts, groups and organizational units this service treats as local.</summary>
        [Display(Name = "Local resources", Description = "Annotation contexts, groups and organizational units whose contexts this service reads tiles for")]
        public virtual List<SegmentationServiceLocalResource> LocalResources { get; set; } = new List<SegmentationServiceLocalResource>();
    }
}
