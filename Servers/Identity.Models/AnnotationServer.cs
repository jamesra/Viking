using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// One SQL annotation database, exposed at an annotation URL root. Read / Annotate / Review
    /// grants live here; every <see cref="Volume"/> that points at this server inherits them.
    /// Rows are created by the VikingXML catalog sync from each volume's VolumeToEndpoint element.
    /// </summary>
    public class AnnotationServer : Resource
    {
        /// <summary>
        /// Maximum stored length. Kept short enough for a unique index on SQL Server.
        /// </summary>
        public const int MaxEndpointLength = 400;

        /// <summary>
        /// Normalized annotation URL root (lower-case scheme and host, no default port, no trailing slash).
        /// Unique across annotation servers; the catalog sync matches volumes to servers on this value.
        /// </summary>
        [MaxLength(MaxEndpointLength)]
        [Display(Name = "Annotation Endpoint", Description = "Root URL of the annotation service for this database")]
        public virtual string AnnotationEndpoint { get; set; }

        [MaxLength(128)]
        [Display(Name = "Database Name", Description = "Database name reported by VolumeToEndpoint in the VikingXML")]
        public virtual string AnnotationDatabaseName { get; set; }

        [Display(Name = "Export URL", Description = "URL of the export service for this database")]
        public virtual Uri ExportUrl { get; set; }

        [Display(Name = "Authentication URL", Description = "Identity server the VikingXML names for this database")]
        public virtual Uri AuthenticationUrl { get; set; }

        [InverseProperty(nameof(Volume.AnnotationServer))]
        [Display(Name = "Volumes", Description = "Volumes whose annotations are stored in this database")]
        public virtual List<Volume> Volumes { get; } = new List<Volume>();
    }
}
