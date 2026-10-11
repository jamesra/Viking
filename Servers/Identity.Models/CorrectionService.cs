using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// A section-correction container (GrpcSectionCorrectionService). Annotation contexts are assigned to it
    /// through <see cref="AnnotationContext.CorrectionServiceId"/>. The container authenticates to Identity
    /// as the shared "correction" OAuth client and asks for the assignments of the service whose
    /// <see cref="Resource.Name"/> it is configured with; Viking clients receive
    /// <see cref="Endpoint"/> as the CorrectionServiceUrl of each assigned context.
    /// </summary>
    public class CorrectionService : Resource
    {
        /// <summary>
        /// Public URL Viking clients use to call the correction service gRPC endpoint.
        /// </summary>
        [Display(Name = "Endpoint", Description = "URL clients use to reach the correction service")]
        public virtual Uri Endpoint { get; set; }

        /// <summary>Annotation contexts this service corrects.</summary>
        [InverseProperty(nameof(AnnotationContext.CorrectionService))]
        public virtual List<AnnotationContext> AnnotationContexts { get; } = new List<AnnotationContext>();
    }
}
