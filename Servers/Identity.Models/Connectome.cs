using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// Scientific product that groups <see cref="AnnotationContext"/>s, <see cref="AnnotationServer"/>s,
    /// and <see cref="Volume"/>s. Not ApiFacing: clients open an annotation context; the default context
    /// keeps today's {Name}.Read|Annotate|Review scopes.
    /// </summary>
    public class Connectome : Resource
    {
        [Display(Name = "Default Annotation Context", Description = "Primary context clients open for this connectome")]
        public virtual long? DefaultAnnotationContextId { get; set; }

        [ForeignKey(nameof(DefaultAnnotationContextId))]
        [Display(Name = "Default Annotation Context", Description = "Primary context clients open for this connectome")]
        public virtual AnnotationContext DefaultAnnotationContext { get; set; }

        [InverseProperty(nameof(AnnotationContext.Connectome))]
        [Display(Name = "Annotation Contexts", Description = "Openable bindings of volumes to annotation databases")]
        public virtual List<AnnotationContext> AnnotationContexts { get; } = new List<AnnotationContext>();

        [InverseProperty(nameof(AnnotationServer.Connectome))]
        [Display(Name = "Annotation Servers", Description = "Annotation databases that belong to this connectome")]
        public virtual List<AnnotationServer> AnnotationServers { get; } = new List<AnnotationServer>();

        [InverseProperty(nameof(Volume.Connectome))]
        [Display(Name = "Volumes", Description = "Image builds that belong to this connectome")]
        public virtual List<Volume> Volumes { get; } = new List<Volume>();
    }
}
