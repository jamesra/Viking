using System.ComponentModel.DataAnnotations;

namespace Viking.Identity.Server.WebManagement.Models.UserViewModels
{
    /// <summary>
    /// Both permission grids of an annotation context on one page: Access on the context, and
    /// Read / Annotate / Review on the annotation database (server) it uses, when it has one.
    /// </summary>
    public class ContextPermissionsEditViewModel
    {
        public long ContextId { get; set; }

        public string ContextName { get; set; }

        /// <summary>Access to the context itself.</summary>
        public ResourcePermissionsEditGridViewModel Context { get; set; }

        /// <summary>
        /// Id of the context's annotation server, or null when the context has none (images only).
        /// Display only: the POST takes the server from the context, never from this value.
        /// </summary>
        public long? DatabaseId { get; set; }

        public string DatabaseName { get; set; }

        /// <summary>Rights to the annotation database. Null when the context has no server, or the user cannot edit it.</summary>
        public ResourcePermissionsEditGridViewModel Database { get; set; }

        /// <summary>True when the context has a server whose grants this user may not edit.</summary>
        [Display(Name = "Database permissions hidden")]
        public bool DatabaseNotEditable { get; set; }
    }
}
