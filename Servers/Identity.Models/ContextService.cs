using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Models
{
    /// <summary>
    /// A server that works on annotation contexts: a <see cref="CorrectionService"/> or a <see cref="SegmentationService"/>.
    /// An administrator assigns it contexts, groups and organizational units (<see cref="AssignedResources"/>); the
    /// service serves every context reached from them (<c>ResourceMembership.AssignedAnnotationContextsAsync</c>). The
    /// container authenticates to Identity as its kind's shared OAuth client and asks for the contexts of the service
    /// whose <see cref="Resource.Name"/> it is configured with.
    /// </summary>
    public abstract class ContextService : Resource
    {
        /// <summary>
        /// Public URL of the service.
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL clients use to reach the service")]
        public virtual Uri Endpoint { get; set; }

        /// <summary>Contexts, groups and organizational units this service is assigned.</summary>
        [Display(Name = "Assigned resources", Description = "Annotation contexts, groups and organizational units whose contexts this service serves")]
        public virtual List<ContextServiceAssignment> AssignedResources { get; set; } = new List<ContextServiceAssignment>();
    }
}
