using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Viking.Identity.Models
{
    /// <summary>
    /// One SQL annotation database, exposed at a service <see cref="BaseUrl"/>. Read / Annotate / Review
    /// grants live here; every <see cref="Volume"/> linked to this server inherits them.
    /// Rows are created by the VikingXML catalog sync or assigned manually on a volume.
    /// </summary>
    public class AnnotationServer : Resource
    {
        /// <summary>
        /// Maximum stored length. Kept short enough for a unique index on SQL Server.
        /// </summary>
        public const int MaxEndpointLength = 400;

        /// <summary>
        /// Normalized service root (lower-case scheme and host, no default port, no trailing slash).
        /// Unique across annotation servers. Annotation / OData / Export URLs are derived from this
        /// unless an override (e.g. <see cref="ExportUrl"/>) is set.
        /// </summary>
        [MaxLength(MaxEndpointLength)]
        [Display(Name = "Base URL", Description = "Service root used to build Annotation, OData, and Export URLs")]
        public virtual string BaseUrl { get; set; }

        [MaxLength(128)]
        [Display(Name = "Database Name", Description = "Database name reported by VolumeToEndpoint in the VikingXML")]
        public virtual string AnnotationDatabaseName { get; set; }

        /// <summary>
        /// Optional Export override when the Export host is not <c>{BaseUrl}/Export/</c>.
        /// </summary>
        [Display(Name = "Export URL", Description = "Override for the export service; leave empty to use BaseUrl/Export/")]
        public virtual Uri ExportUrl { get; set; }

        [Display(Name = "Authentication URL", Description = "Identity server the VikingXML names for this database")]
        public virtual Uri AuthenticationUrl { get; set; }

        /// <summary>
        /// Volumes whose denormalized default pointer is this server.
        /// Prefer <see cref="VolumeLinks"/> for the full many-to-many set.
        /// </summary>
        [InverseProperty(nameof(Volume.AnnotationServer))]
        [Display(Name = "Default Volumes", Description = "Volumes whose default annotation server is this database")]
        public virtual List<Volume> Volumes { get; } = new List<Volume>();

        /// <summary>All volume links that include this server (default or alternate).</summary>
        [InverseProperty(nameof(VolumeAnnotationServer.AnnotationServer))]
        public virtual List<VolumeAnnotationServer> VolumeLinks { get; } = new List<VolumeAnnotationServer>();
    }
}
